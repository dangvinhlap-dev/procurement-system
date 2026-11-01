using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.10.0 – dữ liệu mua lịch sử nhập từ Excel (design.md mục 18.1, lát E1): bảng PurchaseHistoryBatch, PurchaseHistoryRecord.
    /// Down() xóa hai bảng – MẤT toàn bộ dữ liệu mua lịch sử đã nhập (file Excel gốc vẫn ở người dùng, nhập lại được); bảng khác không đổi.
    /// </summary>
    public partial class V0_10_0_DuLieuMuaLichSu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseHistoryBatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseHistoryBatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryBatch_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseHistoryRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Specification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Standard = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    RelatedCost = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    RelatedCostNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SupplierTaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SupplierContact = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Discount = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sponsorship = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OnTime = table.Column<bool>(type: "boolean", nullable: true),
                    QualityPassed = table.Column<bool>(type: "boolean", nullable: true),
                    Complaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SearchText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseHistoryRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryRecord_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryRecord_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryRecord_PurchaseHistoryBatch_BatchId",
                        column: x => x.BatchId,
                        principalTable: "PurchaseHistoryBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryRecord_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryBatch_CompanyId_CreatedAt",
                table: "PurchaseHistoryBatch",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_BatchId",
                table: "PurchaseHistoryRecord",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_CompanyId_PurchaseDate",
                table: "PurchaseHistoryRecord",
                columns: new[] { "CompanyId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_ItemId",
                table: "PurchaseHistoryRecord",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_SupplierId",
                table: "PurchaseHistoryRecord",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseHistoryRecord");

            migrationBuilder.DropTable(
                name: "PurchaseHistoryBatch");
        }
    }
}
