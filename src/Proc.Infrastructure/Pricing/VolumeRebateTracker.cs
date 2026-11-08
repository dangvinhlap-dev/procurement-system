using Microsoft.EntityFrameworkCore;
using Proc.Domain.Documents;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Gom sản lượng và thực nhận của thỏa thuận chiết khấu sản lượng gộp (design.md mục 18.25 – lát G6a, G6b). Dùng chung cho màn hình CKB/CKBT
/// và dấu hiệu KSNB "Chiết khấu sản lượng gộp chưa ghi nhận" (mục 18.19) để các nơi ra cùng một số. Người gọi phải đã kiểm tra quyền xem
/// các thỏa thuận truyền vào – các truy vấn ở đây bỏ filter công ty có chủ đích (số gộp của nhiều pháp nhân).
/// </summary>
internal static class VolumeRebateTracker
{
    /// <summary>Một nhóm dòng PO: công ty × NCC × mặt hàng × ngày.</summary>
    internal sealed record VolumeRow(Guid CompanyId, Guid SupplierId, Guid ItemId, DateOnly Date, decimal BaseQty, decimal Amount);

    /// <summary>Thực nhận chưa hủy: tổng theo thỏa thuận × kỳ và phần theo thỏa thuận × kỳ × công ty.</summary>
    internal sealed record Received(
        IReadOnlyDictionary<(Guid AgreementId, DateOnly PeriodFrom), decimal> Total,
        IReadOnlyDictionary<(Guid AgreementId, DateOnly PeriodFrom, Guid CompanyId), decimal> ByCompany);

    /// <summary>
    /// Sản lượng mua của các thỏa thuận: dòng PO <b>Đã duyệt/Đã đóng</b> bằng VND (cùng nguồn số với chiết khấu hợp đồng mục 18.21 và PRCW), của
    /// các công ty áp dụng × NCC × mặt hàng, ngày PO trong thời hạn. Bỏ filter công ty và nhóm hàng: sản lượng là của các pháp nhân khác nhau
    /// gộp lại (quyết định Ban cung ứng); chỉ trả số tổng theo ngày, không trả chi tiết PO công ty khác.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="agreements">Thỏa thuận (đã nạp dòng, công ty áp dụng).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các nhóm dòng PO.</returns>
    public static async Task<List<VolumeRow>> VolumesAsync(
        ProcDbContext db, IReadOnlyList<VolumeRebateAgreement> agreements, CancellationToken cancellationToken)
    {
        if (agreements.Count == 0) return [];
        var supplierIds = agreements.Select(a => a.SupplierId).Distinct().ToList();
        var companyIds = agreements.SelectMany(a => a.Companies.Select(c => c.CompanyId)).Distinct().ToList();
        var itemIds = agreements.SelectMany(a => a.Lines.Select(l => l.ItemId)).Distinct().ToList();
        var min = agreements.Min(a => a.ValidFrom);
        var max = agreements.Max(a => a.ValidTo);
        return await (
                from o in db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.ItemCategoryFilter]).AsNoTracking()
                where (o.Status == ApprovalDocumentStatus.Approved || o.Status == ApprovalDocumentStatus.Closed) && o.CurrencyCode == "VND"
                      && supplierIds.Contains(o.SupplierId) && companyIds.Contains(o.CompanyId) && o.Date >= min && o.Date <= max
                from l in o.Lines
                where itemIds.Contains(l.ItemId)
                group l by new { o.CompanyId, o.SupplierId, l.ItemId, o.Date } into g
                select new VolumeRow(g.Key.CompanyId, g.Key.SupplierId, g.Key.ItemId, g.Key.Date, g.Sum(x => x.BaseQty), g.Sum(x => x.Amount)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sản lượng, doanh số từng công ty áp dụng trong kỳ: SL = Σ SL cơ sở ÷ hệ số ĐVT dòng thỏa thuận (4 số lẻ), doanh số = Σ thành tiền. Sắp
    /// theo mã công ty để phần chia số lẻ ổn định khi hai công ty doanh số bằng nhau.
    /// </summary>
    /// <param name="agreement">Thỏa thuận.</param>
    /// <param name="from">Đầu kỳ.</param>
    /// <param name="to">Cuối kỳ.</param>
    /// <param name="rows">Kết quả <see cref="VolumesAsync"/>.</param>
    /// <param name="codes">Mã công ty theo Id.</param>
    /// <returns>Sản lượng từng công ty có mua trong kỳ.</returns>
    public static List<VolumeRebateCompanyVolume> PeriodVolumes(
        VolumeRebateAgreement agreement, DateOnly from, DateOnly to, IReadOnlyList<VolumeRow> rows, IReadOnlyDictionary<Guid, string> codes)
    {
        var factors = agreement.Lines.ToDictionary(l => l.ItemId, l => l.UomFactor);
        var applied = agreement.Companies.Select(c => c.CompanyId).ToHashSet();
        return rows.Where(r => r.SupplierId == agreement.SupplierId && applied.Contains(r.CompanyId) && factors.ContainsKey(r.ItemId)
                               && r.Date >= from && r.Date <= to)
            .GroupBy(r => r.CompanyId)
            .Select(g => new VolumeRebateCompanyVolume(
                g.Key, decimal.Round(g.Sum(r => r.BaseQty / factors[r.ItemId]), 4, MidpointRounding.AwayFromZero), g.Sum(r => r.Amount)))
            .OrderBy(v => codes.GetValueOrDefault(v.CompanyId), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Thực nhận chưa hủy của các thỏa thuận. Bỏ filter công ty: bản ghi thuộc tổng công ty nhưng công ty áp dụng cần biết phần của mình.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="agreementIds">Thỏa thuận người gọi đã được xem.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tổng và phần từng công ty.</returns>
    public static async Task<Received> ReceivedAsync(ProcDbContext db, IReadOnlyList<Guid> agreementIds, CancellationToken cancellationToken)
    {
        var shares = await db.VolumeRebateReceipts.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(r => agreementIds.Contains(r.AgreementId) && !r.IsCancelled)
            .SelectMany(r => r.Shares.Select(s => new { r.AgreementId, r.PeriodFrom, s.CompanyId, s.Amount }))
            .ToListAsync(cancellationToken);
        return new Received(
            shares.GroupBy(s => (s.AgreementId, s.PeriodFrom)).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount)),
            shares.GroupBy(s => (s.AgreementId, s.PeriodFrom, s.CompanyId)).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount)));
    }
}
