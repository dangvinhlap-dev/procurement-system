using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát O2c (design.md mục 19.3): bảng các lần nhắc quá hạn duyệt. Bước đang chờ từ trước bản này được nhắc ngay ở lần quét đầu nếu đã quá
    /// 1 ngày làm việc (một lần, các lần sau cách nhau 1 ngày làm việc).
    /// </summary>
    public partial class V0_22_0_NhacQuaHanDuyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalReminder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RemindedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalReminder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalReminder_ApprovalStep_ApprovalStepId",
                        column: x => x.ApprovalStepId,
                        principalTable: "ApprovalStep",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalReminder_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalReminder_ApprovalStepId_Sequence",
                table: "ApprovalReminder",
                columns: new[] { "ApprovalStepId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalReminder_CompanyId",
                table: "ApprovalReminder",
                column: "CompanyId");
        }

        /// <inheritdoc />
        /// <remarks>Mất dữ liệu: lịch sử các lần nhắc (thông báo nhắc đã gửi vẫn giữ); nâng lại bản này thì bước còn chờ quá hạn được nhắc lại.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalReminder");
        }
    }
}
