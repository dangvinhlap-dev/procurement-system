using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.4.0 – đóng kỳ và giá vốn (design.md mục 7.2–7.4): bảng StockPeriodBalance (số dư kỳ đã đóng), ItemPeriodCost (giá vốn kỳ),
    /// cột InventoryPeriod.ReopenReason. Down() xóa hai bảng và cột – mất số dư, giá vốn các kỳ đã đóng và lý do mở lại kỳ; trước khi
    /// gỡ nên mở lại các kỳ đã đóng để trạng thái kỳ khớp dữ liệu.
    /// </summary>
    public partial class V0_4_0_DongKyGiaVon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "InventoryPeriod",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemPeriodCost",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPeriodCost", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemPeriodCost_InventoryPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "InventoryPeriod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPeriodCost_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockPeriodBalance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OpenQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OpenAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    InQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    OutQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OutAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CloseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CloseAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockPeriodBalance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockPeriodBalance_InventoryPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "InventoryPeriod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockPeriodBalance_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockPeriodBalance_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemPeriodCost_ItemId",
                table: "ItemPeriodCost",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPeriodCost_PeriodId_ItemId",
                table: "ItemPeriodCost",
                columns: new[] { "PeriodId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockPeriodBalance_CompanyId_PeriodId",
                table: "StockPeriodBalance",
                columns: new[] { "CompanyId", "PeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockPeriodBalance_ItemId",
                table: "StockPeriodBalance",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockPeriodBalance_PeriodId_WarehouseId_ItemId_LotNo",
                table: "StockPeriodBalance",
                columns: new[] { "PeriodId", "WarehouseId", "ItemId", "LotNo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_StockPeriodBalance_WarehouseId_CompanyId",
                table: "StockPeriodBalance",
                columns: new[] { "WarehouseId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemPeriodCost");

            migrationBuilder.DropTable(
                name: "StockPeriodBalance");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "InventoryPeriod");
        }
    }
}
