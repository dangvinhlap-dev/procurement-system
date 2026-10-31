using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.9.0 – đánh số danh mục tự động theo dải cấu hình (design.md mục 5.11): bảng CatalogNumbering.
    /// Down() xóa bảng – mất cấu hình dải (mã đã cấp không đổi); sau khi gỡ, phần mềm bản cũ cho người dùng tự nhập mã trở lại.
    /// </summary>
    public partial class V0_9_0_DanhSoDanhMuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogNumbering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Catalog = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Digits = table.Column<int>(type: "integer", nullable: false),
                    FromNumber = table.Column<long>(type: "bigint", nullable: false),
                    ToNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogNumbering", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogNumbering_ItemCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogNumbering_Catalog_CategoryId",
                table: "CatalogNumbering",
                columns: new[] { "Catalog", "CategoryId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogNumbering_CategoryId",
                table: "CatalogNumbering",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogNumbering");
        }
    }
}
