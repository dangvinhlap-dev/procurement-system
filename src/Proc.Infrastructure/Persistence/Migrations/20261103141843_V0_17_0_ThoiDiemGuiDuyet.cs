using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Người và thời điểm gửi duyệt của từng lượt (ApprovalStep.SubmittedAt/SubmittedBy) để lịch sử duyệt hiện đủ ngày giờ; bước cũ để trống.
    /// </summary>
    public partial class V0_17_0_ThoiDiemGuiDuyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "ApprovalStep",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedBy",
                table: "ApprovalStep",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất người/thời điểm gửi duyệt đã ghi (kết quả duyệt, người duyệt vẫn còn).
            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "ApprovalStep");
        }
    }
}
