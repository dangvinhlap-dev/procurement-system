using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Đơn vị mua (lát Y5a, design.md mục 19.3 – Q3): chuẩn hóa mã, kiểm tra tên / ghi chú, ngừng dùng.</summary>
public class PurchasingUnitTests
{
    [Fact]
    public void Tao_ChuanHoaMa_CatKhoangTrang()
    {
        var unit = PurchasingUnit.Create(Guid.NewGuid(), " vpndd ", "  Văn phòng NDD ", "  ");

        Assert.Equal("VPNDD", unit.Code);
        Assert.Equal("Văn phòng NDD", unit.Name);
        Assert.Null(unit.Note);
        Assert.True(unit.IsActive);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("PHÒNG")]
    [InlineData("VP-NDD")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void Tao_MaSaiQuyTac_BiChan(string code) =>
        Assert.Throws<BusinessRuleException>(() => PurchasingUnit.Create(Guid.NewGuid(), code, "Tên", null));

    [Fact]
    public void Sua_TenRong_GhiChuQuaDai_BiChan_NgungDungDuoc()
    {
        var unit = PurchasingUnit.Create(Guid.NewGuid(), "CUVT", "Phòng CUVT", null);

        Assert.Throws<BusinessRuleException>(() => unit.Update(" ", null, true));
        Assert.Throws<BusinessRuleException>(() => unit.Update("Phòng CUVT", new string('x', PurchasingUnit.NoteMaxLength + 1), true));
        Assert.Throws<BusinessRuleException>(() => PurchasingUnit.Create(Guid.Empty, "CUVT", "Phòng CUVT", null));

        unit.Update("Phòng Cung ứng vật tư", "Mua vật tư công trình", isActive: false);
        Assert.Equal("CUVT", unit.Code);
        Assert.Equal("Mua vật tư công trình", unit.Note);
        Assert.False(unit.IsActive);
    }
}
