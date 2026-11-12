using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát P3b-3 – thư báo NCC qua Portal (design.md mục 19.5, P3-15…P3-18): bảng SupplierPortalEmail (hàng chờ thư mời, gia hạn, hủy, trả
    /// lời, thông báo RFQ). Down() xóa bảng – **mất dấu vết các thư đã báo NCC** (thư chờ gửi không gửi nữa).
    /// </summary>
    public partial class V0_25_0_EmailPortalNcc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierPortalEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DocType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DocId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPortalEmail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPortalEmail_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPortalEmail_SupplierPortalUser_PortalUserId",
                        column: x => x.PortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPortalEmail_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalEmail_CompanyId",
                table: "SupplierPortalEmail",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalEmail_DocType_DocId",
                table: "SupplierPortalEmail",
                columns: new[] { "DocType", "DocId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalEmail_PortalUserId",
                table: "SupplierPortalEmail",
                column: "PortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalEmail_State_CreatedAt",
                table: "SupplierPortalEmail",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalEmail_SupplierId",
                table: "SupplierPortalEmail",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierPortalEmail");
        }
    }
}
