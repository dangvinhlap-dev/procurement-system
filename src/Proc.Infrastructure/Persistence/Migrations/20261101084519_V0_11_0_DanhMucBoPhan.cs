using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bảng Department – bộ phận theo công ty; PurchaseRequest.DepartmentId trỏ tới bộ phận cùng công ty (design.md mục 18.5, lát A2).
    /// Cột chữ cũ PurchaseRequest.Department giữ nguyên (ngừng dùng, xóa ở bản sau – CLAUDE.md mục 8).
    /// </summary>
    public partial class V0_11_0_DanhMucBoPhan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "PurchaseRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Department",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManagerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.Id);
                    table.UniqueConstraint("AK_Department_Id_CompanyId", x => new { x.Id, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_Department_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequest_DepartmentId_CompanyId",
                table: "PurchaseRequest",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Department_CompanyId_Code",
                table: "Department",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequest_Department_DepartmentId_CompanyId",
                table: "PurchaseRequest",
                columns: new[] { "DepartmentId", "CompanyId" },
                principalTable: "Department",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Xóa bảng Department và cột PurchaseRequest.DepartmentId. MẤT DỮ LIỆU: mất danh mục bộ phận và bộ phận đã chọn trên PR
        /// (chữ bộ phận ghi trước lát A2 vẫn còn).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequest_Department_DepartmentId_CompanyId",
                table: "PurchaseRequest");

            migrationBuilder.DropTable(
                name: "Department");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequest_DepartmentId_CompanyId",
                table: "PurchaseRequest");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "PurchaseRequest");
        }
    }
}
