using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát Y8 (design.md 19.3 bước 7): đề nghị thanh toán 5A/5B (bảng PaymentRequest + dòng PaymentRequestLine theo XNH có hóa đơn). Down() xóa hai
    /// bảng – mất toàn bộ đề nghị thanh toán, kết quả duyệt (bước duyệt loại DNT còn trong ApprovalStep nhưng không còn phiếu) và ghi nhận Đã chi.
    /// </summary>
    public partial class V0_27_0_DeNghiThanhToan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConstructionProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    DeductionAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    DeductionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PayableAmount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_PaymentRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRequest_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequest_ConstructionProject_ConstructionProjectId_Co~",
                        columns: x => new { x.ConstructionProjectId, x.CompanyId },
                        principalTable: "ConstructionProject",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequest_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequest_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequest_Supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRequestLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ReceiptConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceSeries = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRequestLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRequestLine_GoodsReceiptConfirmation_ReceiptConfirma~",
                        column: x => x.ReceiptConfirmationId,
                        principalTable: "GoodsReceiptConfirmation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRequestLine_PaymentRequest_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PaymentRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_CompanyId_Date",
                table: "PaymentRequest",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_CompanyId_No",
                table: "PaymentRequest",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_ConstructionProjectId_CompanyId",
                table: "PaymentRequest",
                columns: new[] { "ConstructionProjectId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_DepartmentId_CompanyId",
                table: "PaymentRequest",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_PurchaseOrderId",
                table: "PaymentRequest",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequest_SupplierId",
                table: "PaymentRequest",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestLine_ReceiptConfirmationId",
                table: "PaymentRequestLine",
                column: "ReceiptConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestLine_RequestId_LineNo",
                table: "PaymentRequestLine",
                columns: new[] { "RequestId", "LineNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRequestLine");

            migrationBuilder.DropTable(
                name: "PaymentRequest");
        }
    }
}
