using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát R2 (design.md mục 18.26): concurrency token xmin cho các chứng từ, NCC, mặt hàng, kỳ kho, bước duyệt. xmin là cột hệ thống của
    /// PostgreSQL nên Npgsql không sinh câu lệnh nào cho các AddColumn/DropColumn "xmin" bên dưới – migration chỉ cập nhật mô hình, không đổi
    /// CSDL; Down() không mất dữ liệu.
    /// </summary>
    public partial class V0_19_0_KhoaSuaTrung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SupplierPriceList",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SupplierChangeRequest",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Supplier",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SourcingRequest",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SoleSourceApproval",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Rfq",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseRequest",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseOrder",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseContractAmendment",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseContract",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MonthlySupplierApproval",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Item",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "InventoryPeriod",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "InventoryDocument",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ApprovalStep",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SupplierPriceList");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SupplierChangeRequest");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SourcingRequest");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SoleSourceApproval");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseContractAmendment");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseContract");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MonthlySupplierApproval");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "InventoryPeriod");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "InventoryDocument");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ApprovalStep");
        }
    }
}
