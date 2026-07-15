using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanningPulse.Application.Tenancy;
using PlanningPulse.Domain.Boms;
using PlanningPulse.Domain.Identity;
using PlanningPulse.Domain.Inventory;
using PlanningPulse.Domain.Items;
using PlanningPulse.Domain.Production;
using PlanningPulse.Domain.Routings;
using PlanningPulse.Domain.Suppliers;
using PlanningPulse.Domain.Tenancy;

namespace PlanningPulse.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(PlanningPulseDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher, ITenantSetter tenantSetter)
    {
        Tenant? tenant = null;

        // 1. Seed Tenant and User
        if (!await dbContext.Tenants.AnyAsync())
        {
            tenant = new Tenant { Name = "Demo Workspace", Slug = "demo" };
            var user = new ApplicationUser
            {
                Email = "admin@demo.com",
                NormalizedEmail = "ADMIN@DEMO.COM",
                DisplayName = "Admin"
            };
            user.PasswordHash = passwordHasher.HashPassword(user, "Admin123!");

            await dbContext.Tenants.AddAsync(tenant);
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();

            tenantSetter.SetTenant(tenant.Id);
            await dbContext.TenantUsers.AddAsync(new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = TenantRole.Admin
            });
            await dbContext.SaveChangesAsync();
        }
        else
        {
            tenant = await dbContext.Tenants.FirstAsync();
        }

        tenantSetter.SetTenant(tenant.Id);

        // 2. Seed Manufacturing Entities if they don't exist
        if (await dbContext.Items.AnyAsync())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 2.1 Items (Finished Goods, Sub-assemblies, and Raw Materials)
        var fg100 = new Item { ItemNumber = "FG-100", Name = "Widget Alpha", Description = "Premium electronic widget with SMT board and wire harness", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 5m, IsActive = true };
        var fg200 = new Item { ItemNumber = "FG-200", Name = "Widget Beta", Description = "Heavy-duty mechanical assembly with custom steel frame", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 8m, IsActive = true };
        var fg300 = new Item { ItemNumber = "FG-300", Name = "Widget Gamma", Description = "Pneumatic regulator assembly with steel frame and values", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 10m, IsActive = true };

        var sa100 = new Item { ItemNumber = "SA-100", Name = "Frame Assembly", Description = "Structural steel frame support sub-assembly", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 10m, IsActive = true };
        var sa200 = new Item { ItemNumber = "SA-200", Name = "PCB Assembly", Description = "Control board assembly with SMT components", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 15m, IsActive = true };
        var sa300 = new Item { ItemNumber = "SA-300", Name = "Harness Assembly", Description = "Internal wiring harness assembly", Type = ItemType.Manufactured, UnitOfMeasure = "EA", SafetyStockQuantity = 12m, IsActive = true };

        var rm100 = new Item { ItemNumber = "RM-100", Name = "Steel Bar", Description = "Raw solid structural steel bars", Type = ItemType.Purchased, UnitOfMeasure = "EA", SafetyStockQuantity = 100m, IsActive = true };
        var rm200 = new Item { ItemNumber = "RM-200", Name = "Screw Pack", Description = "Standard assembly fastening hardware screws pack", Type = ItemType.Purchased, UnitOfMeasure = "PK", SafetyStockQuantity = 500m, IsActive = true };
        var rm300 = new Item { ItemNumber = "RM-300", Name = "Microcontroller IC", Description = "System programmable 32-bit MCU chip", Type = ItemType.Purchased, UnitOfMeasure = "EA", SafetyStockQuantity = 150m, IsActive = true };
        var rm400 = new Item { ItemNumber = "RM-400", Name = "Resistor Pack", Description = "SMT resistor hardware pack", Type = ItemType.Purchased, UnitOfMeasure = "PK", SafetyStockQuantity = 1000m, IsActive = true };
        var rm500 = new Item { ItemNumber = "RM-500", Name = "Copper Wire Spool", Description = "Insulated solid copper wire roll", Type = ItemType.Purchased, UnitOfMeasure = "SP", SafetyStockQuantity = 80m, IsActive = true };
        var rm600 = new Item { ItemNumber = "RM-600", Name = "Pneumatic Valve", Description = "Air pressure control regulator valve", Type = ItemType.Purchased, UnitOfMeasure = "EA", SafetyStockQuantity = 50m, IsActive = true };
        var rm700 = new Item { ItemNumber = "RM-700", Name = "Plastic Housing", Description = "External outer protective plastic enclosure", Type = ItemType.Purchased, UnitOfMeasure = "EA", SafetyStockQuantity = 200m, IsActive = true };

        await dbContext.Items.AddRangeAsync(
            fg100, fg200, fg300, 
            sa100, sa200, sa300, 
            rm100, rm200, rm300, rm400, rm500, rm600, rm700
        );
        await dbContext.SaveChangesAsync();

        // 2.2 BOMs (Bills of Materials - 3 Levels Deep)
        // FG-100 BOM (SMT + Harness + Housing + Screws)
        var bomFg100 = new Bom { ParentItemId = fg100.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomFg100.Lines.Add(new BomLine { ComponentItemId = sa200.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg100.Lines.Add(new BomLine { ComponentItemId = sa300.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg100.Lines.Add(new BomLine { ComponentItemId = rm700.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg100.Lines.Add(new BomLine { ComponentItemId = rm200.Id, QuantityPer = 2m, ScrapFactor = 0.0m });

        // FG-200 BOM (Frame + Housing + Screws)
        var bomFg200 = new Bom { ParentItemId = fg200.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomFg200.Lines.Add(new BomLine { ComponentItemId = sa100.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg200.Lines.Add(new BomLine { ComponentItemId = rm700.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg200.Lines.Add(new BomLine { ComponentItemId = rm200.Id, QuantityPer = 4m, ScrapFactor = 0.0m });

        // FG-300 BOM (Frame + Valves + Screws)
        var bomFg300 = new Bom { ParentItemId = fg300.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomFg300.Lines.Add(new BomLine { ComponentItemId = sa100.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomFg300.Lines.Add(new BomLine { ComponentItemId = rm600.Id, QuantityPer = 2m, ScrapFactor = 0.0m });
        bomFg300.Lines.Add(new BomLine { ComponentItemId = rm200.Id, QuantityPer = 6m, ScrapFactor = 0.0m });

        // SA-100 BOM (3 Steel Bars with 10% Scrap)
        var bomSa100 = new Bom { ParentItemId = sa100.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomSa100.Lines.Add(new BomLine { ComponentItemId = rm100.Id, QuantityPer = 3m, ScrapFactor = 0.1m });

        // SA-200 BOM (SMT - MCU + Resistors)
        var bomSa200 = new Bom { ParentItemId = sa200.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomSa200.Lines.Add(new BomLine { ComponentItemId = rm300.Id, QuantityPer = 1m, ScrapFactor = 0.0m });
        bomSa200.Lines.Add(new BomLine { ComponentItemId = rm400.Id, QuantityPer = 5m, ScrapFactor = 0.0m });

        // SA-300 BOM (Wire Harness - 0.5 Copper Wire with 5% Scrap)
        var bomSa300 = new Bom { ParentItemId = sa300.Id, Revision = "Rev A", IsActive = true, EffectiveFrom = today.AddDays(-30) };
        bomSa300.Lines.Add(new BomLine { ComponentItemId = rm500.Id, QuantityPer = 0.5m, ScrapFactor = 0.05m });

        await dbContext.Boms.AddRangeAsync(bomFg100, bomFg200, bomFg300, bomSa100, bomSa200, bomSa300);
        await dbContext.SaveChangesAsync();

        // 2.3 Inventory Levels (Mix of healthy and deficit stocks)
        await dbContext.InventoryLevels.AddRangeAsync(
            new InventoryLevel { ItemId = fg100.Id, LocationCode = "WH-01", OnHandQuantity = 12m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = fg200.Id, LocationCode = "WH-01", OnHandQuantity = 4m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = fg300.Id, LocationCode = "WH-01", OnHandQuantity = 2m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = sa100.Id, LocationCode = "WH-01", OnHandQuantity = 8m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = sa200.Id, LocationCode = "WH-01", OnHandQuantity = 5m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = sa300.Id, LocationCode = "WH-01", OnHandQuantity = 20m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm100.Id, LocationCode = "WH-02", OnHandQuantity = 80m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm200.Id, LocationCode = "WH-02", OnHandQuantity = 400m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm300.Id, LocationCode = "WH-02", OnHandQuantity = 100m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm400.Id, LocationCode = "WH-02", OnHandQuantity = 800m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm500.Id, LocationCode = "WH-02", OnHandQuantity = 50m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm600.Id, LocationCode = "WH-02", OnHandQuantity = 15m, AllocatedQuantity = 0m, OnOrderQuantity = 0m },
            new InventoryLevel { ItemId = rm700.Id, LocationCode = "WH-02", OnHandQuantity = 120m, AllocatedQuantity = 0m, OnOrderQuantity = 0m }
        );
        await dbContext.SaveChangesAsync();

        // 2.4 Suppliers
        var supElectronics = new Supplier { SupplierNumber = "SUP-001", Name = "Global Electronics Corp", Email = "support@globalelec.com", IsActive = true };
        var supMetal = new Supplier { SupplierNumber = "SUP-002", Name = "Ironworks Inc", Email = "sales@ironworks.com", IsActive = true };
        var supHardware = new Supplier { SupplierNumber = "SUP-003", Name = "Fasteners R Us", Email = "orders@fastenersrus.com", IsActive = true };
        var supPneumatic = new Supplier { SupplierNumber = "SUP-004", Name = "Pneumatic Systems Ltd", Email = "sales@pneumaticsys.com", IsActive = true };

        await dbContext.Suppliers.AddRangeAsync(supElectronics, supMetal, supHardware, supPneumatic);
        await dbContext.SaveChangesAsync();

        // 2.5 Lead Times
        await dbContext.LeadTimes.AddRangeAsync(
            new LeadTime { ItemId = fg100.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 5, SafetyLeadTimeDays = 1 },
            new LeadTime { ItemId = fg200.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 6, SafetyLeadTimeDays = 1 },
            new LeadTime { ItemId = fg300.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 8, SafetyLeadTimeDays = 1 },
            new LeadTime { ItemId = sa100.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 3, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = sa200.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 4, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = sa300.Id, ProcurementLeadTimeDays = 0, ManufacturingLeadTimeDays = 2, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = rm100.Id, SupplierId = supMetal.Id, ProcurementLeadTimeDays = 5, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = rm200.Id, SupplierId = supHardware.Id, ProcurementLeadTimeDays = 2, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 1 },
            new LeadTime { ItemId = rm300.Id, SupplierId = supElectronics.Id, ProcurementLeadTimeDays = 10, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 2 },
            new LeadTime { ItemId = rm400.Id, SupplierId = supElectronics.Id, ProcurementLeadTimeDays = 3, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = rm500.Id, SupplierId = supElectronics.Id, ProcurementLeadTimeDays = 4, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 0 },
            new LeadTime { ItemId = rm600.Id, SupplierId = supPneumatic.Id, ProcurementLeadTimeDays = 7, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 1 },
            new LeadTime { ItemId = rm700.Id, SupplierId = supMetal.Id, ProcurementLeadTimeDays = 6, ManufacturingLeadTimeDays = 0, SafetyLeadTimeDays = 0 }
        );
        await dbContext.SaveChangesAsync();

        // 2.6 Work Centers (Dynamic capacity constraints)
        var wc100 = new WorkCenter { Code = "WC-100", Name = "Assembly Line 1", CapacityHoursPerDay = 8m, IsFiniteCapacity = true, IsActive = true };
        var wc101 = new WorkCenter { Code = "WC-101", Name = "Assembly Line 2", CapacityHoursPerDay = 8m, IsFiniteCapacity = true, IsActive = true };
        var wc200 = new WorkCenter { Code = "WC-200", Name = "Machining Center", CapacityHoursPerDay = 16m, IsFiniteCapacity = true, IsActive = true };
        var wc300 = new WorkCenter { Code = "WC-300", Name = "Electronics SMT", CapacityHoursPerDay = 24m, IsFiniteCapacity = true, IsActive = true };
        var wc400 = new WorkCenter { Code = "WC-400", Name = "Testing & QC", CapacityHoursPerDay = 8m, IsFiniteCapacity = true, IsActive = true };

        await dbContext.WorkCenters.AddRangeAsync(wc100, wc101, wc200, wc300, wc400);
        await dbContext.SaveChangesAsync();

        // 2.7 Routings & Operations
        // FG-100 Routing
        var routeFg100 = new Routing { ItemId = fg100.Id, Revision = "Rev A", IsActive = true };
        routeFg100.Operations.Add(new Operation { Sequence = 10, Name = "Final Assembly", WorkCenterId = wc100.Id, SetupHours = 1.0m, RunHoursPerUnit = 0.5m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeFg100.Operations.Add(new Operation { Sequence = 20, Name = "Testing & QC", WorkCenterId = wc400.Id, SetupHours = 0.5m, RunHoursPerUnit = 0.2m, QueueTimeDays = 0, MoveTimeDays = 0 });

        // FG-200 Routing
        var routeFg200 = new Routing { ItemId = fg200.Id, Revision = "Rev A", IsActive = true };
        routeFg200.Operations.Add(new Operation { Sequence = 10, Name = "Final Assembly", WorkCenterId = wc101.Id, SetupHours = 1.0m, RunHoursPerUnit = 0.6m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeFg200.Operations.Add(new Operation { Sequence = 20, Name = "QC Inspection", WorkCenterId = wc400.Id, SetupHours = 0.5m, RunHoursPerUnit = 0.1m, QueueTimeDays = 0, MoveTimeDays = 0 });

        // FG-300 Routing
        var routeFg300 = new Routing { ItemId = fg300.Id, Revision = "Rev A", IsActive = true };
        routeFg300.Operations.Add(new Operation { Sequence = 10, Name = "Pneumatic Assembly", WorkCenterId = wc101.Id, SetupHours = 1.5m, RunHoursPerUnit = 0.8m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeFg300.Operations.Add(new Operation { Sequence = 20, Name = "Pressure Test", WorkCenterId = wc400.Id, SetupHours = 1.0m, RunHoursPerUnit = 0.3m, QueueTimeDays = 0, MoveTimeDays = 0 });

        // SA-100 Routing
        var routeSa100 = new Routing { ItemId = sa100.Id, Revision = "Rev A", IsActive = true };
        routeSa100.Operations.Add(new Operation { Sequence = 10, Name = "Prep Frame", WorkCenterId = wc200.Id, SetupHours = 0.5m, RunHoursPerUnit = 0.2m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeSa100.Operations.Add(new Operation { Sequence = 20, Name = "Weld Frame", WorkCenterId = wc100.Id, SetupHours = 1.0m, RunHoursPerUnit = 0.3m, QueueTimeDays = 0, MoveTimeDays = 0 });

        // SA-200 Routing
        var routeSa200 = new Routing { ItemId = sa200.Id, Revision = "Rev A", IsActive = true };
        routeSa200.Operations.Add(new Operation { Sequence = 10, Name = "SMT Placement", WorkCenterId = wc300.Id, SetupHours = 2.0m, RunHoursPerUnit = 0.05m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeSa200.Operations.Add(new Operation { Sequence = 20, Name = "Wave Soldering", WorkCenterId = wc300.Id, SetupHours = 1.0m, RunHoursPerUnit = 0.02m, QueueTimeDays = 0, MoveTimeDays = 0 });

        // SA-300 Routing
        var routeSa300 = new Routing { ItemId = sa300.Id, Revision = "Rev A", IsActive = true };
        routeSa300.Operations.Add(new Operation { Sequence = 10, Name = "Wire Cutting", WorkCenterId = wc200.Id, SetupHours = 0.2m, RunHoursPerUnit = 0.1m, QueueTimeDays = 0, MoveTimeDays = 0 });
        routeSa300.Operations.Add(new Operation { Sequence = 20, Name = "Harness Assembly", WorkCenterId = wc100.Id, SetupHours = 0.5m, RunHoursPerUnit = 0.4m, QueueTimeDays = 0, MoveTimeDays = 0 });

        await dbContext.Routings.AddRangeAsync(routeFg100, routeFg200, routeFg300, routeSa100, routeSa200, routeSa300);
        await dbContext.SaveChangesAsync();

        // 2.8 Production Orders (15 Orders overlapping dates to show load spikes)
        await dbContext.ProductionOrders.AddRangeAsync(
            new ProductionOrder { OrderNumber = "PO-1001", ItemId = fg100.Id, RoutingId = routeFg100.Id, Quantity = 10m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(10) },
            new ProductionOrder { OrderNumber = "PO-1002", ItemId = fg200.Id, RoutingId = routeFg200.Id, Quantity = 15m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(12) },
            new ProductionOrder { OrderNumber = "PO-1003", ItemId = fg300.Id, RoutingId = routeFg300.Id, Quantity = 8m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(14) },
            new ProductionOrder { OrderNumber = "PO-1004", ItemId = sa100.Id, RoutingId = routeSa100.Id, Quantity = 20m, Status = ProductionOrderStatus.InProgress, DueDate = today.AddDays(5) },
            new ProductionOrder { OrderNumber = "PO-1005", ItemId = sa200.Id, RoutingId = routeSa200.Id, Quantity = 30m, Status = ProductionOrderStatus.Released, DueDate = today.AddDays(7) },
            new ProductionOrder { OrderNumber = "PO-1006", ItemId = sa300.Id, RoutingId = routeSa300.Id, Quantity = 25m, Status = ProductionOrderStatus.Released, DueDate = today.AddDays(6) },
            new ProductionOrder { OrderNumber = "PO-1007", ItemId = fg100.Id, RoutingId = routeFg100.Id, Quantity = 5m, Status = ProductionOrderStatus.InProgress, DueDate = today.AddDays(3) },
            new ProductionOrder { OrderNumber = "PO-1008", ItemId = fg200.Id, RoutingId = routeFg200.Id, Quantity = 8m, Status = ProductionOrderStatus.Released, DueDate = today.AddDays(4) },
            new ProductionOrder { OrderNumber = "PO-1009", ItemId = fg300.Id, RoutingId = routeFg300.Id, Quantity = 12m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(15) },
            new ProductionOrder { OrderNumber = "PO-1010", ItemId = sa100.Id, RoutingId = routeSa100.Id, Quantity = 15m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(8) },
            new ProductionOrder { OrderNumber = "PO-1011", ItemId = sa200.Id, RoutingId = routeSa200.Id, Quantity = 20m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(9) },
            new ProductionOrder { OrderNumber = "PO-1012", ItemId = sa300.Id, RoutingId = routeSa300.Id, Quantity = 10m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(11) },
            new ProductionOrder { OrderNumber = "PO-1013", ItemId = fg100.Id, RoutingId = routeFg100.Id, Quantity = 20m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(18) },
            new ProductionOrder { OrderNumber = "PO-1014", ItemId = fg200.Id, RoutingId = routeFg200.Id, Quantity = 25m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(20) },
            new ProductionOrder { OrderNumber = "PO-1015", ItemId = fg300.Id, RoutingId = routeFg300.Id, Quantity = 15m, Status = ProductionOrderStatus.Planned, DueDate = today.AddDays(22) }
        );
        await dbContext.SaveChangesAsync();
    }
}
