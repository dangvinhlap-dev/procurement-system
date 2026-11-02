using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát F3 (design.md mục 18.19): bảng ControlFinding – sổ vi phạm Kiểm soát nội bộ. Down() xóa bảng – mất mọi vi phạm đã ghi nhận
    /// (lịch sử thay đổi vẫn còn trong AuditLog).
    /// </summary>
    public partial class V0_13_0_GhiNhanViPham : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlFinding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    AnomalyCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RefScreen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Conclusion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConcludedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcludedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlFinding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlFinding_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlFinding_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlFinding_CompanyId_Date",
                table: "ControlFinding",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlFinding_CompanyId_No",
                table: "ControlFinding",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlFinding_SupplierId",
                table: "ControlFinding",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlFinding");
        }
    }
}
