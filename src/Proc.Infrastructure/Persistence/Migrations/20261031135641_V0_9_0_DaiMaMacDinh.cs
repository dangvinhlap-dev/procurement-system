using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.9.0 – dải đánh số danh mục mặc định (design.md mục 5.11). Máy chạy thật không nạp dữ liệu mẫu nên sau khi nâng cấp bảng
    /// CatalogNumbering trống, mọi người bị chặn thêm NCC, nhóm NCC, nhóm hàng, hàng hóa; migration này tạo sẵn một dải mặc định cho từng
    /// danh mục (người dùng sửa/xóa tùy ý ở màn hình DSDM). Chỉ chạy một lần: dải xóa sau này không bị tạo lại.
    /// Down() chỉ xóa các dải do migration này tạo và chưa được sửa – không mất cấu hình người dùng đã chỉnh.
    /// </summary>
    public partial class V0_9_0_DaiMaMacDinh : Migration
    {
        /// <summary>
        /// Thêm dải mặc định cho danh mục chưa có dải mặc định (CategoryId NULL); danh mục đã có thì giữ nguyên. Id cố định để Down() nhận ra.
        /// Ví dụ: NCC + 6 chữ số, 1 → 999999 → mã đầu NCC000001; hàng hóa {NHOM}- + 5 chữ số → VT-00001 với nhóm VT.
        /// Ghi bằng SQL trong migration nên không vào AuditLog (dữ liệu khởi tạo, không phải thao tác người dùng).
        /// </summary>
        public const string InsertDefaultsSql = """
            INSERT INTO "CatalogNumbering" ("Id", "Catalog", "CategoryId", "Prefix", "Digits", "FromNumber", "ToNumber", "CreatedAt")
            SELECT v.id, v.catalog, NULL, v.prefix, v.digits, 1, v.to_number, now()
            FROM (VALUES
                ('0199a000-0000-7000-8000-000000000001'::uuid, 'supplier', 'NCC', 6, 999999::bigint),
                ('0199a000-0000-7000-8000-000000000002'::uuid, 'supplier-group', 'GN', 4, 9999::bigint),
                ('0199a000-0000-7000-8000-000000000003'::uuid, 'item-category', 'NH', 6, 999999::bigint),
                ('0199a000-0000-7000-8000-000000000004'::uuid, 'item', '{NHOM}-', 5, 99999::bigint)
            ) AS v(id, catalog, prefix, digits, to_number)
            WHERE NOT EXISTS (SELECT 1 FROM "CatalogNumbering" c WHERE c."Catalog" = v.catalog AND c."CategoryId" IS NULL)
              AND NOT EXISTS (SELECT 1 FROM "CatalogNumbering" c WHERE c."Id" = v.id);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InsertDefaultsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dải người dùng đã sửa (UpdatedAt có giá trị) được giữ lại.
            migrationBuilder.Sql("""
                DELETE FROM "CatalogNumbering"
                WHERE "UpdatedAt" IS NULL AND "Id" IN (
                    '0199a000-0000-7000-8000-000000000001', '0199a000-0000-7000-8000-000000000002',
                    '0199a000-0000-7000-8000-000000000003', '0199a000-0000-7000-8000-000000000004');
                """);
        }
    }
}
