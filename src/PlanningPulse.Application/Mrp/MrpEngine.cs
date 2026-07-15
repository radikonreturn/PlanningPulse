using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlanningPulse.Application.Mrp;

public sealed class MrpEngine : IMrpEngine
{
    private readonly IMrpPlanningDataProvider _planningDataProvider;
    private readonly Dictionary<LotSizingMethod, ILotSizingStrategy> _strategies;

    public MrpEngine(
        IMrpPlanningDataProvider planningDataProvider,
        IEnumerable<ILotSizingStrategy> strategies)
    {
        _planningDataProvider = planningDataProvider;
        _strategies = strategies.ToDictionary(s => s.Method);
    }

    public Task<IReadOnlyCollection<MrpRecommendation>> PlanAsync(
        IReadOnlyCollection<GrossRequirement> grossRequirements,
        LotSizingMethod lotSizingMethod,
        CancellationToken cancellationToken)
    {
        return PlanInternalAsync(grossRequirements, lotSizingMethod, cancellationToken);
    }

    private async Task<IReadOnlyCollection<MrpRecommendation>> PlanInternalAsync(
        IReadOnlyCollection<GrossRequirement> grossRequirements,
        LotSizingMethod lotSizingMethod,
        CancellationToken cancellationToken)
    {
        if (grossRequirements.Count == 0)
        {
            return Array.Empty<MrpRecommendation>();
        }

        ValidateGrossRequirements(grossRequirements);

        var snapshot = await _planningDataProvider.GetPlanningSnapshotAsync(
            grossRequirements.Select(x => x.ItemId).Distinct().ToArray(),
            cancellationToken);

        var recommendations = new List<MrpRecommendation>();
        var exceptions = new List<MrpRecommendation>();

        // Check for missing items in snapshot first
        foreach (var req in grossRequirements)
        {
            if (!snapshot.Items.ContainsKey(req.ItemId))
            {
                exceptions.Add(new MrpRecommendation(
                    req.ItemId,
                    req.Quantity,
                    req.RequiredDate,
                    req.RequiredDate,
                    "Exception",
                    "Item is missing from the planning snapshot."));
            }
        }

        // Calculate Low-Level Codes
        var llc = CalculateLowLevelCodes(snapshot, out var cycledItems);
        if (llc == null)
        {
            // BOM cycle detected. Add exceptions for cycled items
            foreach (var req in grossRequirements)
            {
                if (cycledItems.Contains(req.ItemId))
                {
                    exceptions.Add(new MrpRecommendation(
                        req.ItemId,
                        req.Quantity,
                        req.RequiredDate,
                        req.RequiredDate,
                        "Exception",
                        "BOM cycle detected."));
                }
            }
            return exceptions.ToArray();
        }

        // Group gross requirements by item
        var grossReqs = new Dictionary<Guid, List<GrossRequirement>>();
        foreach (var req in grossRequirements)
        {
            if (!snapshot.Items.ContainsKey(req.ItemId))
            {
                continue;
            }

            if (!grossReqs.TryGetValue(req.ItemId, out var list))
            {
                list = new List<GrossRequirement>();
                grossReqs[req.ItemId] = list;
            }
            list.Add(req);
        }

        // Sort items by low-level code (highest level/shallowest first, components later)
        var sortedItemIds = snapshot.Items.Keys
            .OrderBy(id => llc.TryGetValue(id, out var code) ? code : 999)
            .ToArray();

        // Track available inventory balance over time.
        // Key: ItemId. Value: current available balance.
        var availableInventory = snapshot.InventoryByItem.ToDictionary(
            x => x.Key,
            x => x.Value.OnHandQuantity - x.Value.AllocatedQuantity + x.Value.OnOrderQuantity);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var itemId in sortedItemIds)
        {
            if (!snapshot.Items.TryGetValue(itemId, out var item))
            {
                continue;
            }

            // Get gross requirements list
            if (!grossReqs.TryGetValue(itemId, out var reqList))
            {
                reqList = new List<GrossRequirement>();
                grossReqs[itemId] = reqList;
            }

            // Get safety stock and current inventory position
            availableInventory.TryGetValue(itemId, out var available);
            var safetyStock = item.SafetyStockQuantity;

            // If currently below safety stock, and we have no requirements or the first requirement is in the future,
            // we should inject a requirement of 0 at `today` to trigger safety stock replenishment.
            if (item.Type != MrpItemType.Phantom && available < safetyStock)
            {
                var hasTodayReq = reqList.Any(r => r.RequiredDate <= today);
                if (!hasTodayReq)
                {
                    reqList.Add(new GrossRequirement(itemId, 0m, today));
                }
            }

            if (reqList.Count == 0)
            {
                continue;
            }

            // Group requirements by date, sorted chronologically.
            var chronologicalGroups = reqList
                .GroupBy(r => r.RequiredDate)
                .OrderBy(g => g.Key)
                .ToArray();

            foreach (var group in chronologicalGroups)
            {
                var reqDate = group.Key;
                var grossQuantity = group.Sum(r => r.Quantity);

                if (item.Type == MrpItemType.Phantom)
                {
                    // Phantoms do not have inventory or safety stock, they are transient.
                    // Planned quantity is equal to gross requirements.
                    var plannedQuantity = grossQuantity;
                    var releaseDate = reqDate; // Phantoms have no lead time.

                    // Explode immediately to components
                    ExplodeToComponents(itemId, plannedQuantity, releaseDate, snapshot, grossReqs, exceptions, reqDate);
                }
                else
                {
                    // Net requirements calculation
                    var shortage = (grossQuantity + safetyStock) - available;
                    if (shortage > 0)
                    {
                        var netQuantity = shortage;

                        if (!_strategies.TryGetValue(lotSizingMethod, out var strategy))
                        {
                            strategy = _strategies[LotSizingMethod.LotForLot];
                        }

                        var plannedQuantity = strategy.CalculateOrderQuantity(netQuantity, item);
                        var releaseDate = CalculateReleaseDate(reqDate, item, snapshot);
                        var recommendationType = item.Type == MrpItemType.Purchased ? "Purchase" : "Production";
                        var reason = strategy.GetReason(netQuantity, plannedQuantity, item);

                        recommendations.Add(new MrpRecommendation(
                            item.ItemId,
                            plannedQuantity,
                            releaseDate,
                            reqDate,
                            recommendationType,
                            reason
                        ));

                        // Add planned quantity to available inventory, and subtract gross requirement
                        available = available + plannedQuantity - grossQuantity;

                        // Explode component gross requirements at release date
                        if (item.Type == MrpItemType.Manufactured)
                        {
                            ExplodeToComponents(itemId, plannedQuantity, releaseDate, snapshot, grossReqs, exceptions, reqDate);
                        }
                    }
                    else
                    {
                        // Sufficient inventory. Just consume from available.
                        available -= grossQuantity;
                    }
                }
            }

            // Save the final available back (though it's not strictly needed since we iterate sorted items)
            availableInventory[itemId] = available;
        }

