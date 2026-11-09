using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0_20_0_YKienThamDinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppraisalOpinion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Aspect = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsWithdrawn = table.Column<bool>(type: "boolean", nullable: false),
                    WithdrawReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalOpinion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalOpinion_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOpinion_CompanyId",
                table: "AppraisalOpinion",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalOpinion_OwnerType_OwnerId",
                table: "AppraisalOpinion",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        /// <remarks>Gỡ migration xóa bảng AppraisalOpinion – mất các ý kiến thẩm định đã ghi (AuditLog vẫn giữ lịch sử).</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalOpinion");
        }
    }
}
