using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B3b – hồ sơ trình kết quả chọn thầu và sinh bảng giá từ kết quả thầu (design.md mục 8.6): bảng RfqDossierItem (mục hồ sơ đã đánh
    /// dấu), cột RfqSupplier.PriceListId (bảng giá NCC sinh từ RFQ). Down() mất đánh dấu hồ sơ trình và liên kết RFQ – bảng giá (bảng giá
    /// đã sinh vẫn giữ).
    /// </summary>
    public partial class V0_12_0_HoSoTrinhThau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "RfqSupplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RfqDossierItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqDossierItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqDossierItem_Rfq_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RfqSupplier_PriceListId",
                table: "RfqSupplier",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqDossierItem_RfqId_Code",
                table: "RfqDossierItem",
                columns: new[] { "RfqId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RfqSupplier_SupplierPriceList_PriceListId",
                table: "RfqSupplier",
                column: "PriceListId",
                principalTable: "SupplierPriceList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RfqSupplier_SupplierPriceList_PriceListId",
                table: "RfqSupplier");

            migrationBuilder.DropTable(
                name: "RfqDossierItem");

            migrationBuilder.DropIndex(
                name: "IX_RfqSupplier_PriceListId",
                table: "RfqSupplier");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "RfqSupplier");
        }
    }
}
