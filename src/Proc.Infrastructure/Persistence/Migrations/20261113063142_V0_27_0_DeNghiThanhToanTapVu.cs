using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V0_27_0_DeNghiThanhToanTapVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ReceiptConfirmationId",
                table: "PaymentRequestLine",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNo",
                table: "PaymentRequestLine",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<Guid>(
                name: "SundryExpenseSheetId",
                table: "PaymentRequestLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "PaymentRequest",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseOrderId",
                table: "PaymentRequest",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "PayeeName",
                table: "PaymentRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestLine_SundryExpenseSheetId",
                table: "PaymentRequestLine",
                column: "SundryExpenseSheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequestLine_SundryExpenseSheet_SundryExpenseSheetId",
                table: "PaymentRequestLine",
                column: "SundryExpenseSheetId",
                principalTable: "SundryExpenseSheet",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mất dữ liệu: xóa mọi đề nghị thanh toán tổng hợp tạp vụ 1C (Form = 3) và dòng của chúng – cột PO, NCC, XNH trở lại bắt buộc nên phiếu
            // 1C không còn chỗ lưu. Bước duyệt, đính kèm của các phiếu này còn lại nhưng không còn phiếu chủ.
            migrationBuilder.Sql(
                "DELETE FROM \"PaymentRequestLine\" WHERE \"RequestId\" IN (SELECT \"Id\" FROM \"PaymentRequest\" WHERE \"Form\" = 3); " +
                "DELETE FROM \"PaymentRequest\" WHERE \"Form\" = 3;");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequestLine_SundryExpenseSheet_SundryExpenseSheetId",
                table: "PaymentRequestLine");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequestLine_SundryExpenseSheetId",
                table: "PaymentRequestLine");

            migrationBuilder.DropColumn(
                name: "SundryExpenseSheetId",
                table: "PaymentRequestLine");

            migrationBuilder.DropColumn(
                name: "PayeeName",
                table: "PaymentRequest");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReceiptConfirmationId",
                table: "PaymentRequestLine",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNo",
                table: "PaymentRequestLine",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "PaymentRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseOrderId",
                table: "PaymentRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
