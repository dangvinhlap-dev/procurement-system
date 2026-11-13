using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Bảng kê chi phí tạp vụ 2C (lát Y11a – design.md mục 19.2 loại C): chỉ kê chi phí đã phát sinh, tổng tính vào định mức tháng.</summary>
public sealed class SundryExpenseSheetTests
{
    private static readonly DateOnly Day = new(2026, 11, 13);

    private static SundryExpenseSheet Sheet() =>
        SundryExpenseSheet.Create(Guid.CreateVersion7(), "BKT-1", new SundryExpenseSheetHeader(Day, Guid.CreateVersion7(), " Ghi chú "));

    [Fact]
    public void Lap_TongTien_LamTron_GiuDongCu()
    {
        var sheet = Sheet();
        sheet.SetLines([
            new SundryExpenseLineData(Day.AddDays(-3), " Nước uống ", " Cửa hàng A ", " HD01 ", 120_000.005m),
            new SundryExpenseLineData(Day, "Gửi thư", null, null, 30_000m),
        ]);
        Assert.Equal(("Ghi chú", 150_000.01m, 2), (sheet.Note, sheet.TotalAmount, sheet.Lines.Count));
        Assert.Equal(("Nước uống", "Cửa hàng A", "HD01", 120_000.01m), (sheet.Lines[0].Description, sheet.Lines[0].Vendor, sheet.Lines[0].ReceiptNo, sheet.Lines[0].Amount));
        Assert.Equal(sheet.TotalAmount, sheet.BudgetedAmount);

        var firstId = sheet.Lines[0].Id;
        sheet.SetLines([new SundryExpenseLineData(Day, "Nước uống", null, null, 50_000m)]);
        Assert.Equal((firstId, 50_000m, 1), (sheet.Lines[0].Id, sheet.TotalAmount, sheet.Lines.Count));
    }

    [Fact]
    public void ChiKeChiPhiDaPhatSinh_SoTienDuong_NoiDungBatBuoc()
    {
        var sheet = Sheet();
        Assert.Contains("đã phát sinh", Assert.Throws<BusinessRuleException>(() =>
            sheet.SetLines([new SundryExpenseLineData(Day.AddDays(1), "Trà", null, null, 10_000m)])).Message, StringComparison.Ordinal);
        Assert.Throws<BusinessRuleException>(() => sheet.SetLines([new SundryExpenseLineData(Day, "Trà", null, null, -1m)]));
        Assert.Throws<BusinessRuleException>(() => sheet.SetLines([new SundryExpenseLineData(Day, "  ", null, null, 10_000m)]));

        // Lùi ngày bảng kê trước ngày chi của dòng đang có thì báo lỗi.
        sheet.SetLines([new SundryExpenseLineData(Day, "Trà", null, null, 10_000m)]);
        Assert.Throws<BusinessRuleException>(() => sheet.Update(new SundryExpenseSheetHeader(Day.AddDays(-1), sheet.DepartmentId, null)));
    }

    [Fact]
    public void GuiDuyet_KhoaSua()
    {
        var sheet = Sheet();
        Assert.Throws<BusinessRuleException>(() => sheet.Submit());
        sheet.SetLines([new SundryExpenseLineData(Day, "Trà", null, null, 10_000m)]);
        sheet.Submit();
        Assert.Throws<BusinessRuleException>(() => sheet.SetLines([]));
        Assert.Equal(DocumentTypesCode, sheet.DocType);
    }

    private const string DocumentTypesCode = "BKT";
}
