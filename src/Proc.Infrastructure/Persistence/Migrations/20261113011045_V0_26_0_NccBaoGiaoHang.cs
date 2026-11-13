using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P5 (design.md 19.5, P5-1…P5-7): phiếu báo giao hàng NCC gửi trên Portal (bảng PurchaseOrderDeliveryNotice + dòng) và cột
    /// GoodsReceiptConfirmation.DeliveryNoticeId (XNH lập theo phiếu báo giao). Down() xóa hai bảng và cột – mất toàn bộ phiếu báo giao của NCC
    /// và liên kết XNH ↔ báo giao (XNH vẫn giữ nguyên).
    /// </summary>
    public partial class V0_26_0_NccBaoGiaoHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryNoticeId",
                table: "GoodsReceiptConfirmation",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDeliveryNotice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryNoteNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Deliverer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDeliveryNotice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNotice_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNotice_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNotice_SupplierPortalUser_PortalUserId",
                        column: x => x.PortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNotice_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDeliveryNoticeLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDeliveryNoticeLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNoticeLine_PurchaseOrderDeliveryNotice~",
                        column: x => x.NoticeId,
                        principalTable: "PurchaseOrderDeliveryNotice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDeliveryNoticeLine_PurchaseOrderLine_PurchaseO~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptConfirmation_DeliveryNoticeId",
                table: "GoodsReceiptConfirmation",
                column: "DeliveryNoticeId",
                unique: true,
                filter: "\"DeliveryNoticeId\" IS NOT NULL AND \"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNotice_CompanyId_No",
                table: "PurchaseOrderDeliveryNotice",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNotice_PortalUserId",
                table: "PurchaseOrderDeliveryNotice",
                column: "PortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNotice_PurchaseOrderId",
                table: "PurchaseOrderDeliveryNotice",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNotice_SupplierId",
                table: "PurchaseOrderDeliveryNotice",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNoticeLine_NoticeId_LineNo",
                table: "PurchaseOrderDeliveryNoticeLine",
                columns: new[] { "NoticeId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDeliveryNoticeLine_PurchaseOrderLineId",
                table: "PurchaseOrderDeliveryNoticeLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptConfirmation_PurchaseOrderDeliveryNotice_Delive~",
                table: "GoodsReceiptConfirmation",
                column: "DeliveryNoticeId",
                principalTable: "PurchaseOrderDeliveryNotice",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptConfirmation_PurchaseOrderDeliveryNotice_Delive~",
                table: "GoodsReceiptConfirmation");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDeliveryNoticeLine");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDeliveryNotice");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptConfirmation_DeliveryNoticeId",
                table: "GoodsReceiptConfirmation");

            migrationBuilder.DropColumn(
                name: "DeliveryNoticeId",
                table: "GoodsReceiptConfirmation");
        }
    }
}
