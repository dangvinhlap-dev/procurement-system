using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Domain.Sourcing;

namespace Proc.Domain.Tests.Sourcing;

/// <summary>Bảng giá NCC (design.md mục 8.1), RFQ – báo giá – chọn thầu và chấm điểm (mục 8.4).</summary>
public class SourcingTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Thung = Uom.Create("THUNG", "Thùng");
    private static readonly DateOnly Today = new(2026, 10, 31);

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Thung, 12m)]);
        return item;
    }

    [Fact]
    public void BangGia_GiaQuyDoi_TrungBac_HieuLucKhongSua()
    {
        var item = NewItem();
        var list = SupplierPriceList.Create(Company, "PRL-1", new PriceListHeader(Guid.CreateVersion7(), "Bảng giá", false, Today, null, null, null));
        list.SetLines([(new PriceListLineData(null, item.Id, Thung.Id, 0, 600_000, 5, null, null), item)]);
        // 600.000 đ/thùng 12 cái, CK 5% → 570.000 / 12 = 47.500 đ/cái.
        Assert.Equal(47_500m, list.Lines[0].BaseNetPrice);

        Assert.Throws<BusinessRuleException>(() => list.SetLines([
            (new PriceListLineData(null, item.Id, Cai.Id, 10, 1, 0, null, null), item),
            (new PriceListLineData(null, item.Id, Cai.Id, 10, 2, 0, null, null), item),
        ]));
        Assert.True(list.AppliesOn(Today) is false);
        Assert.Equal(1, list.Submit());
        Assert.False(list.AppliesOn(Today));
        list.MarkApproved(DateTimeOffset.UtcNow);
        Assert.True(list.AppliesOn(Today));
        Assert.False(list.AppliesOn(Today.AddDays(-1)));
        Assert.Throws<BusinessRuleException>(() => list.SetLines([(new PriceListLineData(null, item.Id, Cai.Id, 0, 1, 0, null, null), item)]));
        list.Deactivate();
        Assert.False(list.AppliesOn(Today));
    }

    [Fact]
    public void BangGia_GuiDuyet_TuChoiVeNhap_GuiLai_DuyetXongMoiHieuLuc()
    {
        var item = NewItem();
        var list = SupplierPriceList.Create(Company, "PRL-2", new PriceListHeader(Guid.CreateVersion7(), "Bảng giá", false, Today, Today.AddDays(20), null, null));
        // Chưa có dòng thì không gửi duyệt được.
        Assert.Throws<BusinessRuleException>(() => list.Submit());
        list.SetLines([(new PriceListLineData(null, item.Id, Cai.Id, 0, 50_000, 0, null, null), item)]);
        Assert.Equal(DocumentTypes.PriceList, list.DocType);
        Assert.Equal(0m, list.ApprovalAmount);

        list.Submit();
        Assert.Equal(PriceListStatus.Submitted, list.Status);
        Assert.True(list.IsAwaitingApproval);
        // Chờ duyệt thì không sửa, không ngừng được.
        Assert.Throws<BusinessRuleException>(() => list.SetLines([(new PriceListLineData(null, item.Id, Cai.Id, 0, 1, 0, null, null), item)]));
        Assert.Throws<BusinessRuleException>(list.Deactivate);

        list.ReturnToDraft("  Giá cao hơn hợp đồng cũ ");
        Assert.Equal((PriceListStatus.Draft, "Giá cao hơn hợp đồng cũ"), (list.Status, list.RejectReason));
        Assert.Equal(2, list.Submit());
        Assert.Null(list.RejectReason);
        list.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(PriceListStatus.Active, list.Status);
        Assert.NotNull(list.ActivatedAt);
        Assert.Throws<BusinessRuleException>(() => list.MarkApproved(DateTimeOffset.UtcNow));

        // Sắp hết hiệu lực: còn ≤ 30 ngày tới ngày hết hạn (hết hạn sau 20 ngày).
        Assert.Equal(20, list.DaysToExpiry(Today));
        Assert.True(list.ExpiresWithin(Today, 30));
        Assert.False(list.ExpiresWithin(Today, 10));
    }

    private static Rfq SentRfq(Item item, params Guid[] suppliers) => SentRfq(item, 3, suppliers);

    /// <summary>RFQ hai dòng đã gửi cho các NCC, số NCC tối thiểu theo tham số công ty.</summary>
    private static Rfq SentRfq(Item item, int minSuppliers, params Guid[] suppliers)
    {
        var rfq = Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi quý 4", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item), (new RfqLineData(null, item.Id, Thung.Id, 10, null, null), item)]);
        rfq.SetSuppliers(suppliers);
        rfq.Send(DateTimeOffset.UtcNow, minSuppliers, null);
        return rfq;
    }

    [Fact]
    public void Rfq_GuiItHon3Ncc_TrongSoKhacTram_BiChan()
    {
        var item = NewItem();
        Assert.Throws<BusinessRuleException>(() =>
            Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi", Today.AddDays(7), null, new RfqWeights(50, 20, 10, 5, 5), null)));
        Assert.Throws<BusinessRuleException>(() =>
            Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi", Today.AddDays(-1), null, RfqWeights.Default, null)));
        var ex = Assert.Throws<BusinessRuleException>(() => SentRfq(item, Guid.CreateVersion7(), Guid.CreateVersion7()));
        Assert.Contains("ít hơn 3", ex.Message);
        // Công ty hạ tham số xuống 2 thì gửi được với 2 NCC.
        Assert.Equal(RfqStatus.Sent, SentRfq(item, 2, Guid.CreateVersion7(), Guid.CreateVersion7()).Status);
    }

    [Fact]
    public void Rfq_ThieuNcc_CoGiaiTrinh_GuiDuoc()
    {
        // Lát B3 (M3): thiếu NCC so với tham số vẫn gửi được khi có giải trình; không có NCC nào thì không gửi.
        var item = NewItem();
        var rfq = Rfq.Create(Company, "RFQ-1", new RfqHeader(Today, "Vòng bi", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item)]);
        Assert.Throws<BusinessRuleException>(() => rfq.Send(DateTimeOffset.UtcNow, 3, "Hàng độc quyền"));
        rfq.SetSuppliers([Guid.CreateVersion7()]);
        Assert.Contains("giải trình", Assert.Throws<BusinessRuleException>(() => rfq.Send(DateTimeOffset.UtcNow, 3, "  ")).Message);
        rfq.Send(DateTimeOffset.UtcNow, 3, " Chỉ một NCC phân phối chính hãng ");
        Assert.Equal((RfqStatus.Sent, "Chỉ một NCC phân phối chính hãng"), (rfq.Status, rfq.FewSuppliersReason));

        // Đủ NCC thì giải trình không lưu (không có ngoại lệ).
        var full = SentRfq(item, 1, Guid.CreateVersion7());
        Assert.Null(full.FewSuppliersReason);
    }

    /// <summary>
    /// Lát R7 (design.md mục 18.26): quá hạn nộp (hoặc ngày báo giá sau hạn) thì không nhập báo giá – gia hạn có lý do rồi nhập được; số NCC
    /// thực sự chào giá ít hơn tối thiểu thì chọn thầu phải có giải trình ít báo giá.
    /// </summary>
    [Fact]
    public void Rfq_HanNop_GiaHan_ItBaoGiaPhaiGiaiTrinh()
    {
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var rfq = SentRfq(item, a, b, c);
        var cai = rfq.Lines[0].Id;
        QuotationHeader Header(DateOnly date) => new(date, null, null, 8, 8, 8, 8, null);
        var deadline = rfq.Deadline;

        Assert.Contains("quá hạn nộp", Assert.Throws<BusinessRuleException>(() =>
            rfq.SaveQuotation(a, Header(Today), [new(cai, 50_000, 0, null, null, null)], deadline.AddDays(1))).Message);
        Assert.Contains("sau hạn nộp", Assert.Throws<BusinessRuleException>(() =>
            rfq.SaveQuotation(a, Header(deadline.AddDays(1)), [new(cai, 50_000, 0, null, null, null)], Today)).Message);

        Assert.Throws<BusinessRuleException>(() => rfq.ExtendDeadline(deadline, "Thêm NCC", Today));
        Assert.Throws<BusinessRuleException>(() => rfq.ExtendDeadline(deadline.AddDays(5), " ", Today));
        rfq.ExtendDeadline(deadline.AddDays(5), " NCC xin thêm thời gian ", Today);
        Assert.Equal((deadline.AddDays(5), "NCC xin thêm thời gian"), (rfq.Deadline, rfq.DeadlineExtensionReason));
        rfq.SaveQuotation(a, Header(deadline.AddDays(1)), [new(cai, 50_000, 0, null, null, null)], deadline.AddDays(1));
        Assert.Equal(1, rfq.QuotedSupplierCount);

        // Mời 3 nhưng chỉ 1 chào giá, tối thiểu 3: phải giải trình.
        Assert.Contains("giải trình ít báo giá", Assert.Throws<BusinessRuleException>(() =>
            rfq.Award([new(cai, a, null)], Guid.CreateVersion7(), Today, 3, null)).Message);
        rfq.Award([new(cai, a, null)], Guid.CreateVersion7(), Today, 3, " Hai NCC không phản hồi ");
        Assert.Equal("Hai NCC không phản hồi", rfq.FewQuotesReason);
    }

    [Fact]
    public void BieuMau_TheoLoaiMuaVaNguong()
    {
        // Lát Y6 (design.md 19.1 Q7): loại A theo ngưỡng; ngưỡng 0 → mọi phiếu A là 3A; loại B, dịch vụ luôn 3B.
        Assert.Equal(RfqForm.Comparison, RfqForms.For(PurchaseCategory.Construction, 64_000_000, 50_000_000));
        Assert.Equal(RfqForm.Comparison, RfqForms.For(PurchaseCategory.Construction, 50_000_000, 50_000_000));
        Assert.Equal(RfqForm.SingleQuote, RfqForms.For(PurchaseCategory.Construction, 30_000_000, 50_000_000));
        Assert.Equal(RfqForm.Comparison, RfqForms.For(PurchaseCategory.Construction, 1, 0));
        Assert.Equal(RfqForm.Goods, RfqForms.For(PurchaseCategory.Goods, 999_000_000, 0));
        Assert.Equal(RfqForm.Goods, RfqForms.For(PurchaseCategory.Service, 1, 0));
        Assert.Equal(("3A", "3a", "3B"), (RfqForms.Code(RfqForm.Comparison), RfqForms.Code(RfqForm.SingleQuote), RfqForms.Code(RfqForm.Goods)));
    }

    [Fact]
    public void ChonThau_BieuMau3A_BatBuocLyDoChon_3a_3B_KhongBatBuoc()
    {
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var rfq = Rfq.Create(Company, "RFQ-A", new RfqHeader(Today, "Xà gồ nhà tứ diện", Today.AddDays(7), null, RfqWeights.Default, null));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item)]);
        Assert.Throws<BusinessRuleException>(() => rfq.SetPurchaseCategory(PurchaseCategory.Construction, null));
        var project = Guid.CreateVersion7();
        rfq.SetPurchaseCategory(PurchaseCategory.Construction, project);
        Assert.Equal((PurchaseCategory.Construction, project), (rfq.Category, rfq.ConstructionProjectId));
        rfq.SetSuppliers([a, b, c]);
        rfq.Send(DateTimeOffset.UtcNow, 3, null);
        var header = new QuotationHeader(Today, null, "CK sau 7 ngày", 8, 8, 8, 8, null);
        var line = rfq.Lines[0].Id;
        rfq.SaveQuotation(a, header, [new(line, 300_000, 0, "10", 2, null)], Today);
        rfq.SaveQuotation(b, header, [new(line, 306_364, 0, "10", 3, null)], Today);
        rfq.SaveQuotation(c, header, [new(line, 305_455, 0, "10", 4, null)], Today);
        var user = Guid.CreateVersion7();

        // Trúng thầu 30.000.000 ≥ ngưỡng 20.000.000 → 3A, thiếu lý do chọn thì chặn và không ghi lựa chọn.
        Assert.Contains("lý do chọn", Assert.Throws<BusinessRuleException>(() =>
            rfq.Award([new(line, a, null)], user, Today, 3, null, 20_000_000m, " ")).Message);
        Assert.Null(rfq.Lines[0].AwardedSupplierId);
        rfq.Award([new(line, a, null)], user, Today, 3, null, 20_000_000m, " Giá tốt hơn, gồm vận chuyển ");
        Assert.Equal((RfqForm.Comparison, "Giá tốt hơn, gồm vận chuyển"), (rfq.Form, rfq.SelectionReason));

        // Ngưỡng 50.000.000 → 3a: không bắt buộc lý do chọn.
        rfq.Award([new(line, a, null)], user, Today, 3, null, 50_000_000m);
        Assert.Equal((RfqForm.SingleQuote, (string?)null), (rfq.Form, rfq.SelectionReason));

        // Loại B: 3B.
        var goods = SentRfq(item, a, b, c);
        var cai = goods.Lines[0].Id;
        goods.SaveQuotation(a, header, [new(cai, 50_000, 0, "10", 7, null)], Today);
        goods.Award([new(cai, a, null)], user, Today, 1, null);
        Assert.Equal((PurchaseCategory.Goods, RfqForm.Goods), (goods.Category, goods.Form));
    }

    [Fact]
    public void ChonThau_KhongChonGiaThapNhatPhaiCoLyDo()
    {
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var rfq = SentRfq(item, a, b, c);
        var (cai, thung) = (rfq.Lines[0].Id, rfq.Lines[1].Id);
        var header = new QuotationHeader(Today, null, "30 ngày", 8, 10, 10, 6, null);
        rfq.SaveQuotation(a, header, [new(cai, 50_000, 0, "10", 7, null), new(thung, 600_000, 5, "10", 7, null)], Today);
        rfq.SaveQuotation(b, header, [new(cai, 48_000, 0, "10", 14, null)], Today);
        Assert.Throws<BusinessRuleException>(() => rfq.SaveQuotation(Guid.CreateVersion7(), header, [new(cai, 1, 0, null, null, null)], Today));

        var user = Guid.CreateVersion7();
        // Dòng thùng: B không chào → không chọn được B.
        Assert.Throws<BusinessRuleException>(() => rfq.Award([new(thung, b, null)], user, Today, 1, null));
        // Dòng cái: A đắt hơn B → chọn A phải có lý do.
        Assert.Throws<BusinessRuleException>(() => rfq.Award([new(cai, a, null)], user, Today, 1, null));
        // Chưa chọn thầu thì chưa gửi duyệt được.
        Assert.Throws<BusinessRuleException>(() => rfq.Submit());
        rfq.Award([new(cai, a, "Giao nhanh hơn"), new(thung, a, null)], user, Today, 1, null);
        Assert.Equal((RfqStatus.Sent, a, "Giao nhanh hơn"), (rfq.Status, rfq.Lines[0].AwardedSupplierId, rfq.Lines[0].AwardReason));

        // Lát B3 (M4): kết quả chọn thầu phải qua duyệt. Giá trị = 100 × 50.000 + 10 × 600.000 × 95% = 10.700.000.
        Assert.Equal(1, rfq.Submit());
        Assert.Equal((RfqStatus.AwardPending, true, 10_700_000m, user), (rfq.Status, rfq.IsAwaitingApproval, rfq.ApprovalAmount, rfq.AwardedBy));
        Assert.True(((IApprovalSubject)rfq).IsPreparedBy(user));
        Assert.Throws<BusinessRuleException>(() => rfq.SaveQuotation(a, header, [new(cai, 1, 0, null, null, null)], Today));
        Assert.Throws<BusinessRuleException>(() => rfq.Cancel("Thử hủy"));
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPurchaseOrder(a, Guid.CreateVersion7()));

        // Từ chối: về Đang nhận báo giá, giữ lựa chọn cũ làm gợi ý, lưu lý do; gửi lại thì xóa lý do, tăng lượt.
        rfq.ReturnToDraft("Cần thêm báo giá");
        Assert.Equal((RfqStatus.Sent, "Cần thêm báo giá", a), (rfq.Status, rfq.RejectReason, rfq.Lines[0].AwardedSupplierId));
        rfq.Award([new(cai, a, "Giao nhanh hơn"), new(thung, a, null)], user, Today, 1, null);
        Assert.Equal(2, rfq.Submit());
        Assert.Null(rfq.RejectReason);
        var at = DateTimeOffset.UtcNow;
        rfq.MarkApproved(at);
        Assert.Equal((RfqStatus.Awarded, at), (rfq.Status, rfq.AwardedAt));

        Assert.Throws<BusinessRuleException>(() => rfq.LinkPurchaseOrder(b, Guid.CreateVersion7()));
        rfq.LinkPurchaseOrder(a, Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPurchaseOrder(a, Guid.CreateVersion7()));
        Assert.Throws<BusinessRuleException>(() => rfq.Cancel("Thử hủy"));
    }

    [Fact]
    public void HoSoTrinh_DanhDauKhiChuaGuiDuyet_TaoBangGiaSauKhiDuyet()
    {
        // Lát B3b: hồ sơ trình đánh dấu được khi Nháp/Đang nhận báo giá; bảng so sánh tự có khi đã có báo giá; gửi duyệt thì khóa.
        var item = NewItem();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7();
        var rfq = SentRfq(item, 1, a, b);
        Assert.Equal(7, rfq.Dossier.Count);
        Assert.All(rfq.Dossier, d => Assert.False(d.IsDone));
        Assert.Throws<BusinessRuleException>(() => rfq.SetDossier([new(RfqDossier.Comparison, true, null)]));
        Assert.Throws<BusinessRuleException>(() => rfq.SetDossier([new("KHAC", true, null)]));
        Assert.Throws<BusinessRuleException>(() => rfq.SetDossier([new("MINUTES", true, null), new("MINUTES", false, null)]));
        rfq.SetDossier([new("MINUTES", true, " BB-01/11 "), new("BUDGET", false, null)]);
        rfq.SetDossier([new("BUDGET", true, null)]);
        var cai = rfq.Lines[0].Id;
        rfq.SaveQuotation(a, new QuotationHeader(Today, null, null, 8, 8, 8, 8, null), [new(cai, 50_000, 0, null, null, null)], Today);
        var dossier = rfq.Dossier.ToDictionary(d => d.Code);
        Assert.Equal((true, true), (dossier[RfqDossier.Comparison].IsDone, dossier[RfqDossier.Comparison].IsAutomatic));
        Assert.Equal((true, "BB-01/11"), (dossier["MINUTES"].IsDone, dossier["MINUTES"].Note));
        Assert.True(dossier["BUDGET"].IsDone);
        Assert.Equal(3, rfq.Dossier.Count(d => d.IsDone));

        rfq.Award([new(cai, a, null)], Guid.CreateVersion7(), Today, 1, null);
        // Chưa duyệt kết quả thì chưa tạo bảng giá.
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPriceList(a, Guid.CreateVersion7()));
        rfq.Submit();
        Assert.Throws<BusinessRuleException>(() => rfq.SetDossier([new("COST", true, null)]));
        rfq.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPriceList(b, Guid.CreateVersion7()));
        rfq.LinkPriceList(a, Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(() => rfq.LinkPriceList(a, Guid.CreateVersion7()));
    }

    [Fact]
    public void ChamDiem_TheoTrongSo()
    {
        // Dòng 1 thấp nhất (10 điểm), dòng 2 đắt hơn 25% (8 điểm) → 9.
        Assert.Equal(9m, QuotationScoring.PriceScore([(100m, 100m), (100m, 125m)]));
        Assert.Equal(8.8m, QuotationScoring.Total(RfqWeights.Default, 9, 8, 10, 10, 6));
    }

    [Fact]
    public void TimNguon_TheoDoiTienDoTuRfq_KhongLuiTrangThai()
    {
        var supplier = Guid.CreateVersion7();
        var request = SourcingRequest.Create(Company, "SRC-1", new SourcingHeader(Today, null, "Vòng bi chịu nhiệt", null, 50, null, "Anh Nam", null));
        Assert.Throws<BusinessRuleException>(() => request.Update(new SourcingHeader(Today, null, " ", null, null, null, null, null)));
        request.TrackRfq(supplier, CandidateStatus.RfqSent);
        Assert.Equal(CandidateStatus.RfqSent, request.Candidates.Single().Status);
        request.SetCandidates([new(request.Candidates[0].Id, supplier, CandidateStatus.Passed, "Có ISO")]);
        request.TrackRfq(supplier, CandidateStatus.Quoted);
        Assert.Equal(CandidateStatus.Passed, request.Candidates.Single().Status);
        request.Close();
        Assert.Throws<BusinessRuleException>(() => request.SetCandidates([]));
    }
}
