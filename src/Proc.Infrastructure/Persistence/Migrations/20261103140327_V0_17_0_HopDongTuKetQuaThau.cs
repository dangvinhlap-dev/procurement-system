using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Liên kết NCC trúng thầu với hợp đồng nguyên tắc sinh từ kết quả chọn thầu (design.md mục 8.6): cột RfqSupplier.ContractId.
    /// </summary>
    public partial class V0_17_0_HopDongTuKetQuaThau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "RfqSupplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfqSupplier_ContractId",
                table: "RfqSupplier",
                column: "ContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_RfqSupplier_PurchaseContract_ContractId",
                table: "RfqSupplier",
                column: "ContractId",
                principalTable: "PurchaseContract",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất liên kết RFQ → hợp đồng (hợp đồng vẫn còn; chỉ RFQ không còn biết hợp đồng nào sinh từ nó).
            migrationBuilder.DropForeignKey(
                name: "FK_RfqSupplier_PurchaseContract_ContractId",
                table: "RfqSupplier");

            migrationBuilder.DropIndex(
                name: "IX_RfqSupplier_ContractId",
                table: "RfqSupplier");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "RfqSupplier");
        }
    }
}
