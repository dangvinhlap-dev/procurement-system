using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G8: hồ sơ đánh giá NCC mới – bảng SupplierOnboarding (HSN), cột Supplier.RequiresOnboarding (NCC có sẵn nhận false, giữ cách duyệt
    /// cũ) và tham số CompanyParameter.AllowSupplierWithoutOnboarding (mặc định false = bắt buộc hồ sơ).
    /// Down() xóa bảng và hai cột – mất các hồ sơ đánh giá NCC mới và cờ "phải có hồ sơ" của NCC tạo sau bản này (AuditLog vẫn giữ lịch sử).
    /// </summary>
    public partial class V0_20_0_HoSoDanhGiaNccMoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresOnboarding",
                table: "Supplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSupplierWithoutOnboarding",
                table: "CompanyParameter",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SupplierOnboarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DocumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PriceScore = table.Column<int>(type: "integer", nullable: true),
                    SampleQualityScore = table.Column<int>(type: "integer", nullable: true),
                    CapacityScore = table.Column<int>(type: "integer", nullable: true),
                    DeliveryScore = table.Column<int>(type: "integer", nullable: true),
                    PaymentScore = table.Column<int>(type: "integer", nullable: true),
                    LegalScore = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EvaluatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Recommended = table.Column<bool>(type: "boolean", nullable: true),
                    ProposalComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProposedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOnboarding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOnboarding_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOnboarding_ItemCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOnboarding_SupplierChangeRequest_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "SupplierChangeRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOnboarding_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOnboarding_CategoryId",
                table: "SupplierOnboarding",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOnboarding_ChangeRequestId",
                table: "SupplierOnboarding",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOnboarding_CompanyId_No",
                table: "SupplierOnboarding",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SupplierOnboarding_Open",
                table: "SupplierOnboarding",
                column: "SupplierId",
                unique: true,
                filter: "\"Status\" IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierOnboarding");

            migrationBuilder.DropColumn(
                name: "RequiresOnboarding",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "AllowSupplierWithoutOnboarding",
                table: "CompanyParameter");
        }
    }
}
