using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P4a (bản 0.26.0): bảng PurchaseOrderSupplierResponse – NCC xác nhận / từ chối PO trên Portal, Phòng CUVT mở lại phản hồi; mỗi PO một
    /// phản hồi đang hiệu lực (chỉ mục duy nhất có điều kiện ReopenedAt IS NULL). Down() xóa bảng – **mất toàn bộ phản hồi của NCC**.
    /// </summary>
    public partial class V0_26_0_NccXacNhanDonHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrderSupplierResponse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    PromisedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderSupplierResponse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierResponse_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierResponse_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierResponse_SupplierPortalUser_PortalUser~",
                        column: x => x.PortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierResponse_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierResponse_User_ReopenedBy",
                        column: x => x.ReopenedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierResponse_CompanyId",
                table: "PurchaseOrderSupplierResponse",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierResponse_PortalUserId",
                table: "PurchaseOrderSupplierResponse",
                column: "PortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierResponse_PurchaseOrderId",
                table: "PurchaseOrderSupplierResponse",
                column: "PurchaseOrderId",
                unique: true,
                filter: "\"ReopenedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierResponse_ReopenedBy",
                table: "PurchaseOrderSupplierResponse",
                column: "ReopenedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierResponse_SupplierId",
                table: "PurchaseOrderSupplierResponse",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderSupplierResponse");
        }
    }
}
