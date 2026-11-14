using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Nhóm hàng tối đa 4 cấp (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-1, N4-2): thêm cột Level, tính từ cây hiện có, ràng buộc 1–4.
    /// Cây đang có nhánh sâu hơn 4 cấp thì dừng với thông báo để chuyển nhóm trước (dữ liệu hiện có sâu nhất 3 cấp).
    /// Down() chỉ bỏ ràng buộc và cột Level – không mất dữ liệu (cấp tính lại được từ ParentId).
    /// </summary>
    public partial class V0_28_0_NhomHangToiDa4Cap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "ItemCategory",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Cấp = số nhóm từ gốc xuống (nhóm gốc = 1), tính bằng truy vấn đệ quy trên ParentId.
            migrationBuilder.Sql("""
                WITH RECURSIVE tree AS (
                    SELECT "Id", 1 AS lvl FROM "ItemCategory" WHERE "ParentId" IS NULL
                    UNION ALL
                    SELECT c."Id", t.lvl + 1 FROM "ItemCategory" c JOIN tree t ON c."ParentId" = t."Id"
                )
                UPDATE "ItemCategory" c SET "Level" = t.lvl FROM tree t WHERE c."Id" = t."Id";

                DO $$
                DECLARE deep text;
                BEGIN
                    SELECT string_agg("Code", ', ') INTO deep FROM "ItemCategory" WHERE "Level" > 4;
                    IF deep IS NOT NULL THEN
                        RAISE EXCEPTION 'Nhóm hàng tối đa 4 cấp: các nhóm % đang ở cấp 5 trở xuống – chuyển các nhóm này lên trên rồi chạy lại.', deep;
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemCategory_Level",
                table: "ItemCategory",
                sql: "\"Level\" BETWEEN 1 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemCategory_Level",
                table: "ItemCategory");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "ItemCategory");
        }
    }
}
