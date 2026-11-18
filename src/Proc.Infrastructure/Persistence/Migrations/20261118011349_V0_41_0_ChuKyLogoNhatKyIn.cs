using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát IN1 (design.md mục 20.4): bảng chữ ký người dùng theo phiên bản, logo công ty, nhật ký in chứng từ; cột danh xưng của người dùng.
    /// </summary>
    public partial class V0_41_0_ChuKyLogoNhatKyIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Honorific",
                table: "User",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyLogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyLogo_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentPrintLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintNo = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrintedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentPrintLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentPrintLog_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSignature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ByAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetiredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RetireReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSignature", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSignature_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLogo_CompanyId",
                table: "CompanyLogo",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentPrintLog_CompanyId",
                table: "DocumentPrintLog",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentPrintLog_DocType_DocumentId_PrintNo",
                table: "DocumentPrintLog",
                columns: new[] { "DocType", "DocumentId", "PrintNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSignature_UserId_Current",
                table: "UserSignature",
                column: "UserId",
                unique: true,
                filter: "\"ValidTo\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSignature_UserId_VersionNo",
                table: "UserSignature",
                columns: new[] { "UserId", "VersionNo" },
                unique: true);
        }

        /// <summary>
        /// Gỡ lát IN1. <b>Mất dữ liệu</b>: mọi chữ ký, logo công ty đã tải, nhật ký in và danh xưng của người dùng (mức quay về 2 – kèm
        /// script db/scripts/down/0.41.0.sql).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyLogo");

            migrationBuilder.DropTable(
                name: "DocumentPrintLog");

            migrationBuilder.DropTable(
                name: "UserSignature");

            migrationBuilder.DropColumn(
                name: "Honorific",
                table: "User");
        }
    }
}
