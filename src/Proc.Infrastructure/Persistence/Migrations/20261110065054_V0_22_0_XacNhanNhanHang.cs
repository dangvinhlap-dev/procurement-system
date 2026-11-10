using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát K2: phiếu xác nhận nhận hàng XNH thay phiếu nhập kho (design.md mục 19.4) – bảng GoodsReceiptConfirmation, dòng
    /// GoodsReceiptConfirmationLine, lịch sử điều chỉnh GoodsReceiptAdjustment. Không đổi bảng cũ.
    /// Down() xóa ba bảng – mất các phiếu XNH, SL thực nhận của PO tính theo XNH và lịch sử điều chỉnh (AuditLog vẫn giữ lịch sử thay đổi).
    /// </summary>
    public partial class V0_22_0_XacNhanNhanHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsReceiptConfirmation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Deliverer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceSeries = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Acceptance = table.Column<int>(type: "integer", nullable: false),
                    AcceptanceNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalVat = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptConfirmation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmation_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmation_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmation_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmation_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptConfirmationLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DeliveredQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AcceptedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RejectedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdjustedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedBaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CheckResult = table.Column<int>(type: "integer", nullable: false),
                    CheckNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptConfirmationLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmationLine_GoodsReceiptConfirmation_Confi~",
                        column: x => x.ConfirmationId,
                        principalTable: "GoodsReceiptConfirmation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmationLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmationLine_PurchaseOrderLine_PurchaseOrde~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptConfirmationLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptAdjustment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AdjustedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptAdjustment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptAdjustment_GoodsReceiptConfirmationLine_LineId",
                        column: x => x.LineId,
                        principalTable: "GoodsReceiptConfirmationLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptAdjustment_GoodsReceiptConfirmation_Confirmatio~",
                        column: x => x.ConfirmationId,
                        principalTable: "GoodsReceiptConfirmation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptAdjustment_ConfirmationId",
                table: "GoodsReceiptAdjustment",
                column: "ConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptAdjustment_LineId",
                table: "GoodsReceiptAdjustment",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_CompanyId_Date",
                table: "GoodsReceiptConfirmation",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_CompanyId_No",
                table: "GoodsReceiptConfirmation",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_PurchaseOrderId",
                table: "GoodsReceiptConfirmation",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_SupplierId",
                table: "GoodsReceiptConfirmation",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_WarehouseId_CompanyId",
                table: "GoodsReceiptConfirmation",
                columns: new[] { "WarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmationLine_ConfirmationId_LineNo",
                table: "GoodsReceiptConfirmationLine",
                columns: new[] { "ConfirmationId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmationLine_ItemId",
                table: "GoodsReceiptConfirmationLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmationLine_PurchaseOrderLineId",
                table: "GoodsReceiptConfirmationLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmationLine_UomId",
                table: "GoodsReceiptConfirmationLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptAdjustment");

            migrationBuilder.DropTable(
                name: "GoodsReceiptConfirmationLine");

            migrationBuilder.DropTable(
                name: "GoodsReceiptConfirmation");
        }
    }
}
