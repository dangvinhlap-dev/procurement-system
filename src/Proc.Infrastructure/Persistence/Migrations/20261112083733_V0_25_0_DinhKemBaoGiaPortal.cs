using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P3b-1 – file báo giá NCC nộp trên Portal (design.md mục 19.5): cột tài khoản Portal tải file lên. Down() xóa cột – file Portal còn
    /// nhưng mất thông tin người tải.
    /// </summary>
    public partial class V0_25_0_DinhKemBaoGiaPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByPortalUserId",
                table: "Attachment",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadedByPortalUserId",
                table: "Attachment");
        }
    }
}
