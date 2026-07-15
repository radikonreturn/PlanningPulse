using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Mrp;
using PlanningPulse.Domain.Items;
using PlanningPulse.Infrastructure.Persistence;

namespace PlanningPulse.Infrastructure.Mrp;

public sealed class EfMrpPlanningDataProvider(PlanningPulseDbContext dbContext) : IMrpPlanningDataProvider
{
    public async Task<MrpPlanningSnapshot> GetPlanningSnapshotAsync(
        IReadOnlyCollection<Guid> rootItemIds,
        CancellationToken cancellationToken)
    {
        var discoveredItemIds = rootItemIds.Where(x => x != Guid.Empty).ToHashSet();
        var bomLinesByParent = new Dictionary<Guid, IReadOnlyCollection<MrpBomLineSnapshot>>();
        var frontier = discoveredItemIds.ToHashSet();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        while (frontier.Count > 0)
        {
            var currentParents = frontier.ToArray();
            frontier.Clear();

            var activeBoms = await dbContext.Boms
                .AsNoTracking()
                .Where(x =>
                    currentParents.Contains(x.ParentItemId) &&
                    x.IsActive &&
                    x.EffectiveFrom <= today &&
                    (x.EffectiveTo == null || x.EffectiveTo >= today))
                .Select(x => new
                {
                    x.Id,
                    x.ParentItemId,
                    x.EffectiveFrom
                })
                .ToListAsync(cancellationToken);

            var selectedBomIds = activeBoms
                .GroupBy(x => x.ParentItemId)
                .Select(x => x.OrderByDescending(b => b.EffectiveFrom).ThenBy(b => b.Id).First())
                .ToDictionary(x => x.ParentItemId, x => x.Id);

            if (selectedBomIds.Count == 0)
            {
                continue;
            }

            var bomIds = selectedBomIds.Values.ToArray();
            var bomLines = await dbContext.BomLines
                .AsNoTracking()
                .Where(x => bomIds.Contains(x.BomId))
                .Select(x => new
                {
                    x.Bom.ParentItemId,
                    x.ComponentItemId,
                    x.QuantityPer,
                    x.ScrapFactor
                })
                .ToListAsync(cancellationToken);

            foreach (var parentGroup in bomLines.GroupBy(x => x.ParentItemId))
            {
                var lines = parentGroup
                    .Select(x => new MrpBomLineSnapshot(x.ParentItemId, x.ComponentItemId, x.QuantityPer, x.ScrapFactor))
                    .ToArray();

                bomLinesByParent[parentGroup.Key] = lines;

                foreach (var componentItemId in lines.Select(x => x.ComponentItemId))
                {
                    if (discoveredItemIds.Add(componentItemId))
                    {
                        frontier.Add(componentItemId);
                    }
                }
            }
        }

        var allItemIds = discoveredItemIds.ToArray();

        var itemRows = await dbContext.Items
            .AsNoTracking()
            .Where(x => allItemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ItemNumber,
                x.Name,
                x.Type,
                x.MinimumOrderQuantity,
                x.MaximumInventoryQuantity,
                x.EconomicOrderQuantity,
                x.SafetyStockQuantity
            })
            .ToListAsync(cancellationToken);

        var items = itemRows
            .Select(x => new MrpItemSnapshot(
                x.Id,
                x.ItemNumber,
                x.Name,
                MapItemType(x.Type),
                x.MinimumOrderQuantity,
                x.MaximumInventoryQuantity,
                x.EconomicOrderQuantity,
                x.SafetyStockQuantity))
            .ToDictionary(x => x.ItemId);

        var inventoryLevels = await dbContext.InventoryLevels
            .AsNoTracking()
            .Where(x => allItemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        var inventory = inventoryLevels
            .GroupBy(x => x.ItemId)
            .Select(x => new MrpInventorySnapshot(
                x.Key,
                x.Sum(i => i.OnHandQuantity),
                x.Sum(i => i.AllocatedQuantity),
                x.Sum(i => i.OnOrderQuantity)))
            .ToDictionary(x => x.ItemId);

        var leadTimeRows = await dbContext.LeadTimes
            .AsNoTracking()
            .Where(x => allItemIds.Contains(x.ItemId))
            .Select(x => new
            {
                x.ItemId,
                x.SupplierId,
                x.ProcurementLeadTimeDays,
                x.ManufacturingLeadTimeDays,
                x.SafetyLeadTimeDays
            })
            .ToListAsync(cancellationToken);

        var leadTimes = leadTimeRows
            .GroupBy(x => x.ItemId)
            .Select(x => x
                .OrderBy(lt => lt.SupplierId == null ? 0 : 1)
                .Select(lt => new MrpLeadTimeSnapshot(
                    lt.ItemId,
                    lt.ProcurementLeadTimeDays,
                    lt.ManufacturingLeadTimeDays,
                    lt.SafetyLeadTimeDays))
                .First())
            .ToDictionary(x => x.ItemId);

        return new MrpPlanningSnapshot(items, bomLinesByParent, inventory, leadTimes);
    }

    private static MrpItemType MapItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Purchased => MrpItemType.Purchased,
            ItemType.Manufactured => MrpItemType.Manufactured,
            ItemType.Phantom => MrpItemType.Phantom,
            _ => MrpItemType.Purchased
        };
    }
}
