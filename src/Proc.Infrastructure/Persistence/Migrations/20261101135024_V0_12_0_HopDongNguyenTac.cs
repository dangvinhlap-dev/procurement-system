using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B4a – hợp đồng nguyên tắc phương án 2 (design.md mục 18.7): bảng PurchaseContract (đầu, điều khoản, hiệu lực, duyệt),
    /// PurchaseContractLine (sản lượng dự kiến), PurchaseContractDepartment (bộ phận được phép đặt). Bảng mới, không đổi dữ liệu cũ.
    /// Down() xóa các bảng này – mất toàn bộ hợp đồng nguyên tắc đã lập.
    /// </summary>
    public partial class V0_12_0_HopDongNguyenTac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseContract",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PriceAdjustmentTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    DeliveryTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DiscountTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SponsorshipTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PenaltyTerms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OtherTerms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovalRound = table.Column<int>(type: "integer", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseContract", x => x.Id);
                    table.CheckConstraint("CK_PurchaseContract_Validity", "\"ValidTo\" >= \"ValidFrom\"");
                    table.ForeignKey(
                        name: "FK_PurchaseContract_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContract_SupplierPriceList_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "SupplierPriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContract_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseContractDepartment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseContractDepartment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseContractDepartment_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContractDepartment_PurchaseContract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "PurchaseContract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseContractLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinCommitQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseContractLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseContractLine_Item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseContractLine_PurchaseContract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "PurchaseContract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseContractLine_Uom_UomId",
                        column: x => x.UomId,
                        principalTable: "Uom",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContract_CompanyId_No",
                table: "PurchaseContract",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContract_CompanyId_SupplierId_Status",
                table: "PurchaseContract",
                columns: new[] { "CompanyId", "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContract_PriceListId",
                table: "PurchaseContract",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContract_SupplierId",
                table: "PurchaseContract",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractDepartment_ContractId_DepartmentId",
                table: "PurchaseContractDepartment",
                columns: new[] { "ContractId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractDepartment_DepartmentId",
                table: "PurchaseContractDepartment",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractLine_ContractId_ItemId_UomId",
                table: "PurchaseContractLine",
                columns: new[] { "ContractId", "ItemId", "UomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractLine_ItemId",
                table: "PurchaseContractLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractLine_UomId",
                table: "PurchaseContractLine",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseContractDepartment");

            migrationBuilder.DropTable(
                name: "PurchaseContractLine");

            migrationBuilder.DropTable(
                name: "PurchaseContract");
        }
    }
}