        return exceptions.Concat(recommendations)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.ReleaseDate)
            .ThenBy(x => x.ItemId)
            .ToArray();
    }

    private static void ValidateGrossRequirements(IReadOnlyCollection<GrossRequirement> grossRequirements)
    {
        foreach (var requirement in grossRequirements)
        {
            if (requirement.ItemId == Guid.Empty)
            {
                throw new ArgumentException("Gross requirement item id cannot be empty.", nameof(grossRequirements));
            }

            if (requirement.Quantity < 0)
            {
                throw new ArgumentException("Gross requirement quantity must be non-negative.", nameof(grossRequirements));
            }
        }
    }

    private static Dictionary<Guid, int>? CalculateLowLevelCodes(MrpPlanningSnapshot snapshot, out HashSet<Guid> cycledItems)
    {
        cycledItems = new HashSet<Guid>();
        var llc = snapshot.Items.Keys.ToDictionary(id => id, id => 0);
        bool changed = true;
        int maxIterations = snapshot.Items.Count;
        int iteration = 0;

        while (changed && iteration < maxIterations + 1)
        {
            changed = false;
            iteration++;

            foreach (var parentId in snapshot.Items.Keys)
            {
                if (snapshot.BomLinesByParentItem.TryGetValue(parentId, out var lines))
                {
                    var parentLlc = llc[parentId];
                    foreach (var line in lines)
                    {
                        if (llc.TryGetValue(line.ComponentItemId, out var compLlc))
                        {
                            if (compLlc <= parentLlc)
                            {
                                llc[line.ComponentItemId] = parentLlc + 1;
                                changed = true;
                            }
                        }
                    }
                }
            }
        }

        if (changed)
        {
            // Cycle detected
            foreach (var id in snapshot.Items.Keys)
            {
                cycledItems.Add(id);
            }
            return null;
        }

        return llc;
    }

    private static void ExplodeToComponents(
        Guid parentId,
        decimal parentPlannedQty,
        DateOnly parentReleaseDate,
        MrpPlanningSnapshot snapshot,
        Dictionary<Guid, List<GrossRequirement>> grossReqs,
        List<MrpRecommendation> exceptions,
        DateOnly originalDueDate)
    {
        if (!snapshot.BomLinesByParentItem.TryGetValue(parentId, out var lines) || lines.Count == 0)
        {
            if (snapshot.Items.TryGetValue(parentId, out var parentItem) && parentItem.Type == MrpItemType.Manufactured)
            {
                exceptions.Add(new MrpRecommendation(
                    parentId,
                    parentPlannedQty,
                    parentReleaseDate,
                    originalDueDate,
                    "Exception",
                    "Manufactured item has no active BOM."));
            }
            return;
        }

        foreach (var line in lines)
        {
            var scrapMultiplier = 1m + line.ScrapFactor;
            var componentQuantity = parentPlannedQty * line.QuantityPer * scrapMultiplier;
            if (componentQuantity <= 0)
            {
                continue;
            }

            if (!grossReqs.TryGetValue(line.ComponentItemId, out var list))
            {
                list = new List<GrossRequirement>();
                grossReqs[line.ComponentItemId] = list;
            }

            list.Add(new GrossRequirement(line.ComponentItemId, componentQuantity, parentReleaseDate));
        }
    }

    private static DateOnly CalculateReleaseDate(DateOnly dueDate, MrpItemSnapshot item, MrpPlanningSnapshot snapshot)
    {
        if (!snapshot.LeadTimesByItem.TryGetValue(item.ItemId, out var leadTime))
        {
            return dueDate;
        }

        var planningLeadTime = item.Type switch
        {
            MrpItemType.Purchased => leadTime.ProcurementLeadTimeDays,
            MrpItemType.Manufactured => leadTime.ManufacturingLeadTimeDays,
            _ => 0
        };

        return dueDate.AddDays(-(planningLeadTime + leadTime.SafetyLeadTimeDays));
    }
}
