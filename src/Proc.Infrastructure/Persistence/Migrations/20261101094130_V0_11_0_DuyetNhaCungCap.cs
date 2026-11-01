using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B2 – duyệt NCC hai chặng (design.md mục 18.6): bảng SupplierChangeRequest; cột ApprovalStep.Stage (bước cũ của PR/PO là chặng 1).
    /// Down() mất các yêu cầu duyệt NCC và thông tin chặng (NCC giữ nguyên trạng thái, tài khoản đã áp).
    /// </summary>
    public partial class V0_11_0_DuyetNhaCungCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "ApprovalStep",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "SupplierChangeRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: true),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BankAccountsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CurrentSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProposedSummary = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierChangeRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierChangeRequest_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierChangeRequest_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierChangeRequest_CompanyId",
                table: "SupplierChangeRequest",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierChangeRequest_No",
                table: "SupplierChangeRequest",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierChangeRequest_SupplierId_Status",
                table: "SupplierChangeRequest",
                columns: new[] { "SupplierId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierChangeRequest");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ApprovalStep");
        }
    }
}
