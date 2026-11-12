using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Sourcing;

namespace Proc.Domain.Tests.Sourcing;

/// <summary>
/// Lát P3a (design.md mục 19.5): hạn nộp kèm giờ; NCC nộp báo giá niêm phong trên Portal đến giờ hạn, nộp lại được; mở sau giờ hạn chép bản
/// cuối vào báo giá; báo giá Portal không sửa giá thay NCC, chỉ chấm điểm; đã mở thì không gia hạn.
/// </summary>
public class RfqPortalSubmissionTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly DateOnly Today = new(2026, 11, 12);

    /// <summary>RFQ một dòng 100 cái, hạn nộp hôm nay 17:00, mời hai NCC.</summary>
    private static (Rfq Rfq, Guid A, Guid B) SentRfq()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        var rfq = Rfq.Create(Company, "RFQ-P3", new RfqHeader(Today, "Vòng bi", Today, null, RfqWeights.Default, null, new TimeOnly(17, 0, 45)));
        rfq.SetLines([(new RfqLineData(null, item.Id, Cai.Id, 100, null, null), item)]);
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7();
        rfq.SetSuppliers([a, b]);
        rfq.Send(DateTimeOffset.UtcNow, 2, null);
        return (rfq, a, b);
    }

    private static DateTime At(int hour, int minute) => Today.ToDateTime(new TimeOnly(hour, minute));

    private static RfqPortalSubmission Submit(Rfq rfq, Guid supplier, int sequence, decimal price, DateTime now) =>
        RfqPortalSubmission.Create(rfq, supplier, sequence, Guid.CreateVersion7(), "ban@ncc.vn", DateTimeOffset.UtcNow, now,
            new PortalQuoteHeader(Today.AddDays(30), " 30 ngày ", null), [new(rfq.Lines[0].Id, price, 2, "10", 5, null)]);

    [Fact]
    public void HanNop_KemGio_BoGiay_QuaGioThiKhongNhap()
    {
        var (rfq, a, _) = SentRfq();
        Assert.Equal(new TimeOnly(17, 0), rfq.DeadlineTime);
        Assert.Equal("12/11/2026 17:00", rfq.DeadlineText);
        var header = new QuotationHeader(Today, null, null, 8, 8, 8, 8, null);
        rfq.SaveQuotation(a, header, [new(rfq.Lines[0].Id, 50_000, 0, null, null, null)], At(17, 0));
        Assert.Contains("quá hạn nộp báo giá 12/11/2026 17:00", Assert.Throws<BusinessRuleException>(() =>
            rfq.SaveQuotation(a, header, [new(rfq.Lines[0].Id, 50_000, 0, null, null, null)], At(17, 1))).Message);

        // Gia hạn: hạn mới phải sau hạn cũ (tính cả giờ) và không trước bây giờ.
        Assert.Throws<BusinessRuleException>(() => rfq.ExtendDeadline(Today, new TimeOnly(16, 0), "Thêm", At(9, 0)));
        rfq.ExtendDeadline(Today, new TimeOnly(20, 30), "NCC xin thêm giờ", At(17, 5));
        Assert.Equal("12/11/2026 20:30", rfq.DeadlineText);
        // RFQ không ghi giờ: hết ngày.
        Assert.Equal(RfqDeadline.EndOfDay, Rfq.Create(Company, "X", new RfqHeader(Today, "X", Today, null, RfqWeights.Default, null)).DeadlineTime);
    }

    [Fact]
    public void NopBaoGia_NiemPhong_NopLai_MoSauGioHan()
    {
        var (rfq, a, b) = SentRfq();
        var first = Submit(rfq, a, 1, 100_000, At(10, 0));
        var second = Submit(rfq, a, 2, 95_000, At(16, 59));
        Assert.Equal((Today, "30 ngày", "10"), (second.QuoteDate, second.PaymentTerms, second.Lines[0].VatCode));
        Assert.Equal(64, second.ContentHash.Length);
        Assert.NotEqual(first.ContentHash, second.ContentHash);
        Assert.Equal(second.ContentHash, second.ComputeHash());

        // Nộp chưa làm đổi báo giá trên RFQ – giá còn niêm phong.
        Assert.False(rfq.Suppliers.Single(s => s.SupplierId == a).HasQuoted);

        // Sai: NCC không được mời, quá giờ, dòng lạ, giá âm.
        Assert.Contains("không được mời", Assert.Throws<BusinessRuleException>(() => Submit(rfq, Guid.CreateVersion7(), 1, 1, At(9, 0))).Message);
        Assert.Contains("hết hạn", Assert.Throws<BusinessRuleException>(() => Submit(rfq, b, 1, 1, At(17, 1))).Message);
        Assert.Throws<BusinessRuleException>(() => RfqPortalSubmission.Create(rfq, b, 1, Guid.CreateVersion7(), "x@ncc.vn", DateTimeOffset.UtcNow,
            At(9, 0), new PortalQuoteHeader(null, null, null), [new(Guid.CreateVersion7(), 1, 0, null, null, null)]));
        Assert.Throws<BusinessRuleException>(() => Submit(rfq, b, 1, -1, At(9, 0)));

        // Chưa đến giờ thì không mở được.
        Assert.Contains("Chưa đến giờ mở", Assert.Throws<BusinessRuleException>(() =>
            rfq.OpenPortalQuotes([second], DateTimeOffset.UtcNow, Guid.CreateVersion7(), At(17, 0))).Message);

        // Phòng CUVT nhập thay cho B trước giờ hạn – vẫn được (B không dùng Portal).
        rfq.SaveQuotation(b, new QuotationHeader(Today, null, null, 7, 7, 7, 7, null), [new(rfq.Lines[0].Id, 98_000, 0, "10", 3, null)], At(11, 0));

        Assert.Equal(1, rfq.OpenPortalQuotes([second], DateTimeOffset.UtcNow, Guid.CreateVersion7(), At(17, 1)));
        var quoteA = rfq.Suppliers.Single(s => s.SupplierId == a);
        Assert.True(quoteA.HasQuoted);
        Assert.True(quoteA.IsFromPortal);
        Assert.Equal(second.Id, quoteA.PortalSubmissionId);
        Assert.Equal((95_000m, 2m, 0m), (quoteA.QuotationLines[0].UnitPrice, quoteA.QuotationLines[0].DiscountPct, quoteA.QualityScore));
        Assert.NotNull(rfq.PortalQuotesOpenedAt);

        // Đã mở: không mở lại, không gia hạn, không sửa giá NCC đã nộp; chấm điểm được.
        Assert.Throws<BusinessRuleException>(() => rfq.OpenPortalQuotes([second], DateTimeOffset.UtcNow, Guid.CreateVersion7(), At(18, 0)));
        Assert.Contains("đã mở báo giá Portal", Assert.Throws<BusinessRuleException>(() =>
            rfq.ExtendDeadline(Today.AddDays(1), null, "Thêm", At(18, 0))).Message);
        rfq.ScoreQuotation(a, 9, 8.5m, 7, 6);
        Assert.Equal((9m, 8.5m, 7m, 6m), (quoteA.QualityScore, quoteA.DeliveryScore, quoteA.PaymentScore, quoteA.WarrantyScore));
        Assert.Throws<BusinessRuleException>(() => rfq.ScoreQuotation(a, 11, 0, 0, 0));
        Assert.Throws<BusinessRuleException>(() => Submit(rfq, b, 1, 1, At(9, 0)));
    }

    [Fact]
    public void BaoGiaPortal_KhongSuaGiaThay()
    {
        var (rfq, a, _) = SentRfq();
        var submission = Submit(rfq, a, 1, 100_000, At(9, 0));
        rfq.ExtendDeadline(Today.AddDays(1), new TimeOnly(9, 0), "Gia hạn thử", At(9, 30));
        // Hạn mới chưa tới: mở bị chặn; quá giờ mở được.
        Assert.Throws<BusinessRuleException>(() => rfq.OpenPortalQuotes([submission], DateTimeOffset.UtcNow, Guid.CreateVersion7(), At(23, 0)));
        rfq.OpenPortalQuotes([submission], DateTimeOffset.UtcNow, Guid.CreateVersion7(), Today.AddDays(1).ToDateTime(new TimeOnly(9, 1)));
        // Sau khi mở, hạn đã qua nên mọi nhập tay bị chặn; nhưng kể cả trước hạn, NCC Portal không sửa thay được (kiểm cờ trước).
        Assert.Throws<BusinessRuleException>(() => rfq.SaveQuotation(a, new QuotationHeader(Today, null, null, 0, 0, 0, 0, null),
            [new(rfq.Lines[0].Id, 1, 0, null, null, null)], At(9, 0)));
    }
}
