using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B4b – phụ lục hợp đồng nguyên tắc (design.md mục 18.7): bảng PurchaseContractAmendment (gia hạn, đổi bảng giá, đổi điều khoản;
    /// duyệt loại CTA). Bảng mới, không đổi dữ liệu cũ. Down() xóa bảng – mất các phụ lục đã lập (thay đổi đã áp vào hợp đồng vẫn giữ).
    /// </summary>
    public partial class V0_12_0_PhuLucHopDong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseContractAmendment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    No = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PaperNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SignDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OldValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    NewValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    OldPriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewPriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceAdjustmentTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    DeliveryTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DiscountTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SponsorshipTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PenaltyTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OtherTerms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseContractAmendment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseContractAmendment_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContractAmendment_PurchaseContract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "PurchaseContract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContractAmendment_SupplierPriceList_NewPriceListId",
                        column: x => x.NewPriceListId,
                        principalTable: "SupplierPriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContractAmendment_SupplierPriceList_OldPriceListId",
                        column: x => x.OldPriceListId,
                        principalTable: "SupplierPriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractAmendment_CompanyId",
                table: "PurchaseContractAmendment",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractAmendment_ContractId_Seq",
                table: "PurchaseContractAmendment",
                columns: new[] { "ContractId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractAmendment_NewPriceListId",
                table: "PurchaseContractAmendment",
                column: "NewPriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractAmendment_OldPriceListId",
                table: "PurchaseContractAmendment",
                column: "OldPriceListId");

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseContractAmendment_Open",
                table: "PurchaseContractAmendment",
                column: "ContractId",
                unique: true,
                filter: "\"Status\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseContractAmendment");
        }
    }
}
