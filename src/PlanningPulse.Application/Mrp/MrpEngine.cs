namespace PlanningPulse.Application.Mrp;

public sealed class MrpEngine(IMrpPlanningDataProvider planningDataProvider) : IMrpEngine
{
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

        var snapshot = await planningDataProvider.GetPlanningSnapshotAsync(
            grossRequirements.Select(x => x.ItemId).Distinct().ToArray(),
            cancellationToken);

        var explodedRequirements = new List<GrossRequirement>();
        var exceptions = new List<MrpRecommendation>();

        foreach (var requirement in grossRequirements)
        {
            ExplodeRequirement(requirement, snapshot, explodedRequirements, exceptions, []);
        }

        var recommendations = NetAndRecommend(explodedRequirements, snapshot, lotSizingMethod);
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

            if (requirement.Quantity <= 0)
            {
                throw new ArgumentException("Gross requirement quantity must be greater than zero.", nameof(grossRequirements));
            }
        }
    }

    private static void ExplodeRequirement(
        GrossRequirement requirement,
        MrpPlanningSnapshot snapshot,
        List<GrossRequirement> explodedRequirements,
        List<MrpRecommendation> exceptions,
        HashSet<Guid> path)
    {
        explodedRequirements.Add(requirement);

        if (!snapshot.Items.TryGetValue(requirement.ItemId, out var item))
        {
            exceptions.Add(new MrpRecommendation(
                requirement.ItemId,
                requirement.Quantity,
                requirement.RequiredDate,
                requirement.RequiredDate,
                "Exception",
                "Item is missing from the planning snapshot."));
            return;
        }

        if (!path.Add(requirement.ItemId))
        {
            exceptions.Add(new MrpRecommendation(
                requirement.ItemId,
                requirement.Quantity,
                requirement.RequiredDate,
                requirement.RequiredDate,
                "Exception",
                "BOM cycle detected."));
            return;
        }

        if (item.Type == MrpItemType.Purchased)
        {
            path.Remove(requirement.ItemId);
            return;
        }

        if (!snapshot.BomLinesByParentItem.TryGetValue(requirement.ItemId, out var lines) || lines.Count == 0)
        {
            if (item.Type == MrpItemType.Manufactured)
            {
                exceptions.Add(new MrpRecommendation(
                    requirement.ItemId,
                    requirement.Quantity,
                    CalculateReleaseDate(requirement.RequiredDate, item, snapshot),
                    requirement.RequiredDate,
                    "Exception",
                    "Manufactured item has no active BOM."));
            }

            path.Remove(requirement.ItemId);
            return;
        }

        var parentReleaseDate = CalculateReleaseDate(requirement.RequiredDate, item, snapshot);
        foreach (var line in lines)
        {
            var scrapMultiplier = 1m + line.ScrapFactor;
            var componentQuantity = requirement.Quantity * line.QuantityPer * scrapMultiplier;
            if (componentQuantity <= 0)
            {
                continue;
            }

            ExplodeRequirement(
                new GrossRequirement(line.ComponentItemId, componentQuantity, parentReleaseDate),
                snapshot,
                explodedRequirements,
                exceptions,
                path);
        }

        path.Remove(requirement.ItemId);
    }

    private static IReadOnlyCollection<MrpRecommendation> NetAndRecommend(
        IReadOnlyCollection<GrossRequirement> explodedRequirements,
        MrpPlanningSnapshot snapshot,
        LotSizingMethod lotSizingMethod)
    {
        var recommendations = new List<MrpRecommendation>();
        var availableByItem = snapshot.InventoryByItem.ToDictionary(
            x => x.Key,
            x => x.Value.OnHandQuantity - x.Value.AllocatedQuantity + x.Value.OnOrderQuantity);

        foreach (var itemGroup in explodedRequirements
                     .GroupBy(x => x.ItemId)
                     .OrderBy(x => x.Key))
        {
            availableByItem.TryGetValue(itemGroup.Key, out var available);

            foreach (var bucket in itemGroup
                         .GroupBy(x => x.RequiredDate)
                         .OrderBy(x => x.Key))
            {
                var grossQuantity = bucket.Sum(x => x.Quantity);
                var netQuantity = Math.Max(0m, grossQuantity - available);
                available = Math.Max(0m, available - grossQuantity);

                if (netQuantity <= 0)
                {
                    continue;
                }

                if (!snapshot.Items.TryGetValue(itemGroup.Key, out var item))
                {
                    recommendations.Add(new MrpRecommendation(
                        itemGroup.Key,
                        netQuantity,
                        bucket.Key,
                        bucket.Key,
                        "Exception",
                        "Cannot create supply recommendation because item is missing."));
                    continue;
                }

                var plannedQuantity = ApplyLotSizing(netQuantity, item, lotSizingMethod);
                available += Math.Max(0m, plannedQuantity - netQuantity);
                var releaseDate = CalculateReleaseDate(bucket.Key, item, snapshot);
                var recommendationType = item.Type == MrpItemType.Purchased ? "Purchase" : "Production";

                recommendations.Add(new MrpRecommendation(
                    item.ItemId,
                    plannedQuantity,
                    releaseDate,
                    bucket.Key,
                    recommendationType,
                    BuildRecommendationReason(lotSizingMethod, netQuantity, plannedQuantity, item)));
            }
        }

        return recommendations;
    }

    private static decimal ApplyLotSizing(decimal netQuantity, MrpItemSnapshot item, LotSizingMethod lotSizingMethod)
    {
        return lotSizingMethod switch
        {
            LotSizingMethod.LotForLot => netQuantity,
            LotSizingMethod.MinMax => ApplyMinMax(netQuantity, item),
            LotSizingMethod.EconomicOrderQuantity => ApplyEconomicOrderQuantity(netQuantity, item),
            _ => netQuantity
        };
    }

    private static decimal ApplyMinMax(decimal netQuantity, MrpItemSnapshot item)
    {
        var plannedQuantity = netQuantity;

        if (item.MaximumInventoryQuantity.HasValue && item.MaximumInventoryQuantity.Value > plannedQuantity)
        {
            plannedQuantity = item.MaximumInventoryQuantity.Value;
        }

        if (item.MinimumOrderQuantity.HasValue && plannedQuantity < item.MinimumOrderQuantity.Value)
        {
            plannedQuantity = item.MinimumOrderQuantity.Value;
        }

        return plannedQuantity;
    }

    private static decimal ApplyEconomicOrderQuantity(decimal netQuantity, MrpItemSnapshot item)
    {
        if (!item.EconomicOrderQuantity.HasValue || item.EconomicOrderQuantity.Value <= 0)
        {
            return netQuantity;
        }

        var eoq = item.EconomicOrderQuantity.Value;
        return Math.Ceiling(netQuantity / eoq) * eoq;
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

    private static string BuildRecommendationReason(
        LotSizingMethod method,
        decimal netQuantity,
        decimal plannedQuantity,
        MrpItemSnapshot item)
    {
        var leadTimeReason = $"Net requirement for {item.ItemNumber}.";
        return method switch
        {
            LotSizingMethod.LotForLot => $"{leadTimeReason} Lot-for-lot quantity equals net requirement.",
            LotSizingMethod.MinMax when plannedQuantity != netQuantity => $"{leadTimeReason} Quantity adjusted by min/max planning policy.",
            LotSizingMethod.EconomicOrderQuantity when plannedQuantity != netQuantity => $"{leadTimeReason} Quantity rounded to EOQ multiple.",
            _ => leadTimeReason
        };
    }
}
