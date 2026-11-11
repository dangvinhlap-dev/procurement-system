using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.23.0 (lát Z4a, design.md mục 20.2a): lý do hủy bắt buộc – thêm cột CancelReason cho hợp đồng, phụ lục, RFQ, phiếu tìm nguồn
    /// (PR, PO, XNH… đã có). Down() xóa cột: mất lý do hủy đã ghi.
    /// </summary>
    public partial class V0_23_0_LyDoHuyPhieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "SourcingRequest",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Rfq",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "PurchaseContractAmendment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "PurchaseContract",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "SourcingRequest");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "PurchaseContractAmendment");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "PurchaseContract");
        }
    }
}
