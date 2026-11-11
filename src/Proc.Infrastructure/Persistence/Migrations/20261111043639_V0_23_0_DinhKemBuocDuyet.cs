using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Z1 (design.md mục 20): đính kèm gắn với bước duyệt (<c>ApprovalStepId</c>) và cờ "Bổ sung sau duyệt" (<c>AddedAfterApproval</c>).
    /// Chỉ thêm cột; file cũ mặc định không thuộc bước nào. <c>Down()</c> xóa hai cột – mất thông tin file nào thuộc bước duyệt nào,
    /// file vẫn còn.
    /// </summary>
    public partial class V0_23_0_DinhKemBuocDuyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AddedAfterApproval",
                table: "Attachment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalStepId",
                table: "Attachment",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedAfterApproval",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "ApprovalStepId",
                table: "Attachment");
        }
    }
}
