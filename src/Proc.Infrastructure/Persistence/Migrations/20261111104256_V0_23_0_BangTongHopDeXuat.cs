using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y5b (design.md mục 19.3 bước 2): bảng tổng hợp đề xuất mua 2A/2B và dòng (dòng đề nghị đã duyệt + đơn vị mua). Down xóa hai bảng – mất
    /// toàn bộ bảng tổng hợp đã lập (bước duyệt của chúng còn trong ApprovalStep nhưng không còn phiếu).
    /// </summary>
    public partial class V0_23_0_BangTongHopDeXuat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseSummary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConstructionProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    BudgetUsedAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    IsOverBudget = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseSummary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseSummary_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummary_ConstructionProject_ConstructionProjectId_C~",
                        columns: x => new { x.ConstructionProjectId, x.CompanyId },
                        principalTable: "ConstructionProject",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummary_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseSummaryLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SummaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BaseQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    NeededDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchasingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseSummaryLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_PurchaseRequestLine_RequestLineId",
                        column: x => x.RequestLineId,
                        principalTable: "PurchaseRequestLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_PurchaseRequest_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PurchaseRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_PurchaseSummary_SummaryId",
                        column: x => x.SummaryId,
                        principalTable: "PurchaseSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_PurchasingUnit_PurchasingUnitId_Company~",
                        columns: x => new { x.PurchasingUnitId, x.CompanyId },
                        principalTable: "PurchasingUnit",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseSummaryLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummary_CompanyId_Date",
                table: "PurchaseSummary",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummary_CompanyId_No",
                table: "PurchaseSummary",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummary_ConstructionProjectId_CompanyId",
                table: "PurchaseSummary",
                columns: new[] { "ConstructionProjectId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummary_DepartmentId_CompanyId",
                table: "PurchaseSummary",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_CompanyId",
                table: "PurchaseSummaryLine",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_ItemId",
                table: "PurchaseSummaryLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_PurchasingUnitId_CompanyId",
                table: "PurchaseSummaryLine",
                columns: new[] { "PurchasingUnitId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_RequestId",
                table: "PurchaseSummaryLine",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_RequestLineId",
                table: "PurchaseSummaryLine",
                column: "RequestLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_SummaryId_LineNo",
                table: "PurchaseSummaryLine",
                columns: new[] { "SummaryId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSummaryLine_UomId",
                table: "PurchaseSummaryLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseSummaryLine");

            migrationBuilder.DropTable(
                name: "PurchaseSummary");
        }
    }
}
