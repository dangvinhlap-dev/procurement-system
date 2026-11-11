using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Loại mua A/B/DV của đề nghị mua (lát Y3a, design.md mục 19.3): loại A bắt buộc công trình, loại khác không gắn công trình.</summary>
public class PurchaseRequestCategoryTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Department = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2026, 11, 11);

    private static PurchaseRequestHeader Header(PurchaseCategory category, Guid? projectId) =>
        new(Day, Department, null, null, null, PurchasePriority.Normal, null, category, projectId);

    [Fact]
    public void MacDinhLaHangHoa_KhongCongTrinh()
    {
        var pr = PurchaseRequest.Create(Company, "PR-1", new PurchaseRequestHeader(Day, Department, null, null, null, PurchasePriority.Normal, null), null);
        Assert.Equal((PurchaseCategory.Goods, (Guid?)null), (pr.Category, pr.ConstructionProjectId));
    }

    [Fact]
    public void LoaiA_PhaiCoCongTrinh_LoaiKhacKhongGanCongTrinh()
    {
        var project = Guid.CreateVersion7();
        Assert.Contains("phải chọn công trình",
            Assert.Throws<BusinessRuleException>(() => PurchaseRequest.Create(Company, "PR-1", Header(PurchaseCategory.Construction, null), null)).Message);
        Assert.Contains("Chỉ đề nghị mua loại A",
            Assert.Throws<BusinessRuleException>(() => PurchaseRequest.Create(Company, "PR-1", Header(PurchaseCategory.Service, project), null)).Message);
        Assert.Throws<BusinessRuleException>(() => PurchaseRequest.Create(Company, "PR-1", Header((PurchaseCategory)9, null), null));

        var pr = PurchaseRequest.Create(Company, "PR-1", Header(PurchaseCategory.Construction, project), null);
        Assert.Equal((PurchaseCategory.Construction, (Guid?)project), (pr.Category, pr.ConstructionProjectId));

        // Đổi sang dịch vụ khi còn nháp: bỏ công trình.
        pr.Update(Header(PurchaseCategory.Service, null));
        Assert.Equal((PurchaseCategory.Service, (Guid?)null), (pr.Category, pr.ConstructionProjectId));
    }
}
