using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát O1: khối – Trưởng phòng ban – Giám đốc khối (design.md mục 19.3). Bảng Division (khối, Giám đốc khối), UserDepartment (bộ phận của
    /// người dùng tại công ty); bộ phận thêm cột DivisionId, ManagerUserId (null với dữ liệu cũ – không đổi dữ liệu đang có).
    /// Down() xóa hai bảng và hai cột – mất danh mục khối, Giám đốc khối, Trưởng phòng ban (tài khoản) và bộ phận của người dùng (AuditLog vẫn giữ).
    /// </summary>
    public partial class V0_22_0_KhoiPhongBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "Department",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerUserId",
                table: "Department",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Division",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DirectorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Division", x => x.Id);
                    table.UniqueConstraint("AK_Division_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_Division_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Division_User_DirectorUserId",
                        column: x => x.DirectorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartment_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDepartment_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDepartment_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Department_DivisionId_CompanyId",
                table: "Department",
                columns: new[] { "DivisionId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Department_ManagerUserId",
                table: "Department",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Division_CompanyId_Code",
                table: "Division",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Division_DirectorUserId",
                table: "Division",
                column: "DirectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartment_CompanyId",
                table: "UserDepartment",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartment_DepartmentId_CompanyId",
                table: "UserDepartment",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartment_UserId_CompanyId",
                table: "UserDepartment",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Department_Division_DivisionId_CompanyId",
                table: "Department",
                columns: new[] { "DivisionId", "CompanyId" },
                principalTable: "Division",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Department_User_ManagerUserId",
                table: "Department",
                column: "ManagerUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Department_Division_DivisionId_CompanyId",
                table: "Department");

            migrationBuilder.DropForeignKey(
                name: "FK_Department_User_ManagerUserId",
                table: "Department");

            migrationBuilder.DropTable(
                name: "Division");

            migrationBuilder.DropTable(
                name: "UserDepartment");

            migrationBuilder.DropIndex(
                name: "IX_Department_DivisionId_CompanyId",
                table: "Department");

            migrationBuilder.DropIndex(
                name: "IX_Department_ManagerUserId",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "ManagerUserId",
                table: "Department");
        }
    }
}
