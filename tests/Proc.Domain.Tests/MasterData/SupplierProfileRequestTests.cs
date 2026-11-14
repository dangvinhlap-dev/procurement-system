using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Đề nghị cập nhật hồ sơ NCC gửi từ Portal (lát P2a, design.md 19.5 P2-1…P2-6).</summary>
public class SupplierProfileRequestTests
{
    private static readonly SupplierGroup Group = SupplierGroup.Create("TN", "Trong nước");
    private static readonly DateTimeOffset At = new(2026, 11, 14, 3, 0, 0, TimeSpan.Zero);

    private static Supplier NewSupplier()
    {
        var supplier = Supplier.Create("NCC01", Group,
            new SupplierInfo("Thép Việt", "TV", false, "0101234567", "Hà Nội", "0241", null, null, "30 ngày", "Thép", 4));
        supplier.SetContacts([new(null, "Anh A", "KD", "0901", null, true)]);
        supplier.SetBankAccounts([new(null, "Vietcombank", null, "0011 2233", null, true)]);
        return supplier;
    }

    private static SupplierProfileData Same(Supplier s) =>
        new(s.Name, s.ShortName, s.TaxCode, s.Address, s.Phone, s.Email, s.Website, s.SupplyScope);

    private static IEnumerable<SupplierContactData> ContactsOf(Supplier s) =>
        s.Contacts.Select(c => new SupplierContactData(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary));

    private static IEnumerable<SupplierBankAccountData> BanksOf(Supplier s) =>
        s.BankAccounts.Select(b => new SupplierBankAccountData(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault));

    [Fact]
    public void Gui_KhongCoThayDoi_BaoLoi()
    {
        var supplier = NewSupplier();
        var ex = Assert.Throws<BusinessRuleException>(() => SupplierProfileRequest.Create(
            supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn", Same(supplier) with { Address = "  Hà Nội " }, ContactsOf(supplier),
            BanksOf(supplier), null, At));
        Assert.Contains("không khác", ex.Message);
    }

    [Fact]
    public void Gui_ChiLuuTruongDoi_ChapNhanKhongGhiDeTruongPhongCuvtSuaTrongLucCho()
    {
        var supplier = NewSupplier();
        var request = SupplierProfileRequest.Create(
            supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn", Same(supplier) with { Address = "Hải Phòng", Website = "tv.vn" },
            ContactsOf(supplier), BanksOf(supplier), "Đổi trụ sở", At);
        Assert.Equal(["Address", "Website"], request.FieldChanges.Select(c => c.Field));
        Assert.Equal("Hà Nội", request.FieldChanges[0].Old);
        Assert.Null(request.ProposedContacts);
        Assert.Null(request.ProposedBankAccounts);

        // Phòng CUVT tự sửa điện thoại trong lúc chờ – chấp nhận không được trả điện thoại về giá trị lúc NCC gửi.
        supplier.Update(new SupplierInfo("Thép Việt", "TV", false, "0101234567", "Hà Nội", "0249", null, null, "30 ngày", "Thép", 4));
        var reviewer = Guid.NewGuid();
        request.Accept(supplier, reviewer, null, At.AddHours(1));

        Assert.Equal("Hải Phòng", supplier.Address);
        Assert.Equal("tv.vn", supplier.Website);
        Assert.Equal("0249", supplier.Phone);
        Assert.Equal("30 ngày", supplier.PaymentTerms);
        Assert.Equal(4, supplier.Rating);
        Assert.Equal(SupplierProfileRequestStatus.Accepted, request.Status);
        Assert.Equal(reviewer, request.ReviewedBy);
    }

    [Fact]
    public void ChapNhan_NguoiLienHe_ApNgay_TaiKhoanKhongApTaiDomain()
    {
        var supplier = NewSupplier();
        var contacts = ContactsOf(supplier).Append(new SupplierContactData(null, "Chị B", "Kế toán", null, "b@ncc.vn", false)).ToList();
        var banks = new[] { new SupplierBankAccountData(null, "BIDV", "Hà Nội", "1234 5678", "Thép Việt", true) };
        var request = SupplierProfileRequest.Create(
            supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn", Same(supplier), contacts, banks, null, At);
        Assert.Empty(request.FieldChanges);
        Assert.Equal(2, request.ProposedContacts!.Count);
        Assert.Equal("12345678", request.ProposedBankAccounts!.Single().AccountNumber);

        request.Accept(supplier, Guid.NewGuid(), "Đồng ý", At);

        Assert.Equal(["Anh A", "Chị B"], supplier.Contacts.Select(c => c.Name).Order());
        // Tài khoản ngân hàng đi qua yêu cầu duyệt NCC (quy tắc Q7) – domain không tự áp.
        Assert.Equal("00112233", supplier.BankAccounts.Single().AccountNumber);
    }

    [Fact]
    public void ChapNhan_NguoiLienHeDaBiXoaTrongLucCho_ThemMoi()
    {
        var supplier = NewSupplier();
        var old = supplier.Contacts.Single();
        var request = SupplierProfileRequest.Create(supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn", Same(supplier),
            [new(old.Id, "Anh A", "Giám đốc KD", "0901", null, true)], BanksOf(supplier), null, At);
        supplier.SetContacts([]);

        request.Accept(supplier, Guid.NewGuid(), null, At);

        Assert.Equal("Giám đốc KD", supplier.Contacts.Single().Position);
    }

    [Fact]
    public void Gui_MaSoThueSai_BaoLoi_TenTrong_BaoLoi()
    {
        var supplier = NewSupplier();
        Assert.Throws<BusinessRuleException>(() => SupplierProfileRequest.Create(supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn",
            Same(supplier) with { TaxCode = "123" }, ContactsOf(supplier), BanksOf(supplier), null, At));
        Assert.Throws<BusinessRuleException>(() => SupplierProfileRequest.Create(supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn",
            Same(supplier) with { Name = " " }, ContactsOf(supplier), BanksOf(supplier), null, At));
    }

    [Fact]
    public void TuChoi_BatBuocLyDo_DaXuLyThiKhoa()
    {
        var supplier = NewSupplier();
        var request = SupplierProfileRequest.Create(supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn",
            Same(supplier) with { Phone = "0909" }, ContactsOf(supplier), BanksOf(supplier), null, At);
        Assert.Throws<BusinessRuleException>(() => request.Reject(Guid.NewGuid(), "  ", At));

        request.Reject(Guid.NewGuid(), "Thiếu giấy phép", At);

        Assert.Equal(SupplierProfileRequestStatus.Rejected, request.Status);
        Assert.Equal("0241", supplier.Phone);
        Assert.Throws<BusinessRuleException>(() => request.Accept(supplier, Guid.NewGuid(), null, At));
        Assert.Throws<BusinessRuleException>(() => request.Withdraw(null, At));
    }

    [Fact]
    public void Rut_KhiConCho()
    {
        var supplier = NewSupplier();
        var request = SupplierProfileRequest.Create(supplier, "NCC01-HS01", Guid.NewGuid(), "a@ncc.vn",
            Same(supplier) with { Email = "kd@tv.vn" }, ContactsOf(supplier), BanksOf(supplier), null, At);

        request.Withdraw("Gửi nhầm", At);

        Assert.Equal(SupplierProfileRequestStatus.Withdrawn, request.Status);
        Assert.Throws<BusinessRuleException>(() => request.Reject(Guid.NewGuid(), "x", At));
    }
}
