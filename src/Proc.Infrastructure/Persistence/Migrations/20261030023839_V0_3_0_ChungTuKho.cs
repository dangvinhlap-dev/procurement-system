using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Chứng từ kho (design.md mục 6, 10.4, 12.1): bảng InventoryDocument, InventoryDocumentLine.
    /// Down() xóa hai bảng – MẤT mọi chứng từ kho đã lập (phiếu nháp ở giai đoạn này).
    /// </summary>
    public partial class V0_3_0_ChungTuKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceSeries = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryNoteNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Deliverer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalVat = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_InventoryPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "InventoryPeriod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_Warehouse_FromWarehouseId_CompanyId",
                        columns: x => new { x.FromWarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_Warehouse_ToWarehouseId_CompanyId",
                        columns: x => new { x.ToWarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDocumentLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    VatAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentLine_InventoryDocument_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "InventoryDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_CompanyId_DocType_Date",
                table: "InventoryDocument",
                columns: new[] { "CompanyId", "DocType", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_CompanyId_DocType_No",
                table: "InventoryDocument",
                columns: new[] { "CompanyId", "DocType", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_FromWarehouseId_CompanyId",
                table: "InventoryDocument",
                columns: new[] { "FromWarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_PeriodId",
                table: "InventoryDocument",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_SupplierId",
                table: "InventoryDocument",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_ToWarehouseId_CompanyId",
                table: "InventoryDocument",
                columns: new[] { "ToWarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_DocumentId_LineNo",
                table: "InventoryDocumentLine",
                columns: new[] { "DocumentId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_ItemId",
                table: "InventoryDocumentLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_UomId",
                table: "InventoryDocumentLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryDocumentLine");

            migrationBuilder.DropTable(
                name: "InventoryDocument");
        }
    }
}
