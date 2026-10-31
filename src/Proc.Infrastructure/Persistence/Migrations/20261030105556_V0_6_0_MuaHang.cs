using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.6.0 – mua hàng (design.md mục 6.2, 10.2, 10.3): bảng ApprovalRule, ApprovalStep, PurchaseRequest(+Line),
    /// PurchaseOrder(+Line); cột InventoryDocument.PurchaseOrderId, InventoryDocumentLine.PurchaseOrderLineId (phiếu nhập theo PO).
    /// Down() xóa các bảng và cột – mất quy trình duyệt, lịch sử duyệt, mọi PR, PO và liên kết phiếu nhập ↔ PO.
    /// </summary>
    public partial class V0_6_0_MuaHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderLineId",
                table: "InventoryDocumentLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                table: "InventoryDocument",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRule_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRule_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalStep",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalStep_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryTerms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ReceiveTolerancePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    TotalVat = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrder_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NeededDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequest_Warehouse_WarehouseId_CompanyId",
                        columns: x => new { x.WarehouseId, x.CompanyId },
                        principalTable: "Warehouse",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    SuggestedSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    NeededDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLine_PurchaseRequest_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLine_Supplier_SuggestedSupplierId",
                        column: x => x.SuggestedSupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    VatAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourcePrLineId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_PurchaseOrder_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_PurchaseRequestLine_SourcePrLineId",
                        column: x => x.SourcePrLineId,
                        principalTable: "PurchaseRequestLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocumentLine_PurchaseOrderLineId",
                table: "InventoryDocumentLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDocument_PurchaseOrderId",
                table: "InventoryDocument",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_CompanyId_DocType_Level",
                table: "ApprovalRule",
                columns: new[] { "CompanyId", "DocType", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_RoleId",
                table: "ApprovalRule",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_CompanyId_Status",
                table: "ApprovalStep",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_DocType_DocumentId_Round_Level",
                table: "ApprovalStep",
                columns: new[] { "DocType", "DocumentId", "Round", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_RoleId",
                table: "ApprovalStep",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_CompanyId_Date",
                table: "PurchaseOrder",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_CompanyId_No",
                table: "PurchaseOrder",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupplierId",
                table: "PurchaseOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_WarehouseId_CompanyId",
                table: "PurchaseOrder",
                columns: new[] { "WarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_ItemId",
                table: "PurchaseOrderLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_OrderId_LineNo",
                table: "PurchaseOrderLine",
                columns: new[] { "OrderId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_SourcePrLineId",
                table: "PurchaseOrderLine",
                column: "SourcePrLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_UomId",
                table: "PurchaseOrderLine",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_CompanyId_Date",
                table: "PurchaseRequest",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_CompanyId_No",
                table: "PurchaseRequest",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_WarehouseId_CompanyId",
                table: "PurchaseRequest",
                columns: new[] { "WarehouseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLine_ItemId",
                table: "PurchaseRequestLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLine_RequestId_LineNo",
                table: "PurchaseRequestLine",
                columns: new[] { "RequestId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLine_SuggestedSupplierId",
                table: "PurchaseRequestLine",
                column: "SuggestedSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLine_UomId",
                table: "PurchaseRequestLine",
                column: "UomId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocument_PurchaseOrder_PurchaseOrderId",
                table: "InventoryDocument",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryDocumentLine_PurchaseOrderLine_PurchaseOrderLineId",
                table: "InventoryDocumentLine",
                column: "PurchaseOrderLineId",
                principalTable: "PurchaseOrderLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocument_PurchaseOrder_PurchaseOrderId",
                table: "InventoryDocument");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryDocumentLine_PurchaseOrderLine_PurchaseOrderLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropTable(
                name: "ApprovalRule");

            migrationBuilder.DropTable(
                name: "ApprovalStep");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLine");

            migrationBuilder.DropTable(
                name: "PurchaseOrder");

            migrationBuilder.DropTable(
                name: "PurchaseRequestLine");

            migrationBuilder.DropTable(
                name: "PurchaseRequest");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocumentLine_PurchaseOrderLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropIndex(
                name: "IX_InventoryDocument_PurchaseOrderId",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderLineId",
                table: "InventoryDocumentLine");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "InventoryDocument");
        }
    }
}
