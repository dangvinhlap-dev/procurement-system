using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0_27_0_BangKeTapVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SundryExpenseSheet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SundryExpenseSheet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SundryExpenseSheet_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SundryExpenseSheet_Department_DepartmentId_CompanyId",
                        columns: x => new { x.DepartmentId, x.CompanyId },
                        principalTable: "Department",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SundryExpenseLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SundryExpenseLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SundryExpenseLine_SundryExpenseSheet_SheetId",
                        column: x => x.SheetId,
                        principalTable: "SundryExpenseSheet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SundryExpenseLine_SheetId_LineNo",
                table: "SundryExpenseLine",
                columns: new[] { "SheetId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_SundryExpenseSheet_CompanyId_Date",
                table: "SundryExpenseSheet",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SundryExpenseSheet_CompanyId_No",
                table: "SundryExpenseSheet",
                columns: new[] { "CompanyId", "No" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SundryExpenseSheet_DepartmentId_CompanyId",
                table: "SundryExpenseSheet",
                columns: new[] { "DepartmentId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_SundryExpenseSheet_DepartmentId_Date",
                table: "SundryExpenseSheet",
                columns: new[] { "DepartmentId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: xóa hai bảng – mất mọi bảng kê chi phí tạp vụ 2C và dòng của chúng (bước duyệt, đính kèm còn lại nhưng không còn phiếu chủ).
            migrationBuilder.DropTable(
                name: "SundryExpenseLine");

            migrationBuilder.DropTable(
                name: "SundryExpenseSheet");
        }
    }
}
