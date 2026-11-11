using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Công trình và dự toán (lát Y2, design.md mục 19.2): mã, dự toán ban đầu, điều chỉnh ghi lịch sử, trạng thái.</summary>
public class ConstructionProjectTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static ConstructionProject NewProject(decimal estimate = 64_000_000m) => ConstructionProject.Create(
        Guid.NewGuid(), " ct-2026/01 ", "Thi công Vua Trời", Guid.NewGuid(), "Khu A", Today, Today.AddMonths(2), estimate, "QĐ 12", null, Today);

    [Fact]
    public void Tao_ChuanHoaMa_GhiDuToanBanDauLan0()
    {
        var project = NewProject();

        Assert.Equal("CT-2026/01", project.Code);
        Assert.Equal(ConstructionProjectStatus.InProgress, project.Status);
        var initial = Assert.Single(project.Revisions);
        Assert.Equal(0, initial.RevisionNo);
        Assert.Equal(0m, initial.PreviousAmount);
        Assert.Equal(64_000_000m, initial.NewAmount);
        Assert.Equal("QĐ 12", initial.Basis);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("CT 01")]
    [InlineData("-CT01")]
    public void Tao_MaSai_BiChan(string code) =>
        Assert.Throws<BusinessRuleException>(() => ConstructionProject.Create(
            Guid.NewGuid(), code, "Tên", Guid.NewGuid(), null, null, null, 1_000_000m, null, null, Today));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(0.001)]
    public void Tao_DuToanSai_BiChan(decimal amount) =>
        Assert.Throws<BusinessRuleException>(() => NewProject(amount));

    [Fact]
    public void DieuChinh_ThemDongLichSu_CapNhatDuToan()
    {
        var project = NewProject();

        var first = project.ReviseEstimate(70_000_000m, Today, "QĐ 15", "Thêm hạng mục");
        var second = project.ReviseEstimate(65_000_000m, Today.AddDays(5), null, "Giảm khối lượng");

        Assert.Equal(65_000_000m, project.EstimateAmount);
        Assert.Equal(1, first.RevisionNo);
        Assert.Equal(64_000_000m, first.PreviousAmount);
        Assert.Equal(2, second.RevisionNo);
        Assert.Equal(70_000_000m, second.PreviousAmount);
        Assert.Equal(3, project.Revisions.Count);
    }

    [Fact]
    public void DieuChinh_KhongDoi_ThieuLyDo_HoanThanh_BiChan()
    {
        var project = NewProject();
        Assert.Throws<BusinessRuleException>(() => project.ReviseEstimate(64_000_000m, Today, null, "Không đổi"));
        Assert.Throws<BusinessRuleException>(() => project.ReviseEstimate(70_000_000m, Today, null, " "));

        project.Update(project.Name, project.DepartmentId, null, null, null, ConstructionProjectStatus.Completed, null);
        Assert.Throws<BusinessRuleException>(() => project.ReviseEstimate(70_000_000m, Today, null, "Thêm"));
        Assert.Single(project.Revisions);
    }

    [Fact]
    public void Sua_NgayHoanThanhTruocKhoiCong_BiChan()
    {
        var project = NewProject();
        Assert.Throws<BusinessRuleException>(() =>
            project.Update("Tên", project.DepartmentId, null, Today, Today.AddDays(-1), ConstructionProjectStatus.InProgress, null));
    }
}
