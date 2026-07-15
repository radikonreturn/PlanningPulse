using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Common.Validation;
using PlanningPulse.Application.Import;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Boms;
using PlanningPulse.Domain.Inventory;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Suppliers;
using PlanningPulse.Infrastructure.Persistence;

namespace PlanningPulse.Infrastructure.Import;

public sealed class ImportService : IImportService
{
    private readonly PlanningPulseDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;

    public ImportService(PlanningPulseDbContext dbContext, ICurrentTenant currentTenant)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
    }

    public async Task<ImportResult> ImportItemsAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
    {
        var parsedRows = ParseFile(fileStream, fileName);
        if (parsedRows.Count < 2)
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, "File is empty or contains no data rows.") });
        }

        var headers = parsedRows[0];
        // Expected headers: ItemNumber, Name, Description, Type, UnitOfMeasure, SafetyStockQuantity, LeadTimeDays
        var expectedHeaders = new[] { "ItemNumber", "Name", "Description", "Type", "UnitOfMeasure", "SafetyStockQuantity", "LeadTimeDays" };
        if (!ValidateHeaders(headers, expectedHeaders, out var headerError))
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(1, headerError) });
        }

        var errors = new List<ImportRowError>();
        var seenItemNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Preload items and tenants
        var existingItems = await _dbContext.Items
            .Where(x => x.TenantId == (_currentTenant.TenantId ?? Guid.Empty))
            .ToDictionaryAsync(x => x.ItemNumber, x => x, StringComparer.OrdinalIgnoreCase);

        var existingLeadTimes = await _dbContext.LeadTimes
            .Include(x => x.Item)
            .Where(x => x.TenantId == (_currentTenant.TenantId ?? Guid.Empty))
            .ToDictionaryAsync(x => x.Item.ItemNumber, x => x, StringComparer.OrdinalIgnoreCase);

        var itemsToCreate = new List<Item>();
        var itemsToUpdate = new List<Item>();
        var leadTimesToCreate = new List<LeadTime>();

        for (int r = 1; r < parsedRows.Count; r++)
        {
            var row = parsedRows[r];
            int excelRowNumber = r + 1;

            if (row.Length < expectedHeaders.Length)
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Row has {row.Length} columns, expected at least {expectedHeaders.Length}."));
                continue;
            }

            var itemNumber = row[0]?.Trim() ?? string.Empty;
            var name = row[1]?.Trim() ?? string.Empty;
            var description = row[2]?.Trim() ?? string.Empty;
            var typeStr = row[3]?.Trim() ?? string.Empty;
            var uom = row[4]?.Trim() ?? string.Empty;
            var safetyStockStr = row[5]?.Trim() ?? "0";
            var leadTimeDaysStr = row[6]?.Trim() ?? "0";

            if (!decimal.TryParse(safetyStockStr, out var safetyStock))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"SafetyStockQuantity '{safetyStockStr}' is not a valid decimal number."));
                continue;
            }

            if (!int.TryParse(leadTimeDaysStr, out var leadTimeDays))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"LeadTimeDays '{leadTimeDaysStr}' is not a valid integer."));
                continue;
            }

            var rowValidationErrors = EntityValidator.ValidateItem(itemNumber, name, uom, typeStr, safetyStock, leadTimeDays);
            if (rowValidationErrors.Count > 0)
            {
                errors.AddRange(rowValidationErrors.Select(err => new ImportRowError(excelRowNumber, err)));
                continue;
            }

            if (!seenItemNumbers.Add(itemNumber))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Duplicate Item Number '{itemNumber}' in upload file."));
                continue;
            }

            Enum.TryParse<ItemType>(typeStr, true, out var itemType);

            if (existingItems.TryGetValue(itemNumber, out var existingItem))
            {
                existingItem.Name = name;
                existingItem.Description = description;
                existingItem.Type = itemType;
                existingItem.UnitOfMeasure = uom;
                existingItem.SafetyStockQuantity = safetyStock;
                existingItem.UpdatedAtUtc = DateTime.UtcNow;

                itemsToUpdate.Add(existingItem);

                // Update lead time if exists
                if (existingLeadTimes.TryGetValue(itemNumber, out var existingLt))
                {
                    if (itemType == ItemType.Purchased)
                    {
                        existingLt.ProcurementLeadTimeDays = leadTimeDays;
                        existingLt.ManufacturingLeadTimeDays = 0;
                    }
                    else
                    {
                        existingLt.ProcurementLeadTimeDays = 0;
                        existingLt.ManufacturingLeadTimeDays = leadTimeDays;
                    }
                    existingLt.UpdatedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    var newLt = new LeadTime
                    {
                        ItemId = existingItem.Id,
                        ProcurementLeadTimeDays = itemType == ItemType.Purchased ? leadTimeDays : 0,
                        ManufacturingLeadTimeDays = itemType != ItemType.Purchased ? leadTimeDays : 0,
                        SafetyLeadTimeDays = 0
                    };
                    leadTimesToCreate.Add(newLt);
                }
            }
            else
            {
                var newItem = new Item
                {
                    Id = Guid.NewGuid(),
                    ItemNumber = itemNumber,
                    Name = name,
                    Description = description,
                    Type = itemType,
                    UnitOfMeasure = uom,
                    SafetyStockQuantity = safetyStock,
                    IsActive = true
                };

                itemsToCreate.Add(newItem);

                var newLt = new LeadTime
                {
                    ItemId = newItem.Id,
                    ProcurementLeadTimeDays = itemType == ItemType.Purchased ? leadTimeDays : 0,
                    ManufacturingLeadTimeDays = itemType != ItemType.Purchased ? leadTimeDays : 0,
                    SafetyLeadTimeDays = 0
                };
                leadTimesToCreate.Add(newLt);
            }
        }

        if (errors.Count > 0)
        {
            return new ImportResult(false, 0, 0, errors);
        }

        // Apply changes in a transaction
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (itemsToCreate.Count > 0)
            {
                await _dbContext.Items.AddRangeAsync(itemsToCreate, cancellationToken);
            }
            if (leadTimesToCreate.Count > 0)
            {
                await _dbContext.LeadTimes.AddRangeAsync(leadTimesToCreate, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ImportResult(true, itemsToCreate.Count, itemsToUpdate.Count, Array.Empty<ImportRowError>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, $"Database error: {ex.Message}") });
        }
    }

    public async Task<ImportResult> ImportBomsAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
    {
        var parsedRows = ParseFile(fileStream, fileName);
        if (parsedRows.Count < 2)
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, "File is empty or contains no data rows.") });
        }

        var headers = parsedRows[0];
        // Expected headers: ParentItemNumber, ComponentItemNumber, QuantityPer, ScrapFactor, Revision, EffectiveFrom
        var expectedHeaders = new[] { "ParentItemNumber", "ComponentItemNumber", "QuantityPer", "ScrapFactor", "Revision", "EffectiveFrom" };
        if (!ValidateHeaders(headers, expectedHeaders, out var headerError))
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(1, headerError) });
        }

        var errors = new List<ImportRowError>();

        // Preload items
        var items = await _dbContext.Items
            .Where(x => x.TenantId == (_currentTenant.TenantId ?? Guid.Empty))
            .ToDictionaryAsync(x => x.ItemNumber, x => x, StringComparer.OrdinalIgnoreCase);

        var bomLinesToCreate = new List<BomLine>();
        var parentBoms = new Dictionary<(string ParentNumber, string Revision), (Guid ParentId, DateOnly EffectiveFrom)>();
        var groupedLinesByParentRev = new Dictionary<(string ParentNumber, string Revision), List<(string ComponentNumber, decimal Qty, decimal Scrap)>>();

        for (int r = 1; r < parsedRows.Count; r++)
        {
            var row = parsedRows[r];
            int excelRowNumber = r + 1;

            if (row.Length < expectedHeaders.Length)
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Row has {row.Length} columns, expected at least {expectedHeaders.Length}."));
                continue;
            }

            var parentItemNumber = row[0]?.Trim() ?? string.Empty;
            var componentItemNumber = row[1]?.Trim() ?? string.Empty;
            var qtyStr = row[2]?.Trim() ?? "0";
            var scrapStr = row[3]?.Trim() ?? "0";
            var revision = row[4]?.Trim() ?? string.Empty;
            var effectiveFromStr = row[5]?.Trim() ?? string.Empty;

            if (!decimal.TryParse(qtyStr, out var quantityPer))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"QuantityPer '{qtyStr}' is not a valid decimal number."));
                continue;
            }

            if (!decimal.TryParse(scrapStr, out var scrapFactor))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"ScrapFactor '{scrapStr}' is not a valid decimal number."));
                continue;
            }

            if (!DateOnly.TryParse(effectiveFromStr, out var effectiveFrom))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"EffectiveFrom '{effectiveFromStr}' is not a valid date (YYYY-MM-DD)."));
                continue;
            }

            var rowValidationErrors = EntityValidator.ValidateBomLine(parentItemNumber, componentItemNumber, quantityPer, scrapFactor, revision);
            if (rowValidationErrors.Count > 0)
            {
                errors.AddRange(rowValidationErrors.Select(err => new ImportRowError(excelRowNumber, err)));
                continue;
            }

            // Verify items exist
            if (!items.TryGetValue(parentItemNumber, out var parentItem))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Parent Item '{parentItemNumber}' does not exist in the database."));
                continue;
            }

            if (!items.TryGetValue(componentItemNumber, out var componentItem))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Component Item '{componentItemNumber}' does not exist in the database."));
                continue;
            }

            if (string.Equals(parentItemNumber, componentItemNumber, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ImportRowError(excelRowNumber, "A parent item cannot have itself as a component."));
                continue;
            }

            var key = (parentItemNumber, revision);
            if (!parentBoms.ContainsKey(key))
            {
                parentBoms[key] = (parentItem.Id, effectiveFrom);
            }
            if (!groupedLinesByParentRev.ContainsKey(key))
            {
                groupedLinesByParentRev[key] = new List<(string ComponentNumber, decimal Qty, decimal Scrap)>();
            }

            // Check for duplicates within the same BOM Revision
            if (groupedLinesByParentRev[key].Any(x => string.Equals(x.ComponentNumber, componentItemNumber, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Component '{componentItemNumber}' is specified multiple times in BOM for parent '{parentItemNumber}' Revision '{revision}'."));
                continue;
            }

            groupedLinesByParentRev[key].Add((componentItemNumber, quantityPer, scrapFactor));
        }

        // BOM Cycle Detection Check
        if (errors.Count == 0)
        {
            // Build the proposed adjacency list to verify cycles
            var adjList = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // 1. Get existing active BOM lines from DB
            var existingBoms = await _dbContext.Boms
                .Include(b => b.ParentItem)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.ComponentItem)
                .Where(b => b.TenantId == (_currentTenant.TenantId ?? Guid.Empty) && b.IsActive)
                .ToListAsync();

            foreach (var b in existingBoms)
            {
                adjList[b.ParentItem.ItemNumber] = b.Lines.Select(l => l.ComponentItem.ItemNumber).ToList();
            }

            // 2. Overlay the proposed BOM changes
            foreach (var group in groupedLinesByParentRev)
            {
                adjList[group.Key.ParentNumber] = group.Value.Select(x => x.ComponentNumber).ToList();
            }

            // 3. Run cycle detection (Tarjan or simple DFS)
            var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var recStack = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            bool HasCycle(string node)
            {
                if (recStack.GetValueOrDefault(node)) return true;
                if (visited.GetValueOrDefault(node)) return false;

                visited[node] = true;
                recStack[node] = true;

                if (adjList.TryGetValue(node, out var children))
                {
                    foreach (var child in children)
                    {
                        if (HasCycle(child)) return true;
                    }
                }

                recStack[node] = false;
                return false;
            }

            foreach (var parent in adjList.Keys)
            {
                if (HasCycle(parent))
                {
                    errors.Add(new ImportRowError(0, $"Circular dependency detected! Uploading these BOM definitions creates a cycle containing item '{parent}'."));
                    break;
                }
            }
        }

        if (errors.Count > 0)
        {
            return new ImportResult(false, 0, 0, errors);
        }

        // Apply changes in a transaction
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        int createdBomsCount = 0;
        int updatedBomsCount = 0;

        try
        {
            foreach (var group in groupedLinesByParentRev)
            {
                var parentItemNumber = group.Key.ParentNumber;
                var revision = group.Key.Revision;
                var (parentId, effectiveFrom) = parentBoms[group.Key];

                // Check if this BOM revision already exists
                var existingBom = await _dbContext.Boms
                    .Include(b => b.Lines)
                    .FirstOrDefaultAsync(b => b.ParentItemId == parentId && b.Revision == revision && b.TenantId == (_currentTenant.TenantId ?? Guid.Empty), cancellationToken);

                if (existingBom != null)
                {
                    // Update: clear existing lines and re-add
                    _dbContext.BomLines.RemoveRange(existingBom.Lines);
                    existingBom.EffectiveFrom = effectiveFrom;
                    existingBom.IsActive = true;
                    existingBom.UpdatedAtUtc = DateTime.UtcNow;

                    foreach (var lineData in group.Value)
                    {
                        var compItem = items[lineData.ComponentNumber];
                        existingBom.Lines.Add(new BomLine
                        {
                            ComponentItemId = compItem.Id,
                            QuantityPer = lineData.Qty,
                            ScrapFactor = lineData.Scrap
                        });
                    }
                    updatedBomsCount++;
                }
                else
                {
                    // Create new BOM
                    var newBom = new Bom
                    {
                        ParentItemId = parentId,
                        Revision = revision,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    };

                    foreach (var lineData in group.Value)
                    {
                        var compItem = items[lineData.ComponentNumber];
                        newBom.Lines.Add(new BomLine
                        {
                            ComponentItemId = compItem.Id,
                            QuantityPer = lineData.Qty,
                            ScrapFactor = lineData.Scrap
                        });
                    }

                    await _dbContext.Boms.AddAsync(newBom, cancellationToken);
                    createdBomsCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ImportResult(true, createdBomsCount, updatedBomsCount, Array.Empty<ImportRowError>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, $"Database error: {ex.Message}") });
        }
    }

    public async Task<ImportResult> ImportInventoryAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
    {
        var parsedRows = ParseFile(fileStream, fileName);
        if (parsedRows.Count < 2)
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, "File is empty or contains no data rows.") });
        }

        var headers = parsedRows[0];
        // Expected headers: ItemNumber, LocationCode, OnHandQuantity, AllocatedQuantity, OnOrderQuantity
        var expectedHeaders = new[] { "ItemNumber", "LocationCode", "OnHandQuantity", "AllocatedQuantity", "OnOrderQuantity" };
        if (!ValidateHeaders(headers, expectedHeaders, out var headerError))
        {
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(1, headerError) });
        }

        var errors = new List<ImportRowError>();
        var seenKeys = new HashSet<(string ItemNumber, string LocationCode)>();

        // Preload items and inventory
        var items = await _dbContext.Items
            .Where(x => x.TenantId == (_currentTenant.TenantId ?? Guid.Empty))
            .ToDictionaryAsync(x => x.ItemNumber, x => x, StringComparer.OrdinalIgnoreCase);

        var existingInventory = await _dbContext.InventoryLevels
            .Include(x => x.Item)
            .Where(x => x.TenantId == (_currentTenant.TenantId ?? Guid.Empty))
            .ToListAsync();

        var invMap = new Dictionary<(string ItemNumber, string LocationCode), InventoryLevel>();
        foreach (var inv in existingInventory)
        {
            var key = (inv.Item.ItemNumber ?? string.Empty, inv.LocationCode ?? string.Empty);
            invMap[key] = inv;
        }

        var invToCreate = new List<InventoryLevel>();
        var invToUpdate = new List<InventoryLevel>();

        for (int r = 1; r < parsedRows.Count; r++)
        {
            var row = parsedRows[r];
            int excelRowNumber = r + 1;

            if (row.Length < expectedHeaders.Length)
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Row has {row.Length} columns, expected at least {expectedHeaders.Length}."));
                continue;
            }

            var itemNumber = row[0]?.Trim() ?? string.Empty;
            var locationCode = row[1]?.Trim() ?? string.Empty;
            var onHandStr = row[2]?.Trim() ?? "0";
            var allocatedStr = row[3]?.Trim() ?? "0";
            var onOrderStr = row[4]?.Trim() ?? "0";

            if (!decimal.TryParse(onHandStr, out var onHand))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"OnHandQuantity '{onHandStr}' is not a valid decimal number."));
                continue;
            }

            if (!decimal.TryParse(allocatedStr, out var allocated))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"AllocatedQuantity '{allocatedStr}' is not a valid decimal number."));
                continue;
            }

            if (!decimal.TryParse(onOrderStr, out var onOrder))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"OnOrderQuantity '{onOrderStr}' is not a valid decimal number."));
                continue;
            }

            var rowValidationErrors = EntityValidator.ValidateInventoryLevel(itemNumber, locationCode, onHand, allocated, onOrder);
            if (rowValidationErrors.Count > 0)
            {
                errors.AddRange(rowValidationErrors.Select(err => new ImportRowError(excelRowNumber, err)));
                continue;
            }

            if (!items.TryGetValue(itemNumber, out var item))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Item '{itemNumber}' does not exist in the database."));
                continue;
            }

            var key = (itemNumber, locationCode);
            if (!seenKeys.Add(key))
            {
                errors.Add(new ImportRowError(excelRowNumber, $"Duplicate inventory entry for item '{itemNumber}' at location '{locationCode}' in upload file."));
                continue;
            }

            if (invMap.TryGetValue(key, out var existingInv))
            {
                existingInv.OnHandQuantity = onHand;
                existingInv.AllocatedQuantity = allocated;
                existingInv.OnOrderQuantity = onOrder;
                existingInv.UpdatedAtUtc = DateTime.UtcNow;

                invToUpdate.Add(existingInv);
            }
            else
            {
                var newInv = new InventoryLevel
                {
                    ItemId = item.Id,
                    LocationCode = locationCode,
                    OnHandQuantity = onHand,
                    AllocatedQuantity = allocated,
                    OnOrderQuantity = onOrder
                };

                invToCreate.Add(newInv);
            }
        }

        if (errors.Count > 0)
        {
            return new ImportResult(false, 0, 0, errors);
        }

        // Apply changes in a transaction
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (invToCreate.Count > 0)
            {
                await _dbContext.InventoryLevels.AddRangeAsync(invToCreate, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ImportResult(true, invToCreate.Count, invToUpdate.Count, Array.Empty<ImportRowError>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ImportResult(false, 0, 0, new[] { new ImportRowError(0, $"Database error: {ex.Message}") });
        }
    }

    public Task<byte[]> GenerateItemTemplateAsync(string format)
    {
        var headers = new[] { "ItemNumber", "Name", "Description", "Type", "UnitOfMeasure", "SafetyStockQuantity", "LeadTimeDays" };
        var example = new[] { "FG-100", "Widget Alpha", "Premium finished goods", "Manufactured", "EA", "5", "5" };

        return Task.FromResult(GenerateTemplateBytes(headers, example, format));
    }

    public Task<byte[]> GenerateBomTemplateAsync(string format)
    {
        var headers = new[] { "ParentItemNumber", "ComponentItemNumber", "QuantityPer", "ScrapFactor", "Revision", "EffectiveFrom" };
        var example = new[] { "FG-100", "SA-100", "2", "0.05", "Rev A", "2026-07-15" };

        return Task.FromResult(GenerateTemplateBytes(headers, example, format));
    }

    public Task<byte[]> GenerateInventoryTemplateAsync(string format)
    {
        var headers = new[] { "ItemNumber", "LocationCode", "OnHandQuantity", "AllocatedQuantity", "OnOrderQuantity" };
        var example = new[] { "FG-100", "WH-01", "100", "10", "20" };

        return Task.FromResult(GenerateTemplateBytes(headers, example, format));
    }

    private static byte[] GenerateTemplateBytes(string[] headers, string[] example, string format)
    {
        var isExcel = string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase);

        if (isExcel)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Template");
            
            // Populate headers
            for (int col = 0; col < headers.Length; col++)
            {
                var cell = worksheet.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
            }

            // Populate example
            for (int col = 0; col < example.Length; col++)
            {
                worksheet.Cell(2, col + 1).Value = example[col];
            }

            worksheet.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));
            sb.AppendLine(string.Join(",", example.Select(x => x.Contains(',') ? $"\"{x}\"" : x)));

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }

    private static List<string[]> ParseFile(Stream fileStream, string fileName)
    {
        var isExcel = Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        if (isExcel)
        {
            return ParseExcel(fileStream);
        }
        else
        {
            return ParseCsv(fileStream);
        }
    }

    private static List<string[]> ParseExcel(Stream fileStream)
    {
        var rows = new List<string[]>();
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return rows;

        var range = worksheet.RangeUsed();
        if (range == null) return rows;

        var rowCount = range.RowCount();
        var colCount = range.ColumnCount();

        for (int r = 1; r <= rowCount; r++)
        {
            var rowValues = new string[colCount];
            for (int c = 1; c <= colCount; c++)
            {
                rowValues[c - 1] = worksheet.Cell(r, c).Value.ToString() ?? string.Empty;
            }
            if (rowValues.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                rows.Add(rowValues);
            }
        }
        return rows;
    }

    private static List<string[]> ParseCsv(Stream fileStream)
    {
        var rows = new List<string[]>();
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = SplitCsvLine(line);
            rows.Add(values);
        }
        return rows;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var token = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(token.ToString().Trim(' ', '"'));
                token.Clear();
            }
            else
            {
                token.Append(c);
            }
        }
        result.Add(token.ToString().Trim(' ', '"'));
        return result.ToArray();
    }

    private static bool ValidateHeaders(string[] fileHeaders, string[] expectedHeaders, out string error)
    {
        error = string.Empty;
        if (fileHeaders.Length < expectedHeaders.Length)
        {
            error = $"Uploaded file headers count is {fileHeaders.Length}, expected at least {expectedHeaders.Length}. Missing columns.";
            return false;
        }

        for (int i = 0; i < expectedHeaders.Length; i++)
        {
            var fileHeader = fileHeaders[i]?.Trim();
            var expected = expectedHeaders[i];
            if (!string.Equals(fileHeader, expected, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Header mismatch at column {i + 1}: expected '{expected}', found '{fileHeader}'.";
                return false;
            }
        }

        return true;
    }
}
