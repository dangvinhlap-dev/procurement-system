using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bản 0.10.0 – ánh xạ dữ liệu mua lịch sử (design.md mục 18.2, lát E2): bảng PurchaseHistoryItemMapping,
    /// PurchaseHistorySupplierMapping và chỉ mục theo mã cũ/MST trên PurchaseHistoryRecord.
    /// Down() xóa hai bảng ánh xạ – MẤT các ánh xạ đã lưu; các dòng mua lịch sử giữ nguyên mặt hàng/NCC đã gán.
    /// </summary>
    public partial class V0_10_0_AnhXaDuLieuMua : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseHistoryItemMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseHistoryItemMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryItemMapping_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHistoryItemMapping_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseHistorySupplierMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(310)", maxLength: 310, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseHistorySupplierMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseHistorySupplierMapping_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHistorySupplierMapping_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_CompanyId_ItemCode",
                table: "PurchaseHistoryRecord",
                columns: new[] { "CompanyId", "ItemCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryRecord_CompanyId_SupplierTaxCode",
                table: "PurchaseHistoryRecord",
                columns: new[] { "CompanyId", "SupplierTaxCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryItemMapping_CompanyId_SourceCode",
                table: "PurchaseHistoryItemMapping",
                columns: new[] { "CompanyId", "SourceCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistoryItemMapping_ItemId",
                table: "PurchaseHistoryItemMapping",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistorySupplierMapping_CompanyId_SourceKey",
                table: "PurchaseHistorySupplierMapping",
                columns: new[] { "CompanyId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistorySupplierMapping_SupplierId",
                table: "PurchaseHistorySupplierMapping",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseHistoryItemMapping");

            migrationBuilder.DropTable(
                name: "PurchaseHistorySupplierMapping");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHistoryRecord_CompanyId_ItemCode",
                table: "PurchaseHistoryRecord");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHistoryRecord_CompanyId_SupplierTaxCode",
                table: "PurchaseHistoryRecord");
        }
    }
}
