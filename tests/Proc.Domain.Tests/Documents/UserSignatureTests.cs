using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Documents;

/// <summary>Chữ ký người dùng theo phiên bản, logo công ty, kiểm ảnh in (lát IN1 – design.md mục 20.3).</summary>
public class UserSignatureTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Admin = Guid.CreateVersion7();
    private static readonly DateTimeOffset T0 = new(2026, 11, 18, 2, 0, 0, TimeSpan.Zero);

    /// <summary>PNG tối thiểu: 8 byte nhận dạng + khung IHDR (rộng, cao big-endian).</summary>
    internal static byte[] Png(int width, int height)
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 0, 0, 0, 0, 0, 8, 6, 0, 0, 0];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    [Fact]
    public void TuTai_KhongCanLyDo_LayKichThuocTuPng()
    {
        var s = UserSignature.Create(Owner, 1, Png(600, 200), Owner, T0, null);
        Assert.False(s.ByAdmin);
        Assert.Equal((600, 200), (s.Width, s.Height));
        Assert.Null(s.ValidTo);
    }

    [Fact]
    public void AdminTaiThay_BatBuocLyDo()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => UserSignature.Create(Owner, 1, Png(10, 10), Admin, T0, " "));
        Assert.Contains("lý do", ex.Message);
        var s = UserSignature.Create(Owner, 1, Png(10, 10), Admin, T0, "Theo bản ký mẫu");
        Assert.True(s.ByAdmin);
    }

    [Fact]
    public void ThayChuKy_PhieuCuVanLayChuKyCu_ChuKyMoiTinhTuLucThay()
    {
        // Người dùng chốt 18/11/2026: phiếu duyệt trước lúc thay in chữ ký cũ.
        var v1 = UserSignature.Create(Owner, 1, Png(10, 10), Owner, T0, null);
        var thay = T0.AddDays(3);
        v1.Retire(thay, Owner, null, replaced: true);
        var v2 = UserSignature.Create(Owner, 2, Png(10, 10), Owner, thay, null);

        Assert.True(v1.IsValidAt(T0.AddDays(1)));
        Assert.False(v2.IsValidAt(T0.AddDays(1)));
        Assert.False(v1.IsValidAt(thay));
        Assert.True(v2.IsValidAt(thay.AddMinutes(1)));
        Assert.False(v1.IsValidAt(T0.AddMinutes(-1)));
        Assert.Throws<BusinessRuleException>(() => v1.Retire(thay, Owner, null, replaced: true));
    }

    [Fact]
    public void AdminNgungDungChuKyNguoiKhac_BatBuocLyDo()
    {
        var s = UserSignature.Create(Owner, 1, Png(10, 10), Owner, T0, null);
        Assert.Throws<BusinessRuleException>(() => s.Retire(T0.AddHours(1), Admin, null, replaced: false));
        s.Retire(T0.AddHours(1), Admin, "Nghỉ việc", replaced: false);
        Assert.Equal("Nghỉ việc", s.RetireReason);
    }

    [Fact]
    public void ChuKy_ChiNhanPng_LogoNhanCaJpeg()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x64, 0x00, 0xC8, 0x03, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Throws<BusinessRuleException>(() => UserSignature.Create(Owner, 1, jpeg, Owner, T0, null));
        var logo = CompanyLogo.Create(Guid.CreateVersion7(), jpeg);
        Assert.Equal((PrintImages.Jpeg, 200, 100), (logo.ContentType, logo.Width, logo.Height));
        Assert.Throws<BusinessRuleException>(() => CompanyLogo.Create(Guid.CreateVersion7(), [1, 2, 3, 4]));
    }

    [Fact]
    public void AnhQuaLonHoacKichThuocBatThuong_BiChan()
    {
        var big = Png(10, 10).Concat(new byte[UserSignature.MaxImageBytes]).ToArray();
        Assert.Throws<BusinessRuleException>(() => UserSignature.Create(Owner, 1, big, Owner, T0, null));
        Assert.Throws<BusinessRuleException>(() => UserSignature.Create(Owner, 1, Png(5000, 10), Owner, T0, null));
    }
}
