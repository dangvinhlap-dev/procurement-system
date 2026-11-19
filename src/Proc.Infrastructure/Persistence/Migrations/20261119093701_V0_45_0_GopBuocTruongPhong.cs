using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.45.0 – gộp bước người lập với Trưởng phòng ban (design.md mục 19.3, người dùng chốt 19/11/2026): bảng Phó phòng ban của bộ phận
    /// và cờ "gộp với người lập" trên bước duyệt.
    /// </summary>
    public partial class V0_45_0_GopBuocTruongPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MergedWithCreator",
                table: "ApprovalStep",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DepartmentDeputy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentDeputy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentDeputy_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentDeputy_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentDeputy_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDeputy_CompanyId",
                table: "DepartmentDeputy",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDeputy_DepartmentId_CompanyId",
                table: "DepartmentDeputy",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDeputy_DepartmentId_UserId",
                table: "DepartmentDeputy",
                columns: new[] { "DepartmentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDeputy_UserId",
                table: "DepartmentDeputy",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: danh sách Phó phòng ban và dấu "gộp với người lập" của bước duyệt (bước vẫn còn Đã duyệt, ý kiến ghi "Gộp với người lập …").
            migrationBuilder.DropTable(
                name: "DepartmentDeputy");

            migrationBuilder.DropColumn(
                name: "MergedWithCreator",
                table: "ApprovalStep");
        }
    }
}
