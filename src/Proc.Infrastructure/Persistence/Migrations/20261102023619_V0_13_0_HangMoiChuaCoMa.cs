using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát C2b (design.md mục 18.12): bảng PurchaseRequestNewItem – hàng mới chưa có mã trên PR (mô tả, quy cách, ĐVT, SL, giá dự kiến), phiếu
    /// tìm nguồn và mặt hàng gắn sau.
    /// </summary>
    public partial class V0_13_0_HangMoiChuaCoMa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseRequestNewItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Specification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    NeededDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourcingRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedLineId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestNewItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestNewItem_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestNewItem_PurchaseRequestLine_LinkedLineId",
                        column: x => x.LinkedLineId,
                        principalTable: "PurchaseRequestLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestNewItem_PurchaseRequest_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestNewItem_SourcingRequest_SourcingRequestId",
                        column: x => x.SourcingRequestId,
                        principalTable: "SourcingRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestNewItem_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestNewItem_ItemId",
                table: "PurchaseRequestNewItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestNewItem_LinkedLineId",
                table: "PurchaseRequestNewItem",
                column: "LinkedLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestNewItem_RequestId_LineNo",
                table: "PurchaseRequestNewItem",
                columns: new[] { "RequestId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestNewItem_SourcingRequestId",
                table: "PurchaseRequestNewItem",
                column: "SourcingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestNewItem_UomId",
                table: "PurchaseRequestNewItem",
                column: "UomId");
        }

        /// <summary>Gỡ bảng PurchaseRequestNewItem – MẤT DỮ LIỆU: hàng mới chưa có mã đã ghi trên PR (dòng có mã sinh ra khi gắn mã vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseRequestNewItem");
        }
    }
}
