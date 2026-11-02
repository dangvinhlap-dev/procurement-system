using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B5b (design.md mục 18.9): bảng SoleSourceApproval – phê duyệt NCC độc quyền (NCC × mặt hàng, giải trình, hạn đánh giá lại).
    /// </summary>
    public partial class V0_12_0_NccDocQuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoleSourceApproval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RenewedFromId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoleSourceApproval", x => x.Id);
                    table.CheckConstraint("CK_SoleSourceApproval_Review", "\"ReviewDate\" > \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_SoleSourceApproval_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoleSourceApproval_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoleSourceApproval_SoleSourceApproval_RenewedFromId",
                        column: x => x.RenewedFromId,
                        principalTable: "SoleSourceApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoleSourceApproval_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoleSourceApproval_CompanyId_No",
                table: "SoleSourceApproval",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoleSourceApproval_CompanyId_SupplierId_ItemId",
                table: "SoleSourceApproval",
                columns: new[] { "CompanyId", "SupplierId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SoleSourceApproval_ItemId",
                table: "SoleSourceApproval",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SoleSourceApproval_RenewedFromId",
                table: "SoleSourceApproval",
                column: "RenewedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_SoleSourceApproval_SupplierId",
                table: "SoleSourceApproval",
                column: "SupplierId");
        }

        /// <summary>Gỡ bảng SoleSourceApproval – MẤT DỮ LIỆU: mọi phiếu NCC độc quyền đã lập (AuditLog vẫn giữ).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoleSourceApproval");
        }
    }
}
