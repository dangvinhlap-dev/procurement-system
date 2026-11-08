using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G6a (design.md mục 18.25): bảng thỏa thuận chiết khấu bậc sản lượng gộp VolumeRebateAgreement và các bảng con (công ty áp dụng,
    /// bậc, dòng mặt hàng). Bảng mới, không đổi bảng cũ.
    /// </summary>
    public partial class V0_20_0_ChietKhauSanLuongGop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VolumeRebateAgreement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    TargetTierNo = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateAgreement", x => x.Id);
                    table.CheckConstraint("CK_VolumeRebateAgreement_Validity", "\"ValidTo\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_VolumeRebateAgreement_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateAgreement_ItemCategory_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateAgreement_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolumeRebateCompany",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateCompany", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeRebateCompany_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateCompany_VolumeRebateAgreement_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "VolumeRebateAgreement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VolumeRebateLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeRebateLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolumeRebateLine_VolumeRebateAgreement_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "VolumeRebateAgreement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VolumeRebateTier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierNo = table.Column<int>(type: "integer", nullable: false),
                    FromValue = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeRebateTier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeRebateTier_VolumeRebateAgreement_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "VolumeRebateAgreement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateAgreement_CompanyId_No",
                table: "VolumeRebateAgreement",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateAgreement_ItemCategoryId",
                table: "VolumeRebateAgreement",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateAgreement_SupplierId_Status",
                table: "VolumeRebateAgreement",
                columns: new[] { "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateCompany_AgreementId_CompanyId",
                table: "VolumeRebateCompany",
                columns: new[] { "AgreementId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateCompany_CompanyId",
                table: "VolumeRebateCompany",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateLine_AgreementId_ItemId",
                table: "VolumeRebateLine",
                columns: new[] { "AgreementId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateLine_ItemId",
                table: "VolumeRebateLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateLine_UomId",
                table: "VolumeRebateLine",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeRebateTier_AgreementId_TierNo",
                table: "VolumeRebateTier",
                columns: new[] { "AgreementId", "TierNo" },
                unique: true);
        }

        /// <summary>Gỡ: xóa 4 bảng – <b>mất toàn bộ thỏa thuận chiết khấu sản lượng gộp</b> đã lập (AuditLog vẫn giữ lịch sử).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolumeRebateCompany");

            migrationBuilder.DropTable(
                name: "VolumeRebateLine");

            migrationBuilder.DropTable(
                name: "VolumeRebateTier");

            migrationBuilder.DropTable(
                name: "VolumeRebateAgreement");
        }
    }
}
