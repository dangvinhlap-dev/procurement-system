using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.1.0 – nhật ký thao tác (AuditLog, phân vùng theo tháng) và nhật ký truy cập (AccessLog), chỉ cho thêm.
    /// Lưu ý: Down() xóa hai bảng nhật ký, MẤT toàn bộ nhật ký đã ghi – sao lưu trước khi gỡ.
    /// </summary>
    public partial class V0_1_0_NhatKyThaoTac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Event = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLog", x => x.Id);
                });

            // AuditLog phân vùng theo tháng (UTC) của OccurredAt để giữ nhiều năm mà vẫn truy vấn nhanh (design.md mục 9.3).
            // EF Core không tạo được bảng phân vùng nên viết SQL; cột khớp AuditLogConfiguration.
            // Khóa chính phải chứa cột phân vùng nên là (Id, OccurredAt).
            migrationBuilder.Sql("""
                CREATE TABLE "AuditLog" (
                    "Id" uuid NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "UserId" uuid NULL,
                    "UserName" character varying(256) NULL,
                    "CompanyId" uuid NULL,
                    "IpAddress" character varying(64) NULL,
                    "UserAgent" character varying(512) NULL,
                    "CorrelationId" character varying(128) NULL,
                    "EntityType" character varying(100) NOT NULL,
                    "EntityId" character varying(200) NOT NULL,
                    "EntityLabel" character varying(256) NULL,
                    "DocumentNumber" character varying(50) NULL,
                    "Action" character varying(30) NOT NULL,
                    "Changes" jsonb NOT NULL,
                    CONSTRAINT "PK_AuditLog" PRIMARY KEY ("Id", "OccurredAt")
                ) PARTITION BY RANGE ("OccurredAt");

                -- Phân vùng mặc định hứng dòng ngoài các tháng đã tạo, để ghi nhật ký không bao giờ lỗi vì thiếu phân vùng.
                CREATE TABLE "AuditLog_default" PARTITION OF "AuditLog" DEFAULT;

                -- Tạo phân vùng cho tháng hiện tại và months_ahead tháng tới, tên AuditLog_yyyy_MM.
                -- Gọi khi khởi động API (DatabaseInitializer); gọi lại nhiều lần không sao.
                CREATE OR REPLACE FUNCTION audit_log_ensure_partitions(months_ahead integer) RETURNS void
                LANGUAGE plpgsql AS $$
                DECLARE
                    first_day timestamp := date_trunc('month', now() AT TIME ZONE 'UTC');
                    month_start timestamp;
                    partition_name text;
                BEGIN
                    FOR i IN 0..months_ahead LOOP
                        month_start := first_day + make_interval(months => i);
                        partition_name := 'AuditLog_' || to_char(month_start, 'YYYY_MM');
                        IF to_regclass(quote_ident(partition_name)) IS NULL THEN
                            EXECUTE format(
                                'CREATE TABLE %I PARTITION OF "AuditLog" FOR VALUES FROM (%L) TO (%L)',
                                partition_name,
                                (month_start AT TIME ZONE 'UTC')::timestamptz,
                                ((month_start + interval '1 month') AT TIME ZONE 'UTC')::timestamptz);
                        END IF;
                    END LOOP;
                END $$;

                SELECT audit_log_ensure_partitions(2);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_CompanyId_OccurredAt",
                table: "AccessLog",
                columns: new[] { "CompanyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_OccurredAt",
                table: "AccessLog",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_UserId_OccurredAt",
                table: "AccessLog",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CompanyId_OccurredAt",
                table: "AuditLog",
                columns: new[] { "CompanyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_DocumentNumber",
                table: "AuditLog",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityType_EntityId",
                table: "AuditLog",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId_OccurredAt",
                table: "AuditLog",
                columns: new[] { "UserId", "OccurredAt" });

            // Nhật ký chỉ được thêm: chặn UPDATE/DELETE bằng trigger, kể cả với chủ sở hữu bảng (CLAUDE.md mục 4).
            // TRUNCATE không bị chặn (không có trigger theo dòng) – chỉ dùng để làm sạch CSDL test.
            // Trên server thật còn thu quyền UPDATE/DELETE của tài khoản ứng dụng (design.md mục 9.3, 14).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION log_prevent_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Bảng % chỉ được thêm, không được sửa hoặc xóa (design.md mục 9.3).', TG_TABLE_NAME;
                END $$;

                CREATE TRIGGER "TR_AuditLog_ChiThem" BEFORE UPDATE OR DELETE ON "AuditLog"
                    FOR EACH ROW EXECUTE FUNCTION log_prevent_change();
                CREATE TRIGGER "TR_AccessLog_ChiThem" BEFORE UPDATE OR DELETE ON "AccessLog"
                    FOR EACH ROW EXECUTE FUNCTION log_prevent_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLog");

            // Xóa bảng nhật ký kéo theo mọi phân vùng và trigger.
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS audit_log_ensure_partitions(integer);
                DROP FUNCTION IF EXISTS log_prevent_change();
                """);
        }
    }
}
