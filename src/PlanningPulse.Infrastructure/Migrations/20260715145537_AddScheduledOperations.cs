using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkCenterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SetupHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RunHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ScheduledStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ScheduledEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledOperations_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledOperations_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledOperations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledOperations_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_OperationId",
                table: "ScheduledOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_ProductionOrderId",
                table: "ScheduledOperations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_TenantId_ProductionOrderId_Sequence",
                table: "ScheduledOperations",
                columns: new[] { "TenantId", "ProductionOrderId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledOperations_WorkCenterId",
                table: "ScheduledOperations",
                column: "WorkCenterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledOperations");
        }
    }
}
