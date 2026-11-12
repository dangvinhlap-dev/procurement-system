using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P3b-2 – hỏi đáp làm rõ RFQ trên Portal (design.md mục 19.5, P3-11…P3-14): bảng RfqClarification. Down() xóa bảng – **mất toàn bộ
    /// câu hỏi, câu trả lời, thông báo làm rõ**.
    /// </summary>
    public partial class V0_25_0_HoiDapBaoGiaPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RfqClarification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AskedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AskedByPortalUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AskedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnsweredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AnsweredByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqClarification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqClarification_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqClarification_Rfq_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqClarification_SupplierPortalUser_AskedByPortalUserId",
                        column: x => x.AskedByPortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqClarification_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqClarification_User_AnsweredBy",
                        column: x => x.AnsweredBy,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RfqClarification_AnsweredBy",
                table: "RfqClarification",
                column: "AnsweredBy");

            migrationBuilder.CreateIndex(
                name: "IX_RfqClarification_AskedByPortalUserId",
                table: "RfqClarification",
                column: "AskedByPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqClarification_CompanyId",
                table: "RfqClarification",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqClarification_RfqId",
                table: "RfqClarification",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqClarification_SupplierId",
                table: "RfqClarification",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RfqClarification");
        }
    }
}
