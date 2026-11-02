using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Phiếu đánh giá NCC định kỳ theo quý (design.md mục 18.20).</summary>
public class SupplierReviewTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Supplier = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static SupplierReviewScores Full(int low = 4) => new(4, 4, 5, 3, 4, low);

    private static SupplierReviewData Data(SupplierReviewScores? scores = null, SupplierReviewResult? result = SupplierReviewResult.Continue,
        string? comment = null, DateOnly? date = null) =>
        new(date ?? new DateOnly(2026, 10, 15), scores ?? new SupplierReviewScores(null, null, null, null, null, null), result, comment);

    [Fact]
    public void KhoangQuy_TuDauDenCuoiQuy()
    {
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)), SupplierReview.QuarterRange(2026, 3));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), SupplierReview.QuarterRange(2026, 4));
        Assert.Throws<BusinessRuleException>(() => SupplierReview.QuarterRange(2026, 5));
    }

    [Fact]
    public void LapPhieu_QuyChuaBatDau_HoacNgayTruocQuy_BiChan()
    {
        var review = SupplierReview.Create(Company, "DGN-BHA-2611-0001", Supplier, 2026, 3, Data(), Today);
        Assert.Equal((SupplierReviewStatus.Draft, 2026, 3, (decimal?)null), (review.Status, review.Year, review.Quarter, review.Score));

        // Quý 1/2027 chưa bắt đầu tại 02/11/2026.
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Company, "DGN-1", Supplier, 2027, 1, Data(date: Today), Today));
        // Ngày đánh giá trước ngày đầu quý hoặc sau hôm nay.
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 4, Data(date: new DateOnly(2026, 9, 30)), Today));
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 3, Data(date: Today.AddDays(1)), Today));
        // Điểm ngoài 1–5.
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 3, Data(Full() with { Price = 6 }), Today));
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 3, Data(Full() with { Quality = 0 }), Today));
        Assert.Throws<BusinessRuleException>(() => SupplierReview.Create(Guid.Empty, "DGN-1", Supplier, 2026, 3, Data(), Today));
    }

    [Fact]
    public void HoanThanh_DuDiem_TinhDiemBinhQuan_KhoaPhieu()
    {
        var review = SupplierReview.Create(Company, "DGN-BHA-2611-0001", Supplier, 2026, 3, Data(), Today);
        // Thiếu điểm thì chưa hoàn thành được.
        Assert.Throws<BusinessRuleException>(() => review.Complete(DateTimeOffset.UtcNow, Guid.CreateVersion7()));

        review.Update(Data(Full()), Today);
        review.SetMetrics(new SupplierReviewMetrics(120_000_000m, 90m, 1.5m, 100m, "A"));
        var by = Guid.CreateVersion7();
        review.Complete(DateTimeOffset.UtcNow, by);

        // (4 + 4 + 5 + 3 + 4 + 4) ÷ 6 = 4,00.
        Assert.Equal((SupplierReviewStatus.Completed, (decimal?)4.00m, (Guid?)by, "A"), (review.Status, review.Score, review.CompletedBy, review.SystemRating));
        Assert.Throws<BusinessRuleException>(() => review.Update(Data(Full()), Today));
        Assert.Throws<BusinessRuleException>(() => review.SetMetrics(new SupplierReviewMetrics(0, null, null, null, null)));
        Assert.Throws<BusinessRuleException>(() => review.Cancel("Lập nhầm"));
    }

    [Fact]
    public void HoanThanh_KetLuanKhongTiepTuc_HoacDiemThap_BatBuocNhanXet()
    {
        var review = SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 3, Data(Full(), SupplierReviewResult.Monitor), Today);
        Assert.Throws<BusinessRuleException>(() => review.Complete(DateTimeOffset.UtcNow, Guid.CreateVersion7()));

        review.Update(Data(Full(low: 2)), Today);
        Assert.Throws<BusinessRuleException>(() => review.Complete(DateTimeOffset.UtcNow, Guid.CreateVersion7()));

        review.Update(Data(Full(), result: null), Today);
        Assert.Throws<BusinessRuleException>(() => review.Complete(DateTimeOffset.UtcNow, Guid.CreateVersion7()));

        review.Update(Data(Full(low: 2), SupplierReviewResult.Monitor, "Hồ sơ tuân thủ thiếu, theo dõi quý sau"), Today);
        review.Complete(DateTimeOffset.UtcNow, Guid.CreateVersion7());
        // (4 + 4 + 5 + 3 + 4 + 2) ÷ 6 = 3,666… → 3,67.
        Assert.Equal(3.67m, review.Score);
    }

    [Fact]
    public void Huy_ChiKhiNhap_BatBuocLyDo()
    {
        var review = SupplierReview.Create(Company, "DGN-1", Supplier, 2026, 3, Data(), Today);
        Assert.Throws<BusinessRuleException>(() => review.Cancel(" "));
        review.Cancel("Lập trùng NCC");
        Assert.Equal((SupplierReviewStatus.Cancelled, "Lập trùng NCC"), (review.Status, review.CancelReason));
        Assert.Throws<BusinessRuleException>(() => review.Update(Data(), Today));
    }
}
