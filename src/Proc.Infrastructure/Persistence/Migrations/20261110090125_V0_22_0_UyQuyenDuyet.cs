using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát O2 (design.md mục 19.3): bảng ủy quyền duyệt có thời hạn và cột "duyệt thay cho" trên bước duyệt.
    /// </summary>
    public partial class V0_22_0_UyQuyenDuyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OnBehalfOf",
                table: "ApprovalStep",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalDelegation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegation_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegation_User_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegation_User_DelegatorUserId",
                        column: x => x.DelegatorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegation_User_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStep_OnBehalfOf",
                table: "ApprovalStep",
                column: "OnBehalfOf");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegation_CompanyId_DelegateUserId",
                table: "ApprovalDelegation",
                columns: new[] { "CompanyId", "DelegateUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegation_CompanyId_DelegatorUserId",
                table: "ApprovalDelegation",
                columns: new[] { "CompanyId", "DelegatorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegation_DelegateUserId",
                table: "ApprovalDelegation",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegation_DelegatorUserId",
                table: "ApprovalDelegation",
                column: "DelegatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegation_RevokedBy",
                table: "ApprovalDelegation",
                column: "RevokedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalStep_User_OnBehalfOf",
                table: "ApprovalStep",
                column: "OnBehalfOf",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// MẤT DỮ LIỆU: xóa toàn bộ ủy quyền duyệt và dấu "duyệt thay cho" trên lịch sử duyệt (người duyệt thực vẫn giữ ở cột ActedBy).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalStep_User_OnBehalfOf",
                table: "ApprovalStep");

            migrationBuilder.DropTable(
                name: "ApprovalDelegation");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalStep_OnBehalfOf",
                table: "ApprovalStep");

            migrationBuilder.DropColumn(
                name: "OnBehalfOf",
                table: "ApprovalStep");
        }
    }
}
