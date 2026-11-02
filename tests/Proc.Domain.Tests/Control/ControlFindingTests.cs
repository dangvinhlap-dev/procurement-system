using Proc.Domain.Common;
using Proc.Domain.Control;

namespace Proc.Domain.Tests.Control;

/// <summary>Sổ vi phạm KSNB (design.md mục 18.19).</summary>
public class ControlFindingTests
{
    private static readonly Guid Company = Guid.CreateVersion7();

    private static ControlFindingData Data(string title = "Chia nhỏ PO né cấp duyệt", FindingSeverity severity = FindingSeverity.Serious) =>
        new(new DateOnly(2026, 11, 2), severity, title, "3 PO sát ngưỡng 50.000.000 cùng NCC trong tuần", null, "NearThreshold", "PO", Guid.CreateVersion7(), "PO-BHA-2611-0003");

    [Fact]
    public void GhiNhan_BatBuocTieuDeMoTa_GiuChungTuGoc()
    {
        var finding = ControlFinding.Create(Company, "VP-BHA-2611-0001", Data());
        Assert.Equal((FindingStatus.Open, FindingSeverity.Serious, "NearThreshold", "PO", "PO-BHA-2611-0003"),
            (finding.Status, finding.Severity, finding.AnomalyCode, finding.RefScreen, finding.RefNo));
        Assert.Throws<BusinessRuleException>(() => ControlFinding.Create(Company, "VP-1", Data(" ")));
        Assert.Throws<BusinessRuleException>(() => ControlFinding.Create(Company, "VP-1", Data() with { Description = "" }));
        Assert.Throws<BusinessRuleException>(() => ControlFinding.Create(Company, "VP-1", Data(new string('x', ControlFinding.TitleMaxLength + 1))));
        Assert.Throws<BusinessRuleException>(() => ControlFinding.Create(Company, "VP-1", Data(severity: (FindingSeverity)9)));
        Assert.Throws<BusinessRuleException>(() => ControlFinding.Create(Guid.Empty, "VP-1", Data()));
    }

    [Fact]
    public void KetLuan_KhoaPhieu_HaMucKhiGhiNhanNham()
    {
        var finding = ControlFinding.Create(Company, "VP-BHA-2611-0001", Data());
        finding.Update(Data("Chia nhỏ PO – đã xác minh"));
        Assert.Equal("Chia nhỏ PO – đã xác minh", finding.Title);

        Assert.Throws<BusinessRuleException>(() => finding.Conclude(" ", FindingSeverity.Normal, DateTimeOffset.UtcNow, Guid.CreateVersion7()));
        var by = Guid.CreateVersion7();
        finding.Conclude("Không vi phạm: 3 PO cho 3 công trình khác nhau", FindingSeverity.Normal, DateTimeOffset.UtcNow, by);
        Assert.Equal((FindingStatus.Concluded, FindingSeverity.Normal, (Guid?)by), (finding.Status, finding.Severity, finding.ConcludedBy));
        Assert.Throws<BusinessRuleException>(() => finding.Update(Data()));
        Assert.Throws<BusinessRuleException>(() => finding.Conclude("Lần hai", FindingSeverity.Serious, DateTimeOffset.UtcNow, by));
    }
}
