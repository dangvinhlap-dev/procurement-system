using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Yêu cầu duyệt NCC và tiến trình duyệt hai chặng (design.md mục 18.6).</summary>
public class SupplierChangeRequestTests
{
    private static readonly SupplierGroup Group = SupplierGroup.Create("TN", "Trong nước");
    private static readonly Guid Company = Guid.CreateVersion7();

    private static Supplier NewSupplier()
    {
        var supplier = Supplier.Create("NCC01", Group, new SupplierInfo("Thép Việt", null, false, null, null, null, null, null, null, null, null));
        supplier.SetBankAccounts([new(null, "Vietcombank", null, "0011 2233", null, true)]);
        return supplier;
    }

    [Fact]
    public void TienTrinh_ChangMotSongSongTheoCongTy_ChangHaiSauCung()
    {
        Guid bha = Guid.CreateVersion7(), bhb = Guid.CreateVersion7(), tct = Guid.CreateVersion7(), doc = Guid.CreateVersion7();
        var bha1 = ApprovalStep.Create(bha, "NCC", doc, 1, 1, null);
        var bha2 = ApprovalStep.Create(bha, "NCC", doc, 1, 2, null);
        var bhb1 = ApprovalStep.Create(bhb, "NCC", doc, 1, 1, null);
        var tct1 = ApprovalStep.Create(tct, "NCC", doc, 1, 1, null, stage: 2);
        var steps = new[] { bha1, bha2, bhb1, tct1 };

        // Hai công ty cùng đến lượt (cấp thấp nhất của mỗi công ty); Tổng công ty chưa.
        Assert.Equal([bha1, bhb1], ApprovalProgress.Active(steps));
        bha1.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        Assert.Equal([bha2, bhb1], ApprovalProgress.Active(steps));
        bhb1.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        Assert.Equal([bha2], ApprovalProgress.Active(steps));
        bha2.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        Assert.Equal([tct1], ApprovalProgress.Active(steps));
        tct1.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null);
        Assert.Empty(ApprovalProgress.Active(steps));
    }

    [Fact]
    public void YeuCauTrangThai_ChiDaDuyetVaDanhSachDen_DuyetXongMoiAp()
    {
        var supplier = NewSupplier();
        Assert.True(supplier.StatusChangeNeedsApproval(SupplierStatus.Approved));
        Assert.True(supplier.StatusChangeNeedsApproval(SupplierStatus.Blacklisted));
        Assert.False(supplier.StatusChangeNeedsApproval(SupplierStatus.Suspended));
        Assert.Throws<BusinessRuleException>(() => SupplierChangeRequest.ForStatus(supplier, Company, "NCC01-01", SupplierStatus.Suspended, "x"));
        Assert.Throws<BusinessRuleException>(() => SupplierChangeRequest.ForStatus(supplier, Company, "NCC01-01", SupplierStatus.Blacklisted, " "));

        var request = SupplierChangeRequest.ForStatus(supplier, Company, "NCC01-01", SupplierStatus.Approved, null);
        Assert.Equal(1, request.Submit());
        Assert.Throws<BusinessRuleException>(() => request.ApplyTo(supplier));
        request.MarkApproved(DateTimeOffset.UtcNow);
        request.ApplyTo(supplier);
        Assert.Equal(SupplierStatus.Approved, supplier.Status);
    }

    [Fact]
    public void YeuCauTaiKhoan_KhongDoiThiBaoLoi_TuChoiLaKetThuc()
    {
        var supplier = NewSupplier();
        Assert.Contains("không thay đổi", Assert.Throws<BusinessRuleException>(() =>
            SupplierChangeRequest.ForBankAccounts(supplier, Company, "NCC01-01", [new(null, " Vietcombank ", null, "00112233", null, true)])).Message);

        var request = SupplierChangeRequest.ForBankAccounts(supplier, Company, "NCC01-01", [new(null, "ACB", null, "9988 7766", null, true)]);
        Assert.Equal(("Vietcombank 00112233 (mặc định)", "ACB 99887766 (mặc định)"), (request.CurrentSummary, request.ProposedSummary));
        request.Submit();
        request.ReturnToDraft("Chưa xác minh");
        Assert.Equal(SupplierChangeRequestStatus.Rejected, request.Status);
        Assert.Throws<BusinessRuleException>(() => request.Submit());
        Assert.Equal("00112233", Assert.Single(supplier.BankAccounts).AccountNumber);
    }
}
