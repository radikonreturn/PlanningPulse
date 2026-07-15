using System;
using System.Collections.Generic;

namespace PlanningPulse.Application.Common.Validation;

public static class EntityValidator
{
    public static List<string> ValidateItem(
        string itemNumber,
        string name,
        string uom,
        string typeStr,
        decimal safetyStock,
        int leadTimeDays)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(itemNumber))
        {
            errors.Add("Item Number is required.");
        }
        else if (itemNumber.Length > 80)
        {
            errors.Add("Item Number must not exceed 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Item Name is required.");
        }
        else if (name.Length > 200)
        {
            errors.Add("Item Name must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(uom))
        {
            errors.Add("Unit of Measure is required.");
        }
        else if (uom.Length > 20)
        {
            errors.Add("Unit of Measure must not exceed 20 characters.");
        }

        // Try to parse ItemType
        var typeParsed = false;
        foreach (var nameVal in Enum.GetNames(typeof(Domain.Items.ItemType)))
        {
            if (string.Equals(nameVal, typeStr, StringComparison.OrdinalIgnoreCase))
            {
                typeParsed = true;
                break;
            }
        }

        if (!typeParsed)
        {
            errors.Add($"Invalid Item Type '{typeStr}'. Must be Purchased, Manufactured, or Phantom.");
        }

        if (safetyStock < 0)
        {
            errors.Add("Safety Stock must be non-negative.");
        }

        if (leadTimeDays < 0)
        {
            errors.Add("Lead Time must be non-negative.");
        }

        return errors;
    }

    public static List<string> ValidateBomLine(
        string parentItemNumber,
        string componentItemNumber,
        decimal quantityPer,
        decimal scrapFactor,
        string revision)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(parentItemNumber))
        {
            errors.Add("Parent Item Number is required.");
        }

        if (string.IsNullOrWhiteSpace(componentItemNumber))
        {
            errors.Add("Component Item Number is required.");
        }

        if (string.IsNullOrWhiteSpace(revision))
        {
            errors.Add("Revision is required.");
        }
        else if (revision.Length > 40)
        {
            errors.Add("Revision must not exceed 40 characters.");
        }

        if (quantityPer <= 0)
        {
            errors.Add("Quantity Per must be greater than zero.");
        }

        if (scrapFactor < 0 || scrapFactor > 1)
        {
            errors.Add("Scrap Factor must be between 0 and 1 (inclusive).");
        }

        return errors;
    }

    public static List<string> ValidateInventoryLevel(
        string itemNumber,
        string locationCode,
        decimal onHand,
        decimal allocated,
        decimal onOrder)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(itemNumber))
        {
            errors.Add("Item Number is required.");
        }

        if (string.IsNullOrWhiteSpace(locationCode))
        {
            errors.Add("Location Code is required.");
        }
        else if (locationCode.Length > 80)
        {
            errors.Add("Location Code must not exceed 80 characters.");
        }

        if (onHand < 0)
        {
            errors.Add("On Hand quantity must be non-negative.");
        }

        if (allocated < 0)
        {
            errors.Add("Allocated quantity must be non-negative.");
        }

        if (onOrder < 0)
        {
            errors.Add("On Order quantity must be non-negative.");
        }

        return errors;
    }
}
