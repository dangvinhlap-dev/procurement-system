using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát QT7 – cấu hình truy cập (màn hình CFG): bảng một dòng AccessPolicy (chính sách mật khẩu, khóa, hạn phiên, dải IP quản trị, giới hạn tần
    /// suất, mốc "Đăng xuất tất cả"). Chưa có dòng thì phần mềm dùng giá trị mặc định – không cần dữ liệu khởi tạo.
    /// </summary>
    public partial class V0_35_0_CauHinhTruyCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessPolicy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinPasswordLength = table.Column<int>(type: "integer", nullable: false),
                    BlockCommonPasswords = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraBlockedPasswords = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MaxFailedLogins = table.Column<int>(type: "integer", nullable: false),
                    LockoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    SessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    OpsAllowedIpRanges = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LoginPerMinutePerIp = table.Column<int>(type: "integer", nullable: false),
                    ApiPerMinutePerUser = table.Column<int>(type: "integer", nullable: false),
                    HeavyConcurrentPerUser = table.Column<int>(type: "integer", nullable: false),
                    SessionsRevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessPolicy", x => x.Id);
                });
        }

        /// <summary>Xóa bảng – mất cấu hình đã lưu trên CFG (bản cũ không đọc bảng này, dùng số cố định như trước 0.35.0).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessPolicy");
        }
    }
}
