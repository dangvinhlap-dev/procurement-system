using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Tests.MasterData;

/// <summary>Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25).</summary>
public class SupplierOnboardingTests
{
    private static readonly SupplierGroup Group = SupplierGroup.Create("TN", "Trong nước");
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Guid Category = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly Guid Evaluator = Guid.CreateVersion7();
    private static readonly Guid Proposer = Guid.CreateVersion7();

    private static Supplier NewSupplier() =>
        Supplier.Create("NCC01", Group, new SupplierInfo("Rau sạch Đà Lạt", null, false, null, null, null, null, null, null, null, null));

    private static SupplierOnboardingData Data(
        SupplierOnboardingResult? result = SupplierOnboardingResult.Pass, string? comment = null, bool license = true, DateOnly? licenseTo = null,
        int?[]? scores = null)
    {
        var s = scores ?? [4, 5, 4, 3, 4, 3];
        return new SupplierOnboardingData(Today, Category, "Hội chợ nông sản", license ? [new(SupplierOnboardingDocuments.License, true, licenseTo, "0312345678")] : [],
            new SupplierOnboardingScores(s[0], s[1], s[2], s[3], s[4], s[5]), result, comment, "Ghi chú hồ sơ");
    }

    private static SupplierOnboarding New(SupplierOnboardingData? data = null) => SupplierOnboarding.Create(Company, "HSN-01", NewSupplier(), data ?? Data(), Today);

    [Fact]
    public void NccMoiTao_PhaiCoHoSo_ChiLapChoNccTiemNang()
    {
        var supplier = NewSupplier();
        Assert.True(supplier.RequiresOnboarding);
        supplier.SetStatus(SupplierStatus.Suspended, "Đang khiếu nại");
        Assert.Contains("chỉ lập cho NCC Tiềm năng", Assert.Throws<BusinessRuleException>(() => SupplierOnboarding.Create(Company, "HSN-01", supplier, Data(), Today)).Message);
    }

    [Fact]
    public void LuuDuDanhMucGiayTo_ChuaNopThiBoNgayHieuLuc_GiayToLaBiTuChoi()
    {
        var data = Data() with
        {
            Documents = [new(SupplierOnboardingDocuments.License, true, Today.AddYears(1), null), new(SupplierOnboardingDocuments.Quality, false, Today, "Chưa có")],
        };
        var onboarding = New(data);
        Assert.Equal(SupplierOnboardingDocuments.All.Select(d => d.Code), onboarding.Documents.Select(d => d.Code));
        var quality = onboarding.Documents.Single(d => d.Code == SupplierOnboardingDocuments.Quality);
        Assert.Equal((false, (DateOnly?)null, "Chưa có"), (quality.Provided, quality.ValidTo, quality.Note));

        Assert.Throws<BusinessRuleException>(() => New(Data() with { Documents = [new("XYZ", true, null, null)] }));
        Assert.Throws<BusinessRuleException>(() => New(Data() with { Date = Today.AddDays(1) }));
        Assert.Throws<BusinessRuleException>(() => New(Data(scores: [6, 5, 4, 3, 4, 3])));
    }

    [Fact]
    public void HoanThanhDanhGia_TinhDiemBinhQuan_DatCanGiayPhepConHieuLuc()
    {
        // 4 + 5 + 4 + 3 + 4 + 3 = 23 ÷ 6 = 3,8333 → 3,83.
        var onboarding = New();
        onboarding.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        Assert.Equal((SupplierOnboardingStatus.Evaluated, 3.83m, Evaluator), (onboarding.Status, onboarding.Score, onboarding.EvaluatedBy));

        Assert.Contains("giấy phép kinh doanh", Assert.Throws<BusinessRuleException>(() => New(Data(license: false)).Evaluate(DateTimeOffset.UtcNow, Evaluator)).Message);
        Assert.Contains("hết hiệu lực", Assert.Throws<BusinessRuleException>(() => New(Data(licenseTo: Today.AddDays(-1))).Evaluate(DateTimeOffset.UtcNow, Evaluator)).Message);
        Assert.Contains("6 tiêu chí", Assert.Throws<BusinessRuleException>(() => New(Data(scores: [4, 5, null, 3, 4, 3])).Evaluate(DateTimeOffset.UtcNow, Evaluator)).Message);
        // Không đạt (hoặc tiêu chí ≤ 2) phải có nhận xét; Không đạt thì không cần giấy phép.
        Assert.Contains("nhận xét", Assert.Throws<BusinessRuleException>(() => New(Data(SupplierOnboardingResult.Fail, license: false)).Evaluate(DateTimeOffset.UtcNow, Evaluator)).Message);
        Assert.Contains("nhận xét", Assert.Throws<BusinessRuleException>(() => New(Data(scores: [2, 5, 4, 3, 4, 3])).Evaluate(DateTimeOffset.UtcNow, Evaluator)).Message);
        var fail = New(Data(SupplierOnboardingResult.Fail, "Năng lực cung ứng thấp", license: false));
        fail.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        Assert.Equal(SupplierOnboardingStatus.Evaluated, fail.Status);
    }

