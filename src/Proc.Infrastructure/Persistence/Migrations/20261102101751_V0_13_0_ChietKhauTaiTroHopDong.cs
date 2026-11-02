using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát F4b (design.md mục 18.21): bảng PurchaseContractRebate – cam kết chiết khấu doanh số/tài trợ trên hợp đồng, ContractRebateReceipt –
    /// thực nhận. Down() xóa hai bảng – mất cam kết và các lần ghi nhận thực nhận (lịch sử thay đổi vẫn còn trong AuditLog).
    /// </summary>
    public partial class V0_13_0_ChietKhauTaiTroHopDong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseContractRebate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    MinPurchaseAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: true),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseContractRebate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseContractRebate_PurchaseContract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "PurchaseContract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractRebateReceipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    RebateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    RefNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRebateReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRebateReceipt_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractRebateReceipt_PurchaseContractRebate_RebateId",
                        column: x => x.RebateId,
                        principalTable: "PurchaseContractRebate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractRebateReceipt_PurchaseContract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "PurchaseContract",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRebateReceipt_CompanyId",
                table: "ContractRebateReceipt",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRebateReceipt_ContractId_RebateId_PeriodFrom",
                table: "ContractRebateReceipt",
                columns: new[] { "ContractId", "RebateId", "PeriodFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRebateReceipt_RebateId",
                table: "ContractRebateReceipt",
                column: "RebateId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseContractRebate_ContractId_LineNo",
                table: "PurchaseContractRebate",
                columns: new[] { "ContractId", "LineNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractRebateReceipt");

            migrationBuilder.DropTable(
                name: "PurchaseContractRebate");
        }
    }
}
