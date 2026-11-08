namespace Proc.Domain.Pricing;

/// <summary>Sản lượng, doanh số mua của một công ty trong một kỳ (đã quy về ĐVT thỏa thuận).</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Quantity">SL theo ĐVT thỏa thuận.</param>
/// <param name="Amount">Doanh số mua trước VAT (VND).</param>
public sealed record VolumeRebateCompanyVolume(Guid CompanyId, decimal Quantity, decimal Amount);

/// <summary>Phần của một công ty: tỷ lệ doanh số và chiết khấu được chia.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Quantity">SL theo ĐVT thỏa thuận.</param>
/// <param name="Amount">Doanh số.</param>
/// <param name="SharePercent">Tỷ lệ doanh số (%, 4 số lẻ).</param>
/// <param name="Rebate">Chiết khấu được chia (2 số lẻ).</param>
public sealed record VolumeRebateShare(Guid CompanyId, decimal Quantity, decimal Amount, decimal SharePercent, decimal Rebate);

/// <summary>Kết quả một kỳ tính bậc – xem <see cref="VolumeRebateCalculation.Evaluate"/>.</summary>
public sealed record VolumeRebatePeriodResult(
    DateOnly From, DateOnly To, string State, decimal Volume, decimal Amount, VolumeRebateTier? Tier, decimal Rebate, VolumeRebateTier? Next,
    decimal? Shortfall, decimal? Forecast, VolumeRebateTier? Target, string TargetStatus, bool IsNearNextTier, IReadOnlyList<VolumeRebateShare> Shares);

/// <summary>
/// Tính lũy kế so với bậc chiết khấu sản lượng gộp (design.md mục 18.25 – lát G6a). Thuần tính toán, không đọc CSDL: tầng hạ tầng gom sản lượng
/// từ PO rồi gọi <see cref="Evaluate"/> cho từng kỳ.
/// </summary>
public static class VolumeRebateCalculation
{
    /// <summary>X% mặc định để cảnh báo sắp chạm bậc kế tiếp.</summary>
    public const decimal DefaultNearPercent = 10m;

    /// <summary>
    /// Tính một kỳ.
    /// <list type="bullet">
    /// <item>Lũy kế = Σ SL (tính theo số lượng) hoặc Σ doanh số (tính theo doanh số) của các công ty; bậc đạt = bậc ngưỡng lớn nhất ≤ lũy kế
    /// (bậc toàn phần).</item>
    /// <item>Chiết khấu gộp = doanh số gộp × % bậc ÷ 100, làm tròn 2 số. Ví dụ bậc 2 = 2%, doanh số 1.250.000.000 → 25.000.000.</item>
    /// <item>Chia theo tỷ lệ doanh số: phần công ty = chiết khấu gộp × doanh số công ty ÷ doanh số gộp, làm tròn 2 số; số lẻ còn lại dồn vào công
    /// ty doanh số lớn nhất để tổng các phần đúng bằng chiết khấu gộp. Ví dụ 100.000 chia 3 công ty doanh số bằng nhau → 33.333,34 /
    /// 33.333,33 / 33.333,33.</item>
    /// <item>Dự báo cuối kỳ (kỳ đang chạy) = lũy kế × số ngày của kỳ ÷ số ngày đã qua (tính cả hôm nay). Ví dụ kỳ tháng 30 ngày, đến ngày 10
    /// đã mua 120 tấn → dự báo 360 tấn.</item>
    /// <item>Sắp chạm bậc: kỳ đã bắt đầu, còn bậc kế tiếp và lũy kế ≥ ngưỡng bậc kế × (100 − X) ÷ 100. Ví dụ ngưỡng 200 tấn, X = 10% → từ 180 tấn.</item>
    /// <item>So bậc mục tiêu: đạt → Reached; chưa tới kỳ → NotStarted; hết kỳ chưa đạt → Missed; đang chạy: dự báo ≥ ngưỡng → OnTrack, ngược
    /// lại AtRisk (nguy cơ hụt bậc).</item>
    /// </list>
    /// </summary>
    /// <param name="agreement">Thỏa thuận (đã nạp bậc).</param>
    /// <param name="from">Ngày đầu kỳ.</param>
    /// <param name="to">Ngày cuối kỳ.</param>
    /// <param name="volumes">Sản lượng, doanh số từng công ty trong kỳ.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="nearPercent">X% cảnh báo sắp chạm bậc.</param>
    /// <returns>Kết quả kỳ.</returns>
    public static VolumeRebatePeriodResult Evaluate(
        this VolumeRebateAgreement agreement, DateOnly from, DateOnly to, IReadOnlyList<VolumeRebateCompanyVolume> volumes, DateOnly today,
        decimal nearPercent)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        var byQuantity = agreement.Basis == VolumeRebateBasis.Quantity;
        var decimals = byQuantity ? 4 : 2;
        var quantity = volumes.Sum(v => v.Quantity);
        var amount = volumes.Sum(v => v.Amount);
        var volume = decimal.Round(byQuantity ? quantity : amount, decimals, MidpointRounding.AwayFromZero);
        var state = today < from ? "Future" : today > to ? "Ended" : "Open";

        var tier = agreement.TierFor(volume);
        var rebate = tier is null ? 0m : decimal.Round(amount * tier.Percent / 100m, 2, MidpointRounding.AwayFromZero);
        var next = agreement.Tiers.Where(t => t.FromValue > volume).MinBy(t => t.FromValue);
        decimal? forecast = null;
        if (state == "Open")
        {
            var elapsed = today.DayNumber - from.DayNumber + 1;
            var total = to.DayNumber - from.DayNumber + 1;
            forecast = decimal.Round(volume * total / elapsed, decimals, MidpointRounding.AwayFromZero);
        }

        var near = next is not null && state != "Future" && volume >= next.FromValue * (100m - nearPercent) / 100m;
        var target = agreement.TargetTierNo is { } no ? agreement.Tiers.FirstOrDefault(t => t.TierNo == no) : null;
        var targetStatus = target is null ? "None"
            : volume >= target.FromValue ? "Reached"
            : state == "Future" ? "NotStarted"
            : state == "Ended" ? "Missed"
            : forecast >= target.FromValue ? "OnTrack" : "AtRisk";
        return new VolumeRebatePeriodResult(
            from, to, state, volume, amount, tier, rebate, next, next is null ? null : next.FromValue - volume, forecast, target, targetStatus, near,
            Allocate(volumes, amount, rebate));
    }

    /// <summary>Chia chiết khấu theo tỷ lệ doanh số, dồn số lẻ làm tròn vào công ty doanh số lớn nhất (xem <see cref="Evaluate"/>).</summary>
    private static List<VolumeRebateShare> Allocate(IReadOnlyList<VolumeRebateCompanyVolume> volumes, decimal amount, decimal rebate)
    {
        var shares = volumes.Select(v => new VolumeRebateShare(
                v.CompanyId, v.Quantity, v.Amount,
                amount == 0 ? 0m : decimal.Round(v.Amount * 100m / amount, 4, MidpointRounding.AwayFromZero),
                amount == 0 ? 0m : decimal.Round(rebate * v.Amount / amount, 2, MidpointRounding.AwayFromZero)))
            .ToList();
        var diff = rebate - shares.Sum(s => s.Rebate);
        if (diff != 0 && shares.Count > 0)
        {
            var largest = shares.IndexOf(shares.MaxBy(s => s.Amount)!);
            shares[largest] = shares[largest] with { Rebate = shares[largest].Rebate + diff };
        }

        return shares;
    }
}
