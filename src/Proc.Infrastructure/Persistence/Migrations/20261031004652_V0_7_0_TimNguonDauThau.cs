using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.7.0 – tìm nguồn và đấu thầu (design.md mục 8.3, 8.4): bảng SourcingRequest, SourcingCandidate, Rfq, RfqLine, RfqSupplier,
    /// QuotationLine. Down() xóa các bảng – mất mọi phiếu tìm nguồn, RFQ, báo giá, kết quả chọn thầu (PO đã sinh vẫn giữ).
    /// </summary>
    public partial class V0_7_0_TimNguonDauThau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourcingRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Specification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Responsible = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcingRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcingRequest_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourcingRequest_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rfq",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    SourcingId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceWeight = table.Column<int>(type: "integer", nullable: false),
                    QualityWeight = table.Column<int>(type: "integer", nullable: false),
                    DeliveryWeight = table.Column<int>(type: "integer", nullable: false),
                    PaymentWeight = table.Column<int>(type: "integer", nullable: false),
                    WarrantyWeight = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AwardDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rfq", x => x.Id);
                    table.CheckConstraint("CK_Rfq_Weights", "\"PriceWeight\" + \"QualityWeight\" + \"DeliveryWeight\" + \"PaymentWeight\" + \"WarrantyWeight\" = 100");
                    table.ForeignKey(
                        name: "FK_Rfq_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rfq_SourcingRequest_SourcingId",
                        column: x => x.SourcingId,
                        principalTable: "SourcingRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourcingCandidate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcingCandidate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcingCandidate_SourcingRequest_SourcingId",
                        column: x => x.SourcingId,
                        principalTable: "SourcingRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourcingCandidate_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RfqLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SourcePrLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AwardedSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwardReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqLine_PurchaseRequestLine_SourcePrLineId",
                        column: x => x.SourcePrLineId,
                        principalTable: "PurchaseRequestLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqLine_Rfq_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RfqLine_Supplier_AwardedSupplierId",
                        column: x => x.AwardedSupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RfqSupplier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    HasQuoted = table.Column<bool>(type: "boolean", nullable: false),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QualityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DeliveryScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PaymentScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    WarrantyScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqSupplier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqSupplier_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqSupplier_Rfq_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RfqSupplier_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqSupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    VatCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLine_RfqLine_RfqLineId",
                        column: x => x.RfqLineId,
                        principalTable: "RfqLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuotationLine_RfqSupplier_RfqSupplierId",
                        column: x => x.RfqSupplierId,
                        principalTable: "RfqSupplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLine_RfqLineId",
                table: "QuotationLine",
                column: "RfqLineId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLine_RfqSupplierId_RfqLineId",
                table: "QuotationLine",
                columns: new[] { "RfqSupplierId", "RfqLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rfq_CompanyId_No",
                table: "Rfq",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rfq_CompanyId_Status",
                table: "Rfq",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Rfq_SourcingId",
                table: "Rfq",
                column: "SourcingId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLine_AwardedSupplierId",
                table: "RfqLine",
                column: "AwardedSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLine_ItemId",
                table: "RfqLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLine_RfqId_LineNo",
                table: "RfqLine",
                columns: new[] { "RfqId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqLine_SourcePrLineId",
                table: "RfqLine",
                column: "SourcePrLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLine_UomId",
                table: "RfqLine",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqSupplier_PurchaseOrderId",
                table: "RfqSupplier",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqSupplier_RfqId_SupplierId",
                table: "RfqSupplier",
                columns: new[] { "RfqId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfqSupplier_SupplierId",
                table: "RfqSupplier",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SourcingCandidate_SourcingId_SupplierId",
                table: "SourcingCandidate",
                columns: new[] { "SourcingId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourcingCandidate_SupplierId",
                table: "SourcingCandidate",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SourcingRequest_CompanyId_No",
                table: "SourcingRequest",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourcingRequest_ItemId",
                table: "SourcingRequest",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationLine");

            migrationBuilder.DropTable(
                name: "SourcingCandidate");

            migrationBuilder.DropTable(
                name: "RfqLine");

            migrationBuilder.DropTable(
                name: "RfqSupplier");

            migrationBuilder.DropTable(
                name: "Rfq");

            migrationBuilder.DropTable(
                name: "SourcingRequest");
        }
    }
}
