using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0_28_0_DeNghiCapNhatHoSoNcc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierProfileRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FieldChangesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    BankAccountsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BankChangeRequestNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProfileRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProfileRequest_SupplierPortalUser_PortalUserId",
                        column: x => x.PortalUserId,
                        principalTable: "SupplierPortalUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProfileRequest_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfileRequest_No",
                table: "SupplierProfileRequest",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfileRequest_PortalUserId",
                table: "SupplierProfileRequest",
                column: "PortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfileRequest_SupplierId_Pending",
                table: "SupplierProfileRequest",
                column: "SupplierId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProfileRequest_SupplierId_Status",
                table: "SupplierProfileRequest",
                columns: new[] { "SupplierId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: xóa mọi đề nghị cập nhật hồ sơ NCC gửi từ Portal (lát P2a). Thay đổi đã chấp nhận vẫn còn trên NCC; file kèm
            // (OwnerType SupplierProfileFile) còn lại nhưng không còn đề nghị chủ.
            migrationBuilder.DropTable(
                name: "SupplierProfileRequest");
        }
    }
}
