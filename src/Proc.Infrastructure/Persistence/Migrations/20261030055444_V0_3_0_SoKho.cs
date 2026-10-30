using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sổ kho và tồn tức thời (design.md mục 12): bảng StockLedger, StockOnHand.
    /// Down() xóa hai bảng – MẤT toàn bộ sổ kho và số tồn; chứng từ vẫn còn nhưng trạng thái Đã ghi sổ không còn khớp sổ kho.
    /// </summary>
    public partial class V0_3_0_SoKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    IsCostFinal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLedger", x => x.Id);
                    table.CheckConstraint("CK_StockLedger_BaseQty", "\"BaseQty\" > 0");
                    table.CheckConstraint("CK_StockLedger_Direction", "\"Direction\" IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_StockLedger_InventoryDocumentLine_DocumentLineId",
                        column: x => x.DocumentLineId,
                        principalTable: "InventoryDocumentLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedger_InventoryDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "InventoryDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedger_InventoryPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "InventoryPeriod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedger_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedger_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockOnHand",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockOnHand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockOnHand_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockOnHand_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_CompanyId_PeriodId_ItemId",
                table: "StockLedger",
                columns: new[] { "CompanyId", "PeriodId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_CompanyId_WarehouseId_ItemId_PostingDate",
                table: "StockLedger",
                columns: new[] { "CompanyId", "WarehouseId", "ItemId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_DocumentId",
                table: "StockLedger",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_DocumentLineId",
                table: "StockLedger",
                column: "DocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_ItemId",
                table: "StockLedger",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_PeriodId",
                table: "StockLedger",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_WarehouseId_CompanyId",
                table: "StockLedger",
                columns: new[] { "WarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockOnHand_CompanyId_WarehouseId_ItemId_LotNo",
                table: "StockOnHand",
                columns: new[] { "CompanyId", "WarehouseId", "ItemId", "LotNo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_StockOnHand_ItemId",
                table: "StockOnHand",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockOnHand_WarehouseId_CompanyId",
                table: "StockOnHand",
                columns: new[] { "WarehouseId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockLedger");

            migrationBuilder.DropTable(
                name: "StockOnHand");
        }
    }
}
