using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Lát Y7 (design.md mục 19.3): loại mua của PO quyết định biểu mẫu 4A (công trình) / 4B (hàng hóa, dịch vụ).</summary>
public class PurchaseOrderCategoryTests
{
    private static PurchaseOrder NewOrder() => PurchaseOrder.Create(Guid.CreateVersion7(), "PO-1",
        new PurchaseOrderHeader(new DateOnly(2026, 11, 12), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 0, null));

    [Fact]
    public void LoaiA_BatBuocCongTrinh_DichVuGhiLaB()
    {
        var order = NewOrder();
        Assert.Equal((PurchaseCategory.Goods, (Guid?)null), (order.Category, order.ConstructionProjectId));

        Assert.Throws<BusinessRuleException>(() => order.SetPurchaseCategory(PurchaseCategory.Construction, null));
        var project = Guid.CreateVersion7();
        order.SetPurchaseCategory(PurchaseCategory.Construction, project);
        Assert.Equal((PurchaseCategory.Construction, (Guid?)project), (order.Category, order.ConstructionProjectId));

        // Dịch vụ cùng biểu mẫu 4B với hàng hóa; bỏ công trình.
        order.SetPurchaseCategory(PurchaseCategory.Service, project);
        Assert.Equal((PurchaseCategory.Goods, (Guid?)null), (order.Category, order.ConstructionProjectId));
    }
}
