using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát O1: người duyệt động (design.md mục 19.3). ApprovalRule thêm Kind (1 = theo vai trò, 2 = Trưởng phòng ban, 3 = GĐ khối của phiếu),
    /// RoleId cho phép null (cấp người duyệt động không có vai trò); ApprovalStep thêm Kind và DepartmentId (bộ phận của phiếu lúc gửi).
    /// Dữ liệu cũ nhận Kind = 1 nên quy trình đang có giữ nguyên.
    /// Down() xóa các cấp duyệt người duyệt động (RoleId null) trước khi đặt lại RoleId bắt buộc, bỏ Kind/DepartmentId của bước duyệt –
    /// mất cấu hình cấp động và thông tin "ai duyệt" của bước động trong lịch sử (người đã duyệt vẫn giữ ở ActedBy).
    /// </summary>
    public partial class V0_22_0_DuyetDong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "ApprovalStep",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ApprovalStep",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "ApprovalRule",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ApprovalRule",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_DepartmentId",
                table: "ApprovalStep",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalStep_Department_DepartmentId",
                table: "ApprovalStep",
                column: "DepartmentId",
                principalTable: "Department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalStep_Department_DepartmentId",
                table: "ApprovalStep");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalStep_DepartmentId",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ApprovalRule");

            migrationBuilder.Sql("DELETE FROM \"ApprovalRule\" WHERE \"RoleId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "ApprovalRule",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
