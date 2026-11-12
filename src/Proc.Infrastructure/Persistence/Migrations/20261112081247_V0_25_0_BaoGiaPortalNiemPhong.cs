using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P3a – báo giá Portal niêm phong (design.md mục 19.5): giờ hạn nộp RFQ (RFQ cũ 23:59), người – giờ mở báo giá Portal, cờ báo giá từ
    /// Portal trên NCC được mời, bảng bản nộp Portal và dòng giá. Down() xóa hai bảng và các cột – **mất các bản nộp báo giá Portal** và giờ hạn nộp.
    /// </summary>
    public partial class V0_25_0_BaoGiaPortalNiemPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFromPortal",
                table: "RfqSupplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PortalSubmissionId",
                table: "RfqSupplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DeadlineTime",
                table: "Rfq",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(23, 59, 0)); // RFQ cũ: hạn nộp hết ngày (23:59) như quy tắc trước lát P3a.

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PortalQuotesOpenedAt",
                table: "Rfq",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PortalQuotesOpenedBy",
                table: "Rfq",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RfqPortalSubmission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedByPortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqPortalSubmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmission_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmission_Rfq_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmission_SupplierPortalUser_SubmittedByPortalUse~",
                        column: x => x.SubmittedByPortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmission_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RfqPortalSubmissionLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqPortalSubmissionLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmissionLine_RfqLine_RfqLineId",
                        column: x => x.RfqLineId,
                        principalTable: "RfqLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqPortalSubmissionLine_RfqPortalSubmission_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "RfqPortalSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmission_CompanyId",
                table: "RfqPortalSubmission",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmission_RfqId_SupplierId_Sequence",
                table: "RfqPortalSubmission",
                columns: new[] { "RfqId", "SupplierId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmission_SubmittedByPortalUserId",
                table: "RfqPortalSubmission",
                column: "SubmittedByPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmission_SupplierId_RfqId",
                table: "RfqPortalSubmission",
                columns: new[] { "SupplierId", "RfqId" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmissionLine_RfqLineId",
                table: "RfqPortalSubmissionLine",
                column: "RfqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqPortalSubmissionLine_SubmissionId_RfqLineId",
                table: "RfqPortalSubmissionLine",
                columns: new[] { "SubmissionId", "RfqLineId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RfqPortalSubmissionLine");

            migrationBuilder.DropTable(
                name: "RfqPortalSubmission");

            migrationBuilder.DropColumn(
                name: "IsFromPortal",
                table: "RfqSupplier");

            migrationBuilder.DropColumn(
                name: "PortalSubmissionId",
                table: "RfqSupplier");

            migrationBuilder.DropColumn(
                name: "DeadlineTime",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "PortalQuotesOpenedAt",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "PortalQuotesOpenedBy",
                table: "Rfq");
        }
    }
}
