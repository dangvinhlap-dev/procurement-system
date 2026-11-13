using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Đề nghị thanh toán tổng hợp tạp vụ 1C (lát Y11b – design.md mục 19.2 loại C): dòng là bảng kê 2C đã duyệt cùng bộ phận, người nhận tiền.</summary>
public sealed class SundryPaymentRequestTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly Guid Department = Guid.CreateVersion7();

    private static PaymentRequestSundrySource Sheet(decimal amount, Guid? department = null, ApprovalDocumentStatus status = ApprovalDocumentStatus.Approved) =>
        new(Guid.CreateVersion7(), "BKT-1", Today, department ?? Department, status, amount, null);

    private static PaymentRequest Create(PaymentMethod method = PaymentMethod.Cash, PaymentBankAccount? bank = null) =>
        PaymentRequest.CreateSundry(Guid.CreateVersion7(), "DNT-1", Department, new PaymentRequestHeader(Today, method, 0, null, null), " Nguyễn Văn An ", bank);

    [Fact]
    public void Lap1C_GopBangKe_TongVaNguoiNhan()
    {
        var request = Create();
        request.SetSundryLines([Sheet(550_500), Sheet(195_500)]);
        Assert.Equal((PaymentRequestForm.Sundry, "Nguyễn Văn An", 746_000m, (Guid?)null), (request.Form, request.PayeeName, request.PayableAmount, request.PurchaseOrderId));
        Assert.Empty(request.RelatedSupplierIds);
        Assert.Throws<BusinessRuleException>(() => request.SetLines([]));
    }

    [Fact]
    public void BangKeKhacBoPhan_ChuaDuyet_ChuyenKhoanThieuTaiKhoan_ThieuNguoiNhan()
    {
        var request = Create();
        Assert.Throws<BusinessRuleException>(() => request.SetSundryLines([Sheet(1_000, Guid.CreateVersion7())]));
        Assert.Throws<BusinessRuleException>(() => request.SetSundryLines([Sheet(1_000, status: ApprovalDocumentStatus.Submitted)]));
        Assert.Throws<BusinessRuleException>(() => Create(PaymentMethod.BankTransfer));
        Assert.Throws<BusinessRuleException>(() => PaymentRequest.CreateSundry(
            Guid.CreateVersion7(), "DNT-2", Department, new PaymentRequestHeader(Today, PaymentMethod.Cash, 0, null, null), " ", null));

        var transfer = Create(PaymentMethod.BankTransfer, new PaymentBankAccount(Guid.CreateVersion7(), " ACB ", null, " 123 ", null));
        Assert.Equal(("ACB", "123", (Guid?)null), (transfer.BankName, transfer.BankAccountNumber, transfer.BankAccountId));
    }
}
