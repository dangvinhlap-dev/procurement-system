using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Tests.Documents;

/// <summary>
/// File đính kèm: đuôi được nhận, dung lượng tối đa (mặc định 2 MB, cấu hình được), tên file, đường dẫn lưu, xóa mềm (design.md mục 18.4).
/// </summary>
public class AttachmentTests
{
    private static readonly DateTimeOffset Nov2026 = new(2026, 11, 1, 3, 0, 0, TimeSpan.Zero);

    private static Attachment Create(string fileName, long size = 1024, string? note = null) =>
        Attachment.Create(AttachmentOwnerTypes.PurchaseOrder, Guid.NewGuid(), Guid.NewGuid(), fileName, size, "abc", Nov2026, note);

    [Fact]
    public void TaiLenPdf_LuuTheoNamThangVaIdKhongDungTenGoc()
    {
        var a = Create("Hợp đồng NCC.PDF", note: "  Bản ký  ");

        Assert.Equal("Hợp đồng NCC.PDF", a.FileName);
        Assert.Equal(".pdf", a.Extension);
        Assert.Equal("application/pdf", a.ContentType);
        Assert.Equal($"2026/11/{a.Id:N}.pdf", a.StoragePath);
        Assert.Equal("Bản ký", a.Note);
    }

    [Fact]
    public void TenCoDuongDan_ChiGiuPhanTenFile()
    {
        // Trình duyệt cũ gửi cả đường dẫn máy người dùng; "../" không được lọt vào tên.
        Assert.Equal("bao-gia.xlsx", Create(@"C:\Users\a\..\bao-gia.xlsx").FileName);
        Assert.Equal("anh.png", Create("../../anh.png").FileName);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("trang.html")]
    [InlineData("hinh.svg")]
    [InlineData("script.js")]
    [InlineData("khongduoi")]
    public void DuoiKhongDuocNhan_BaoLoi(string fileName)
    {
        Assert.Throws<BusinessRuleException>(() => Create(fileName));
    }

    [Fact]
    public void MacDinh2MB_Dung2MBDuocNhan_Vuot1Byte_BaoLoi()
    {
        const long twoMb = 2 * 1024 * 1024;
        Assert.Equal(2, Attachment.DefaultMaxFileSizeMb);
        Assert.Equal(twoMb, Create("a.zip", twoMb).SizeBytes);
        var ex = Assert.Throws<BusinessRuleException>(() => Create("a.zip", twoMb + 1));
        Assert.Contains("2 MB", ex.Message);
        Assert.Throws<BusinessRuleException>(() => Create("a.zip", 0));
    }

    [Fact]
    public void GioiHanCauHinh_TheoThamSoTruyenVao()
    {
        Assert.Equal(".pdf", Attachment.ValidateFile("a.pdf", 5L * 1024 * 1024, maxFileSizeMb: 5));
        var ex = Assert.Throws<BusinessRuleException>(() => Attachment.ValidateFile("a.pdf", 5L * 1024 * 1024 + 1, maxFileSizeMb: 5));
        Assert.Contains("5 MB", ex.Message);
    }

    [Fact]
    public void LoaiChuKhongHopLe_BaoLoi()
    {
        Assert.Throws<BusinessRuleException>(() =>
            Attachment.Create("Khac", Guid.NewGuid(), null, "a.pdf", 10, "abc", Nov2026, null));
    }

    [Fact]
    public void XoaMem_GhiNguoiXoa_XoaLanHaiBaoLoi()
    {
        var a = Create("a.pdf");
        var user = Guid.NewGuid();

        a.Delete(user, Nov2026);

        Assert.True(a.IsDeleted);
        Assert.Equal(user, a.DeletedBy);
        Assert.Throws<BusinessRuleException>(() => a.Delete(user, Nov2026));
    }
}
