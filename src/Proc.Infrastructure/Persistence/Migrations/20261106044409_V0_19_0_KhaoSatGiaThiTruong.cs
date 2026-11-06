using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G4a (design.md mục 18.25): bảng phiếu khảo sát giá thị trường – ngân hàng giá (MarketPriceSurvey, MarketPriceSurveyLine). Bảng mới;
    /// Down() xóa hai bảng – mất các phiếu khảo sát giá (AuditLog vẫn giữ).
    /// </summary>
    public partial class V0_19_0_KhaoSatGiaThiTruong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketPriceSurvey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SurveyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceSurvey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketPriceSurvey_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketPriceSurvey_ItemCategory_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceSurveyLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceSurveyLine", x => x.Id);
                    table.CheckConstraint("CK_MarketPriceSurveyLine_Source", "\"SupplierId\" IS NOT NULL OR \"SourceName\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_MarketPriceSurveyLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketPriceSurveyLine_MarketPriceSurvey_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "MarketPriceSurvey",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketPriceSurveyLine_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketPriceSurveyLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurvey_CompanyId_No",
                table: "MarketPriceSurvey",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurvey_ItemCategoryId",
                table: "MarketPriceSurvey",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurvey_SurveyDate_Status",
                table: "MarketPriceSurvey",
                columns: new[] { "SurveyDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurveyLine_ItemId",
                table: "MarketPriceSurveyLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurveyLine_SupplierId",
                table: "MarketPriceSurveyLine",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurveyLine_SurveyId",
                table: "MarketPriceSurveyLine",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSurveyLine_UomId",
                table: "MarketPriceSurveyLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketPriceSurveyLine");

            migrationBuilder.DropTable(
                name: "MarketPriceSurvey");
        }
    }
}
