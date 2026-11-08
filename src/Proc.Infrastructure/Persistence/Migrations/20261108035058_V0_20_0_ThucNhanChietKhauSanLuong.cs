using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G6b (design.md mục 18.25): thực nhận chiết khấu sản lượng gộp theo kỳ – bảng <c>VolumeRebateReceipt</c> (lần ghi nhận, thuộc tổng
    /// công ty) và <c>VolumeRebateReceiptShare</c> (phần từng công ty áp dụng, chia theo tỷ lệ doanh số kỳ lúc ghi). Bảng mới, không đụng dữ liệu cũ.
    /// </summary>
    public partial class V0_20_0_ThucNhanChietKhauSanLuong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VolumeRebateReceipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    RefNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeRebateReceipt_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateReceipt_VolumeRebateAgreement_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "VolumeRebateAgreement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolumeRebateReceiptShare",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    SharePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateReceiptShare", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeRebateReceiptShare_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateReceiptShare_VolumeRebateReceipt_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "VolumeRebateReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateReceipt_AgreementId_PeriodFrom",
                table: "VolumeRebateReceipt",
                columns: new[] { "AgreementId", "PeriodFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateReceipt_CompanyId",
                table: "VolumeRebateReceipt",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateReceiptShare_CompanyId",
                table: "VolumeRebateReceiptShare",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateReceiptShare_ReceiptId_CompanyId",
                table: "VolumeRebateReceiptShare",
                columns: new[] { "ReceiptId", "CompanyId" },
                unique: true);
        }

        /// <summary>Gỡ: xóa 2 bảng – <b>mất các lần ghi nhận thực nhận và phần chia</b> (AuditLog vẫn giữ lịch sử).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolumeRebateReceiptShare");

            migrationBuilder.DropTable(
                name: "VolumeRebateReceipt");
        }
    }
}
