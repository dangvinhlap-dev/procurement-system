using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.9.0 – tham số công ty (design.md mục 5.4): bảng CompanyParameter, cột số NCC tối thiểu khi gửi RFQ. Down() xóa bảng – mất
    /// các tham số đã sửa, mọi công ty quay về mặc định (3 NCC); chứng từ không đổi.
    /// </summary>
    public partial class V0_9_0_ThamSoCongTy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyParameter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqMinSuppliers = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyParameter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyParameter_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyParameter_CompanyId",
                table: "CompanyParameter",
                column: "CompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyParameter");
        }
    }
}
