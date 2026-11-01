using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proc.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lát B3 – RFQ gửi thiếu NCC có giải trình, kết quả chọn thầu phải duyệt (design.md mục 8.6): cột giải trình, người chọn thầu, lượt
    /// gửi duyệt, lý do từ chối; trạng thái mới Chờ duyệt kết quả = 5 lưu ở cột Status sẵn có. RFQ đã chọn thầu trước bản này giữ nguyên
    /// (không bắt duyệt lại). Down() đưa RFQ đang chờ duyệt về Đã gửi (chọn thầu lại như bản cũ) và mất giải trình, người chọn thầu, lượt
    /// gửi, lý do từ chối (lịch sử ApprovalStep vẫn giữ).
    /// </summary>
    public partial class V0_12_0_DuyetKetQuaChonThau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRound",
                table: "Rfq",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AwardedBy",
                table: "Rfq",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FewSuppliersReason",
                table: "Rfq",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "Rfq",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bản cũ không biết trạng thái 5 (Chờ duyệt kết quả) – đưa về Đã gửi (2) để chọn thầu lại như trước.
            migrationBuilder.Sql("UPDATE \"Rfq\" SET \"Status\" = 2 WHERE \"Status\" = 5;");

            migrationBuilder.DropColumn(
                name: "ApprovalRound",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "AwardedBy",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "FewSuppliersReason",
                table: "Rfq");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "Rfq");
        }
    }
}
