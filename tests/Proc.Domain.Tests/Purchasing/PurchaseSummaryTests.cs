using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>
/// Bảng tổng hợp đề xuất mua 2A/2B (lát Y5b, design.md mục 19.3): đầu phiếu theo biểu mẫu, dòng đúng biểu mẫu – bộ phận – công trình, còn cần mua,
/// có đơn vị mua; SL, thành tiền lấy từ dòng đề nghị; chỉ sửa khi nháp.
/// </summary>
public class PurchaseSummaryTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Unit = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 11);

    private static PurchaseSummary Goods() =>
        PurchaseSummary.Create(Company, "BTH-0001", new(Day, PurchaseSummaryForm.Goods, Department, null, " Tháng 11 "));

    private static PurchaseSummaryLineSource Source(
        PurchaseCategory category = PurchaseCategory.Goods, Guid? department = null, Guid? project = null, decimal open = 6, decimal price = 12_345.678m,
        Guid? lineId = null) =>
        new(lineId ?? Guid.NewGuid(), Guid.NewGuid(), "PR-0001", category, department ?? Department, project, Guid.NewGuid(), "VT01", Guid.NewGuid(),
            24, open, price, Day.AddDays(5));

    private static (PurchaseSummaryLineData, PurchaseSummaryLineSource) Line(PurchaseSummaryLineSource source, Guid? unit = null) =>
        (new PurchaseSummaryLineData(source.RequestLineId, unit ?? Unit, null), source);

    [Fact]
    public void DauPhieu_2APhaiCoCongTrinh_2BKhongGanCongTrinh_BoPhanBatBuoc()
    {
        Assert.Throws<BusinessRuleException>(() => PurchaseSummary.Create(Company, "BTH-1", new(Day, PurchaseSummaryForm.Construction, Department, null, null)));
        Assert.Throws<BusinessRuleException>(() => PurchaseSummary.Create(Company, "BTH-1", new(Day, PurchaseSummaryForm.Goods, Department, Project, null)));
        Assert.Throws<BusinessRuleException>(() => PurchaseSummary.Create(Company, "BTH-1", new(Day, PurchaseSummaryForm.Goods, Guid.Empty, null, null)));
        Assert.Throws<BusinessRuleException>(() => PurchaseSummary.Create(Company, "BTH-1", new(Day, (PurchaseSummaryForm)9, Department, null, null)));

        var summary = Goods();
        Assert.Equal(("Tháng 11", "BTH", Department), (summary.Note, summary.DocType, summary.ApprovalDepartmentId));
        Assert.Equal(ApprovalDocumentStatus.Draft, summary.Status);
    }

    [Fact]
    public void Dong_LaySlConCanMua_ThanhTienLamTron_TongDeXuat()
    {
        var summary = Goods();
        var service = Source(PurchaseCategory.Service, open: 2.5m, price: 10_000m);
        summary.SetLines([Line(Source()), Line(service)]);

        var first = summary.Lines[0];
        // 6 × 12.345,678 = 74.074,068 → 74.074,07; SL cơ sở 6 × 24 = 144.
        Assert.Equal((1, 6m, 144m, 74_074.07m), (first.LineNo, first.Qty, first.BaseQty, first.Amount));
        Assert.Equal(74_074.07m + 25_000m, summary.TotalAmount);
        Assert.Equal(summary.TotalAmount, summary.ApprovalAmount);

        // Lưu lại: dòng giữ Id theo dòng đề nghị, bỏ dòng không gửi.
        var keptId = summary.Lines[1].Id;
        summary.SetLines([Line(service)]);
        Assert.Equal(keptId, Assert.Single(summary.Lines).Id);
        Assert.Equal(1, summary.Lines[0].LineNo);
    }

    [Fact]
    public void Dong_SaiBieuMau_BoPhan_CongTrinh_HetCanMua_ThieuDonVi_Trung_BiChan()
    {
        var summary = Goods();
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(Source(PurchaseCategory.Construction, project: Project))]));
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(Source(department: Guid.NewGuid()))]));
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(Source(open: 0))]));
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(Source(), Guid.Empty)]));
        var same = Source();
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(same), Line(same)]));

        var construction = PurchaseSummary.Create(Company, "BTH-2", new(Day, PurchaseSummaryForm.Construction, Department, Project, null));
        Assert.Throws<BusinessRuleException>(() => construction.SetLines([Line(Source())]));
        Assert.Throws<BusinessRuleException>(() => construction.SetLines([Line(Source(PurchaseCategory.Construction, project: Guid.NewGuid()))]));
        construction.SetLines([Line(Source(PurchaseCategory.Construction, project: Project))]);
        Assert.Single(construction.Lines);
    }

    [Fact]
    public void GuiDuyet_PhaiCoDong_DaGuiThiKhongSua_TuChoiVeNhap()
    {
        var summary = Goods();
        Assert.Throws<BusinessRuleException>(() => summary.Submit());

        summary.SetLines([Line(Source())]);
        Assert.Equal(1, summary.Submit());
        Assert.Throws<BusinessRuleException>(() => summary.SetLines([Line(Source())]));
        Assert.Throws<BusinessRuleException>(() => summary.Update(new(Day, PurchaseSummaryForm.Goods, Department, null, null)));

        summary.ReturnToDraft("Thiếu đơn vị mua");
        Assert.Equal((ApprovalDocumentStatus.Draft, "Thiếu đơn vị mua"), (summary.Status, summary.RejectReason));
        summary.Submit();
        summary.MarkApproved(DateTimeOffset.UtcNow);
        // Đã duyệt: hủy bắt buộc lý do.
        Assert.Throws<BusinessRuleException>(() => summary.Cancel(null));
        summary.Cancel("Lập lại");
        Assert.Equal(ApprovalDocumentStatus.Cancelled, summary.Status);
    }
}