    [Fact]
    public void MoLaiDanhGia_VeNhap_XoaDiem()
    {
        var onboarding = New();
        onboarding.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        Assert.Throws<BusinessRuleException>(() => onboarding.Update(Data(), Today));
        onboarding.Reopen();
        Assert.Equal((SupplierOnboardingStatus.Draft, (decimal?)null, (Guid?)null), (onboarding.Status, onboarding.Score, onboarding.EvaluatedBy));
        onboarding.Update(Data(scores: [5, 5, 5, 5, 5, 5]), Today);
    }

    [Fact]
    public void DeXuat_NguoiDanhGiaKhongTuDeXuat_KhongDatChiKhongDeXuat_RutVeDaDanhGia()
    {
        var onboarding = New();
        Assert.Throws<BusinessRuleException>(() => onboarding.EnsureCanRecommend(Proposer));
        onboarding.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        Assert.Contains("không tự đề xuất", Assert.Throws<BusinessRuleException>(() => onboarding.EnsureCanRecommend(Evaluator)).Message);

        var change = Guid.CreateVersion7();
        onboarding.Recommend("Giá tốt, mẫu đạt", DateTimeOffset.UtcNow, Proposer, change);
        Assert.Equal((SupplierOnboardingStatus.Submitted, true, change), (onboarding.Status, onboarding.Recommended, onboarding.ChangeRequestId));
        Assert.True(onboarding.IsOpen);
        Assert.Throws<BusinessRuleException>(() => onboarding.Cancel("Nhầm"));

        onboarding.Withdraw();
        Assert.Equal((SupplierOnboardingStatus.Evaluated, (bool?)null, (Guid?)null), (onboarding.Status, onboarding.Recommended, onboarding.ChangeRequestId));
        onboarding.Recommend(null, DateTimeOffset.UtcNow, Proposer, change);
        onboarding.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(SupplierOnboardingStatus.Approved, onboarding.Status);
        Assert.False(onboarding.IsOpen);

        var fail = New(Data(SupplierOnboardingResult.Fail, "Giá cao"));
        fail.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        Assert.Contains("Không đạt", Assert.Throws<BusinessRuleException>(() => fail.Recommend(null, DateTimeOffset.UtcNow, Proposer, change)).Message);
        Assert.Throws<BusinessRuleException>(() => fail.NotRecommend(" ", DateTimeOffset.UtcNow, Proposer));
        fail.NotRecommend("Giá cao hơn 2 NCC hiện có", DateTimeOffset.UtcNow, Proposer);
        Assert.Equal((SupplierOnboardingStatus.NotRecommended, false), (fail.Status, fail.Recommended));
    }

    [Fact]
    public void TuChoi_KetThuc_HuyChiKhiNhapHoacDaDanhGia()
    {
        var onboarding = New();
        onboarding.Evaluate(DateTimeOffset.UtcNow, Evaluator);
        onboarding.Recommend(null, DateTimeOffset.UtcNow, Proposer, Guid.CreateVersion7());
        onboarding.MarkRejected("Chưa đủ năng lực");
        Assert.Equal((SupplierOnboardingStatus.Rejected, "Chưa đủ năng lực"), (onboarding.Status, onboarding.RejectReason));

        var draft = New();
        Assert.Throws<BusinessRuleException>(() => draft.Cancel(" "));
        draft.Cancel("NCC rút lui");
        Assert.Equal(SupplierOnboardingStatus.Cancelled, draft.Status);
    }
}
