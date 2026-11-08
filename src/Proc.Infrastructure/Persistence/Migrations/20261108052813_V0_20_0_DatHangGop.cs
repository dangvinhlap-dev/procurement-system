using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G7 (design.md mục 18.25): phiếu đặt hàng gộp – bảng <c>PooledOrder</c> (thuộc tổng công ty), <c>PooledOrderCompany</c> (công ty nhận,
    /// kho, bộ phận, PO đã sinh), <c>PooledOrderLine</c> (dòng theo dòng phiếu GTH, giá Ban duyệt), <c>PooledOrderAllocation</c> (SL dòng ×
    /// công ty); cột tham số <c>CompanyParameter.PooledMarginPercent</c> (% biên độ phân bổ, mặc định 10). Down() xóa các bảng và cột này –
    /// mất phiếu đặt gộp đã lập (PO đã sinh vẫn còn ở các công ty).
    /// </summary>
    public partial class V0_20_0_DatHangGop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PooledMarginPercent",
                table: "CompanyParameter",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.CreateTable(
                name: "PooledOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthlyApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MarginPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    OrderedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OrderedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PooledOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PooledOrder_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrder_ItemCategory_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrder_MonthlySupplierApproval_MonthlyApprovalId",
                        column: x => x.MonthlyApprovalId,
                        principalTable: "MonthlySupplierApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrder_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PooledOrderCompany",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PooledOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PooledOrderCompany", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PooledOrderCompany_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderCompany_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderCompany_PooledOrder_PooledOrderId",
                        column: x => x.PooledOrderId,
                        principalTable: "PooledOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PooledOrderCompany_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderCompany_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PooledOrderLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PooledOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    MonthlyApprovalLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    AllocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PooledOrderLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PooledOrderLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderLine_MonthlySupplierApprovalLine_MonthlyApproval~",
                        column: x => x.MonthlyApprovalLineId,
                        principalTable: "MonthlySupplierApprovalLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderLine_PooledOrder_PooledOrderId",
                        column: x => x.PooledOrderId,
                        principalTable: "PooledOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PooledOrderLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PooledOrderAllocation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PooledOrderAllocation", x => x.Id);
                    table.CheckConstraint("CK_PooledOrderAllocation_Qty", "\"Qty\" > 0");
                    table.ForeignKey(
                        name: "FK_PooledOrderAllocation_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PooledOrderAllocation_PooledOrderLine_LineId",
                        column: x => x.LineId,
                        principalTable: "PooledOrderLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrder_CompanyId_No",
                table: "PooledOrder",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrder_ItemCategoryId",
                table: "PooledOrder",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrder_MonthlyApprovalId",
                table: "PooledOrder",
                column: "MonthlyApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrder_OrderDate_Status",
                table: "PooledOrder",
                columns: new[] { "OrderDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrder_SupplierId",
                table: "PooledOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderAllocation_CompanyId",
                table: "PooledOrderAllocation",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderAllocation_LineId_CompanyId",
                table: "PooledOrderAllocation",
                columns: new[] { "LineId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderCompany_CompanyId",
                table: "PooledOrderCompany",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderCompany_DepartmentId",
                table: "PooledOrderCompany",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderCompany_PooledOrderId_CompanyId",
                table: "PooledOrderCompany",
                columns: new[] { "PooledOrderId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderCompany_PurchaseOrderId",
                table: "PooledOrderCompany",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderCompany_WarehouseId",
                table: "PooledOrderCompany",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderLine_ItemId",
                table: "PooledOrderLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderLine_MonthlyApprovalLineId",
                table: "PooledOrderLine",
                column: "MonthlyApprovalLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderLine_PooledOrderId_MonthlyApprovalLineId",
                table: "PooledOrderLine",
                columns: new[] { "PooledOrderId", "MonthlyApprovalLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PooledOrderLine_UomId",
                table: "PooledOrderLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PooledOrderAllocation");

            migrationBuilder.DropTable(
                name: "PooledOrderCompany");

            migrationBuilder.DropTable(
                name: "PooledOrderLine");

            migrationBuilder.DropTable(
                name: "PooledOrder");

            migrationBuilder.DropColumn(
                name: "PooledMarginPercent",
                table: "CompanyParameter");
        }
    }
}
