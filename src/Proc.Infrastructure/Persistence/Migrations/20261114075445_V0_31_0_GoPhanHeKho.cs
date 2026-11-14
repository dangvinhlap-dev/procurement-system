using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát K3 – gỡ hẳn phân hệ kho (design.md mục 19.4; người dùng chốt 14/11/2026: hệ thống đang dev, không cần sao lưu trước).
    /// Up: xóa quyền kho trong vai trò, đính kèm của phiếu kho (dòng CSDL – file trên máy chủ để lại, xóa tay nếu cần), cấu hình và bộ đếm số
    /// của loại phiếu kho (GRN, RTV, TRF, ICS, ICR, ADJ), rồi xóa 7 bảng kho. AuditLog giữ nguyên (chỉ thêm, không sửa/xóa – CLAUDE.md mục 4).
    /// Down: tạo lại các bảng kho <b>rỗng</b> – mất dữ liệu phiếu kho, sổ kho, tồn kho, kỳ kho, giá vốn, quyền kho đã xóa; không khôi phục được.
    /// </summary>
    public partial class V0_31_0_GoPhanHeKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "PermissionCode" IN (
                    'Inventory.Document.View', 'Inventory.Document.Create', 'Inventory.Document.Post', 'Inventory.Document.Unpost',
                    'Inventory.Adjustment.Create', 'Inventory.Adjustment.Post', 'Inventory.Receipt.Accept', 'Inventory.Stock.View',
                    'Inventory.Period.View', 'Inventory.Period.Open', 'Inventory.Period.Close', 'Inventory.Period.Reopen',
                    'Report.Stock.View', 'Report.Stock.ViewAllCompanies', 'Report.Accounting.Export');
                DELETE FROM "Attachment" WHERE "OwnerType" = 'InventoryDocument';
                DELETE FROM "DocumentSequence" WHERE "DocType" IN ('GRN', 'RTV', 'TRF', 'ICS', 'ICR', 'ADJ');
                DELETE FROM "DocumentNumbering" WHERE "DocType" IN ('GRN', 'RTV', 'TRF', 'ICS', 'ICR', 'ADJ');
                """);

            migrationBuilder.DropTable(
                name: "ItemPeriodCost");

            migrationBuilder.DropTable(
                name: "StockLedger");

            migrationBuilder.DropTable(
                name: "StockOnHand");

            migrationBuilder.DropTable(
                name: "StockPeriodBalance");

            migrationBuilder.DropTable(
                name: "InventoryDocumentLine");

            migrationBuilder.DropTable(
                name: "InventoryDocument");

            migrationBuilder.DropTable(
                name: "InventoryPeriod");
        }

        /// <inheritdoc />
        /// <remarks>Chỉ tạo lại cấu trúc bảng kho rỗng; dữ liệu kho, quyền kho, đính kèm phiếu kho đã xóa ở Up không khôi phục.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryPeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    ReopenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPeriod", x => x.Id);
                    table.CheckConstraint("CK_InventoryPeriod_Month", "\"Month\" BETWEEN 0 AND 12");
                    table.ForeignKey(
                        name: "FK_InventoryPeriod_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockOnHand",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "InventoryDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Acceptance = table.Column<int>(type: "integer", nullable: false),
                    AcceptanceNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountReportNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CounterCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Deliverer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryNoteNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceSeries = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LinkedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Receiver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalVat = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                        name: "FK_InventoryDocument_Company_CounterCompanyId",
                        column: x => x.CounterCompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_InventoryDocument_LinkedDocumentId",
                        column: x => x.LinkedDocumentId,
                        principalTable: "InventoryDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_InventoryPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "InventoryPeriod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocument_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
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
                name: "ItemPeriodCost",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false)
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
                    CloseAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CloseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    InQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OpenAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    OpenQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OutAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    OutQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "InventoryDocumentLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BookQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CountedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DeliveredQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    QcNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QcResult = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RejectedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryDocumentLine", x => x.Id);
                    table.CheckConstraint("CK_InventoryDocumentLine_Direction", "\"Direction\" IS NULL OR \"Direction\" IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_InventoryDocumentLine_InventoryDocumentLine_SourceLineId",
                        column: x => x.SourceLineId,
                        principalTable: "InventoryDocumentLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                        name: "FK_InventoryDocumentLine_PurchaseOrderLine_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDocumentLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCostFinal = table.Column<bool>(type: "boolean", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "IX_InventoryDocument_CounterCompanyId",
                table: "InventoryDocument",
                column: "CounterCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_FromWarehouseId_CompanyId",
                table: "InventoryDocument",
                columns: new[] { "FromWarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_LinkedDocumentId",
                table: "InventoryDocument",
                column: "LinkedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_PeriodId",
                table: "InventoryDocument",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_PurchaseOrderId",
                table: "InventoryDocument",
                column: "PurchaseOrderId");

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
                name: "IX_InventoryDocumentLine_PurchaseOrderLineId",
                table: "InventoryDocumentLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_SourceLineId",
                table: "InventoryDocumentLine",
                column: "SourceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_UomId",
                table: "InventoryDocumentLine",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPeriod_CompanyId_Year_Month",
                table: "InventoryPeriod",
                columns: new[] { "CompanyId", "Year", "Month" },
                unique: true);

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
    }
}
