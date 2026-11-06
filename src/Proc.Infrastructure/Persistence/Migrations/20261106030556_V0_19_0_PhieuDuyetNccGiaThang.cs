using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát G2 (design.md mục 18.25): phiếu duyệt NCC và giá tháng – bảng MonthlySupplierApproval, MonthlySupplierApprovalCompany,
    /// MonthlySupplierApprovalLine. Bảng mới, không đụng dữ liệu cũ.
    /// </summary>
    public partial class V0_19_0_PhieuDuyetNccGiaThang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlySupplierApproval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacesId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySupplierApproval", x => x.Id);
                    table.CheckConstraint("CK_MonthlySupplierApproval_Period", "EXTRACT(DAY FROM \"Period\") = 1");
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApproval_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApproval_ItemCategory_ItemCategoryId",
                        column: x => x.ItemCategoryId,
                        principalTable: "ItemCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApproval_MonthlySupplierApproval_ReplacesId",
                        column: x => x.ReplacesId,
                        principalTable: "MonthlySupplierApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthlySupplierApprovalCompany",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySupplierApprovalCompany", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalCompany_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalCompany_MonthlySupplierApproval_Appr~",
                        column: x => x.ApprovalId,
                        principalTable: "MonthlySupplierApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlySupplierApprovalLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    MainSupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    BackupSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackupUnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    QuantityLimit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySupplierApprovalLine", x => x.Id);
                    table.CheckConstraint("CK_MonthlySupplierApprovalLine_Backup", "\"BackupSupplierId\" IS NULL OR \"BackupSupplierId\" <> \"MainSupplierId\"");
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalLine_MonthlySupplierApproval_Approva~",
                        column: x => x.ApprovalId,
                        principalTable: "MonthlySupplierApproval",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalLine_Supplier_BackupSupplierId",
                        column: x => x.BackupSupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalLine_Supplier_MainSupplierId",
                        column: x => x.MainSupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthlySupplierApprovalLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApproval_CompanyId_No",
                table: "MonthlySupplierApproval",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApproval_ItemCategoryId",
                table: "MonthlySupplierApproval",
                column: "ItemCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApproval_Period_Status",
                table: "MonthlySupplierApproval",
                columns: new[] { "Period", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApproval_ReplacesId",
                table: "MonthlySupplierApproval",
                column: "ReplacesId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalCompany_ApprovalId_CompanyId",
                table: "MonthlySupplierApprovalCompany",
                columns: new[] { "ApprovalId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalCompany_CompanyId",
                table: "MonthlySupplierApprovalCompany",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalLine_ApprovalId_ItemId",
                table: "MonthlySupplierApprovalLine",
                columns: new[] { "ApprovalId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalLine_BackupSupplierId",
                table: "MonthlySupplierApprovalLine",
                column: "BackupSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalLine_ItemId",
                table: "MonthlySupplierApprovalLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalLine_MainSupplierId",
                table: "MonthlySupplierApprovalLine",
                column: "MainSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySupplierApprovalLine_UomId",
                table: "MonthlySupplierApprovalLine",
                column: "UomId");
        }

        /// <inheritdoc />
        /// <remarks>Mất dữ liệu: xóa ba bảng, mất mọi phiếu duyệt NCC và giá tháng (AuditLog vẫn giữ).</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlySupplierApprovalCompany");

            migrationBuilder.DropTable(
                name: "MonthlySupplierApprovalLine");

            migrationBuilder.DropTable(
                name: "MonthlySupplierApproval");
        }
    }
}
