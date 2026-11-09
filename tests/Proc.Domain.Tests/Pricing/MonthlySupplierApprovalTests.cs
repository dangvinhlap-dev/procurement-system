using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;

namespace Proc.Domain.Tests.Pricing;

/// <summary>Phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2): đầu phiếu, dòng mặt hàng, vòng đời duyệt, điều chỉnh, hủy.</summary>
public class MonthlySupplierApprovalTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly Guid MainSupplier = Guid.CreateVersion7();
    private static readonly Guid BackupSupplier = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Period = new(2026, 12, 1);

    private static Item NewItem(string code = "VB01")
    {
        var item = Item.Create(code, ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    private static MonthlyApprovalHeader Header(DateOnly? period = null, params Guid[] companies) =>
        new(period ?? Period, Category, companies.Length == 0 ? [Company, Member] : companies, " Kết quả họp Ban ");

    private static MonthlyApprovalLineData Line(Item item, Guid? backup = null, decimal? backupPrice = null, decimal? limit = 100m, Guid? uom = null) =>
        new(null, item.Id, uom ?? Cai.Id, MainSupplier, 45_000m, backup, backupPrice, limit, null);

    [Fact]
    public void DauPhieu_ThangDauThang_CoCongTy_GhiChuCatKhoangTrang()
    {
        var approval = MonthlySupplierApproval.Create(Company, "GTH-1", Header());
        Assert.Equal(("Kết quả họp Ban", 2, MonthlyApprovalStatus.Draft), (approval.Note, approval.Companies.Count, approval.Status));
        Assert.Throws<BusinessRuleException>(() => MonthlySupplierApproval.Create(Company, "GTH-1", Header(new DateOnly(2026, 12, 5))));
        Assert.Throws<BusinessRuleException>(() => MonthlySupplierApproval.Create(Company, "GTH-1", Header(Period, Guid.Empty)));
        Assert.Throws<BusinessRuleException>(() => MonthlySupplierApproval.Create(Company, "GTH-1", Header() with { ItemCategoryId = Guid.Empty }));

        // Bớt công ty: chỉ giữ công ty còn chọn.
        approval.Update(Header(Period, Member));
        Assert.Equal([Member], approval.Companies.Select(c => c.CompanyId));
    }

    [Fact]
    public void GiaChuanTranMucTieu_KhongBatBuoc_GiaDuyetKhongVuotTran()
    {
        var item = NewItem();
        var approval = MonthlySupplierApproval.Create(Company, "GTH-1", Header());
        MonthlyApprovalLineData Priced(decimal? standard, decimal? ceiling, decimal? target, decimal? backupPrice = null) =>
            Line(item, backupPrice is null ? null : BackupSupplier, backupPrice) with { StandardPrice = standard, CeilingPrice = ceiling, TargetPrice = target };

        // Giá chính 45.000: trần 44.000 không được; dự phòng, chuẩn, mục tiêu cũng không vượt trần.
        Assert.Contains("giá NCC chính vượt giá trần", Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Priced(null, 44_000m, null), item)])).Message);
        Assert.Contains("dự phòng vượt", Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Priced(null, 46_000m, null, 47_000m), item)])).Message);
        Assert.Contains("giá chuẩn vượt", Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Priced(47_000m, 46_000m, null), item)])).Message);
        Assert.Contains("mục tiêu vượt", Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Priced(null, 46_000m, 47_000m), item)])).Message);
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Priced(-1m, null, null), item)]));

        approval.SetLines([(Priced(45_000m, 48_000m, 43_000m, 46_500m), item)]);
        var line = Assert.Single(approval.Lines);
        Assert.Equal(((decimal?)45_000m, (decimal?)48_000m, (decimal?)43_000m), (line.StandardPrice, line.CeilingPrice, line.TargetPrice));
        // Chép sang phiếu khác giữ ba giá.
        Assert.Equal((decimal?)48_000m, approval.CopyLines().Single().CeilingPrice);

        // Không có trần: không giới hạn giá duyệt.
        approval.SetLines([(Priced(null, null, null), item)]);
        Assert.Null(approval.Lines.Single().CeilingPrice);
    }

    [Fact]
    public void Dong_MotMatHangMotDong_DuPhongKhacChinhVaCoGia_HanMucDuong_DvtQuyDoi()
    {
        var item = NewItem();
        var approval = MonthlySupplierApproval.Create(Company, "GTH-1", Header());
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([]));
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Line(item), item), (Line(item), item)]));
        Assert.Contains("khác NCC chính", Assert.Throws<BusinessRuleException>(() =>
            approval.SetLines([(Line(item, MainSupplier, 1m), item)])).Message);
        Assert.Contains("giá NCC dự phòng", Assert.Throws<BusinessRuleException>(() =>
            approval.SetLines([(Line(item, BackupSupplier), item)])).Message);
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Line(item, null, 1m), item)]));
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Line(item, limit: 0m), item)]));
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Line(item, uom: Guid.CreateVersion7()), item)]));

        approval.SetLines([(Line(item, BackupSupplier, 46_500m, null, Thung.Id), item)]);
        var line = Assert.Single(approval.Lines);
        Assert.Equal((12m, (decimal?)null, (decimal?)46_500m), (line.UomFactor, line.QuantityLimit, line.BackupUnitPrice));
        Assert.Equal([MainSupplier, BackupSupplier], approval.RelatedSupplierIds);
    }

    [Fact]
    public void VongDoi_GuiDuyetTuChoiDuyet_DaDuyetKhongSua_ThayVaHuy()
    {
        var item = NewItem();
        var approval = MonthlySupplierApproval.Create(Company, "GTH-1", Header());
        Assert.Throws<BusinessRuleException>(() => approval.Submit());
        approval.SetLines([(Line(item), item)]);
        Assert.Equal(1, approval.Submit());
        Assert.True(approval.IsAwaitingApproval);
        Assert.Throws<BusinessRuleException>(() => approval.Update(Header()));
        // Chờ duyệt không hủy được – phải rút về nháp trước.
        Assert.Throws<BusinessRuleException>(() => approval.Cancel(Period, "Không mua"));
        approval.ReturnToDraft("Giá cao hơn tháng trước");
        Assert.Equal(("Giá cao hơn tháng trước", MonthlyApprovalStatus.Draft), (approval.RejectReason, approval.Status));
        Assert.Equal(2, approval.Submit());
        Assert.Null(approval.RejectReason);
        approval.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Throws<BusinessRuleException>(() => approval.SetLines([(Line(item), item)]));

        // Chép sang phiếu khác: cùng nhóm hàng, công ty, dòng mới (Id null).
        var copy = MonthlySupplierApproval.Create(Company, "GTH-2", approval.HeaderFor(Period.AddMonths(1)), approval.Id);
        copy.SetLines(approval.CopyLines().Select(d => (d, item)).ToList());
        Assert.Equal((approval.Id, Period.AddMonths(1), 1, 2), (copy.ReplacesId!.Value, copy.Period, copy.Lines.Count, copy.Companies.Count));
        Assert.All(approval.CopyLines(), d => Assert.Null(d.Id));

        approval.MarkReplaced();
        Assert.Equal(MonthlyApprovalStatus.Replaced, approval.Status);
        Assert.Throws<BusinessRuleException>(() => approval.Cancel(Period, "x"));

        Assert.Throws<BusinessRuleException>(() => copy.Cancel(Period, " "));
        copy.Cancel(Period, "Nhóm hàng không mua tháng này");
        Assert.Equal((MonthlyApprovalStatus.Cancelled, Period), (copy.Status, copy.CancelledOn!.Value));
    }

    [Fact]
    public void NgoaiLe_ItNcc_DocQuyen_VuotMucTieu_BatBuocGiaiTrinh_TuKhaiNgoaiLe()
    {
        // Quy tắc từng dòng (lát H2): dưới 3 NCC đủ điều kiện; độc quyền; giá chính/dự phòng vượt giá mục tiêu (bằng mục tiêu thì không).
        Assert.Empty(MonthlyApprovalExceptions.ForLine(3, null, 12_000m, 12_500m, 12_500m));
        Assert.Equal(["Chỉ có 2 NCC đủ điều kiện (tối thiểu 3)", "Giá NCC chính 12.000 vượt giá mục tiêu 11.500",
                "Giá NCC dự phòng 12.500,5 vượt giá mục tiêu 11.500"],
            MonthlyApprovalExceptions.ForLine(2, null, 12_000m, 12_500.5m, 11_500m));
        Assert.Equal(["NCC độc quyền theo NDQ-TCT-2612-0001 – 1 NCC đủ điều kiện"],
            MonthlyApprovalExceptions.ForLine(1, "NDQ-TCT-2612-0001", 12_000m, null, null));

        // Có ngoại lệ mà chưa giải trình thì không gửi được; có giải trình thì qua chặng ngoại lệ.
        var item = NewItem();
        var approval = MonthlySupplierApproval.Create(Company, "GTH-1", Header());
        approval.SetLines([(Line(item), item)]);
        Assert.Contains("giải trình ngoại lệ", Assert.Throws<BusinessRuleException>(() => approval.PrepareSubmit(["VB01: Chỉ có 1 NCC"])).Message);
        Assert.False(approval.PrepareSubmit([]));
        approval.Update(Header() with { ExceptionNote = "  Mặt hàng mới, đang tìm thêm NCC  " });
        Assert.Equal("Mặt hàng mới, đang tìm thêm NCC", approval.ExceptionNote);
        Assert.True(approval.PrepareSubmit(["VB01: Chỉ có 1 NCC"]));

        // Không có ngoại lệ tự phát hiện nhưng người lập ghi giải trình: tự khai ngoại lệ – vẫn lên Chủ tịch/TGĐ.
        Assert.True(approval.PrepareSubmit([]));
        Assert.True(approval.HasException);
        Assert.Equal("Mặt hàng mới, đang tìm thêm NCC", approval.HeaderFor(Period).ExceptionNote);
        approval.Submit();
        Assert.Throws<BusinessRuleException>(() => approval.PrepareSubmit([]));
    }
}
