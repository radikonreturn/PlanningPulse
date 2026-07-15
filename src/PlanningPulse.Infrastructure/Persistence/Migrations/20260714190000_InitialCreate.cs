using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPulse.Infrastructure.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Tenants",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Slug = table.Column<string>(maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Tenants", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Email = table.Column<string>(maxLength: 320, nullable: false),
                NormalizedEmail = table.Column<string>(maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(maxLength: 200, nullable: false),
                PasswordHash = table.Column<string>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Items",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                ItemNumber = table.Column<string>(maxLength: 80, nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 1000, nullable: true),
                Type = table.Column<int>(nullable: false),
                UnitOfMeasure = table.Column<string>(maxLength: 20, nullable: false),
                MinimumOrderQuantity = table.Column<decimal>(nullable: true),
                MaximumInventoryQuantity = table.Column<decimal>(nullable: true),
                EconomicOrderQuantity = table.Column<decimal>(nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Items", x => x.Id);
                table.ForeignKey("FK_Items_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                SupplierNumber = table.Column<string>(maxLength: 80, nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Email = table.Column<string>(maxLength: 320, nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
                table.ForeignKey("FK_Suppliers_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TenantUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                Role = table.Column<int>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantUsers", x => x.Id);
                table.ForeignKey("FK_TenantUsers_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TenantUsers_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkCenters",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                Code = table.Column<string>(maxLength: 80, nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                CapacityHoursPerDay = table.Column<decimal>(nullable: false),
                IsFiniteCapacity = table.Column<bool>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkCenters", x => x.Id);
                table.ForeignKey("FK_WorkCenters_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Boms",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                ParentItemId = table.Column<Guid>(nullable: false),
                Revision = table.Column<string>(maxLength: 40, nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                EffectiveFrom = table.Column<DateOnly>(nullable: false),
                EffectiveTo = table.Column<DateOnly>(nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Boms", x => x.Id);
                table.ForeignKey("FK_Boms_Items_ParentItemId", x => x.ParentItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Boms_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InventoryLevels",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                ItemId = table.Column<Guid>(nullable: false),
                LocationCode = table.Column<string>(maxLength: 80, nullable: false),
                OnHandQuantity = table.Column<decimal>(nullable: false),
                AllocatedQuantity = table.Column<decimal>(nullable: false),
                OnOrderQuantity = table.Column<decimal>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryLevels", x => x.Id);
                table.ForeignKey("FK_InventoryLevels_Items_ItemId", x => x.ItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryLevels_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LeadTimes",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                ItemId = table.Column<Guid>(nullable: false),
                SupplierId = table.Column<Guid>(nullable: true),
                ProcurementLeadTimeDays = table.Column<int>(nullable: false),
                ManufacturingLeadTimeDays = table.Column<int>(nullable: false),
                SafetyLeadTimeDays = table.Column<int>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeadTimes", x => x.Id);
                table.ForeignKey("FK_LeadTimes_Items_ItemId", x => x.ItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_LeadTimes_Suppliers_SupplierId", x => x.SupplierId, "Suppliers", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_LeadTimes_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Routings",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                ItemId = table.Column<Guid>(nullable: false),
                Revision = table.Column<string>(maxLength: 40, nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Routings", x => x.Id);
                table.ForeignKey("FK_Routings_Items_ItemId", x => x.ItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Routings_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "BomLines",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                BomId = table.Column<Guid>(nullable: false),
                ComponentItemId = table.Column<Guid>(nullable: false),
                QuantityPer = table.Column<decimal>(nullable: false),
                ScrapFactor = table.Column<decimal>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BomLines", x => x.Id);
                table.ForeignKey("FK_BomLines_Boms_BomId", x => x.BomId, "Boms", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_BomLines_Items_ComponentItemId", x => x.ComponentItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_BomLines_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Operations",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                RoutingId = table.Column<Guid>(nullable: false),
                Sequence = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                WorkCenterId = table.Column<Guid>(nullable: false),
                SetupHours = table.Column<decimal>(nullable: false),
                RunHoursPerUnit = table.Column<decimal>(nullable: false),
                QueueTimeDays = table.Column<int>(nullable: false),
                MoveTimeDays = table.Column<int>(nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Operations", x => x.Id);
                table.ForeignKey("FK_Operations_Routings_RoutingId", x => x.RoutingId, "Routings", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_Operations_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_Operations_WorkCenters_WorkCenterId", x => x.WorkCenterId, "WorkCenters", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProductionOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: false),
                OrderNumber = table.Column<string>(maxLength: 80, nullable: false),
                ItemId = table.Column<Guid>(nullable: false),
                RoutingId = table.Column<Guid>(nullable: true),
                Quantity = table.Column<decimal>(nullable: false),
                Status = table.Column<int>(nullable: false),
                DueDate = table.Column<DateOnly>(nullable: false),
                ScheduledStartDate = table.Column<DateOnly>(nullable: true),
                ScheduledEndDate = table.Column<DateOnly>(nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                table.ForeignKey("FK_ProductionOrders_Items_ItemId", x => x.ItemId, "Items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductionOrders_Routings_RoutingId", x => x.RoutingId, "Routings", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_ProductionOrders_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_BomLines_BomId", "BomLines", "BomId");
        migrationBuilder.CreateIndex("IX_BomLines_ComponentItemId", "BomLines", "ComponentItemId");
        migrationBuilder.CreateIndex("IX_BomLines_TenantId", "BomLines", "TenantId");
        migrationBuilder.CreateIndex("IX_Boms_ParentItemId", "Boms", "ParentItemId");
        migrationBuilder.CreateIndex("IX_Boms_TenantId_ParentItemId_Revision", "Boms", new[] { "TenantId", "ParentItemId", "Revision" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryLevels_ItemId", "InventoryLevels", "ItemId");
        migrationBuilder.CreateIndex("IX_InventoryLevels_TenantId_ItemId_LocationCode", "InventoryLevels", new[] { "TenantId", "ItemId", "LocationCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_Items_TenantId_ItemNumber", "Items", new[] { "TenantId", "ItemNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_LeadTimes_ItemId", "LeadTimes", "ItemId");
        migrationBuilder.CreateIndex("IX_LeadTimes_SupplierId", "LeadTimes", "SupplierId");
        migrationBuilder.CreateIndex("IX_LeadTimes_TenantId_ItemId_SupplierId", "LeadTimes", new[] { "TenantId", "ItemId", "SupplierId" }, unique: true);
        migrationBuilder.CreateIndex("IX_Operations_TenantId_RoutingId_Sequence", "Operations", new[] { "TenantId", "RoutingId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_Operations_RoutingId", "Operations", "RoutingId");
        migrationBuilder.CreateIndex("IX_Operations_WorkCenterId", "Operations", "WorkCenterId");
        migrationBuilder.CreateIndex("IX_ProductionOrders_ItemId", "ProductionOrders", "ItemId");
        migrationBuilder.CreateIndex("IX_ProductionOrders_RoutingId", "ProductionOrders", "RoutingId");
        migrationBuilder.CreateIndex("IX_ProductionOrders_TenantId_OrderNumber", "ProductionOrders", new[] { "TenantId", "OrderNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_Routings_ItemId", "Routings", "ItemId");
        migrationBuilder.CreateIndex("IX_Routings_TenantId_ItemId_Revision", "Routings", new[] { "TenantId", "ItemId", "Revision" }, unique: true);
        migrationBuilder.CreateIndex("IX_Suppliers_TenantId_SupplierNumber", "Suppliers", new[] { "TenantId", "SupplierNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_Tenants_Slug", "Tenants", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_TenantUsers_TenantId_UserId", "TenantUsers", new[] { "TenantId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_TenantUsers_UserId", "TenantUsers", "UserId");
        migrationBuilder.CreateIndex("IX_Users_NormalizedEmail", "Users", "NormalizedEmail", unique: true);
        migrationBuilder.CreateIndex("IX_WorkCenters_TenantId_Code", "WorkCenters", new[] { "TenantId", "Code" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("BomLines");
        migrationBuilder.DropTable("InventoryLevels");
        migrationBuilder.DropTable("LeadTimes");
        migrationBuilder.DropTable("Operations");
        migrationBuilder.DropTable("ProductionOrders");
        migrationBuilder.DropTable("TenantUsers");
        migrationBuilder.DropTable("Boms");
        migrationBuilder.DropTable("Suppliers");
        migrationBuilder.DropTable("WorkCenters");
        migrationBuilder.DropTable("Routings");
        migrationBuilder.DropTable("Users");
        migrationBuilder.DropTable("Items");
        migrationBuilder.DropTable("Tenants");
    }
}
