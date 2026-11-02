using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Đánh giá NCC định kỳ theo quý (design.md mục 18.20). Số liệu tham khảo lấy đúng từ báo cáo đánh giá NCC (SUPEVAL) và cách đếm kiểm tra chất
/// lượng (QCDEV) của quý, để phiếu khớp với báo cáo người dùng đã xem. Bảng lọc theo công ty đang làm việc (global query filter).
/// </summary>
public sealed class SupplierReviewService : ISupplierReviewService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IDocumentNumberService _numbers;
    private readonly IPurchaseReportService _reports;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="reports">Báo cáo mua hàng (đánh giá NCC, chất lượng).</param>
    public SupplierReviewService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IDocumentNumberService numbers, IPurchaseReportService reports)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _numbers = numbers;
        _reports = reports;
    }

    /// <inheritdoc />
    public async Task<SupplierReviewBoard> GetBoardAsync(int year, int quarter, CancellationToken cancellationToken)
    {
        EnsureView();
        var (from, to) = SupplierReview.QuarterRange(year, quarter);
        var metrics = await MetricsAsync(from, to, cancellationToken);
        var reviews = await (
                from r in _db.SupplierReviews.AsNoTracking()
                where r.Year == year && r.Quarter == quarter && r.Status != SupplierReviewStatus.Cancelled
                join s in _db.Suppliers on r.SupplierId equals s.Id
                select new
                {
                    r.Id, r.No, r.SupplierId, s.Code, s.Name, r.Status, r.Date, r.Score, r.Result,
                    Reviewer = _db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);
        var byId = reviews.ToDictionary(r => r.SupplierId);

        var supplierIds = metrics.Keys.Union(byId.Keys).ToList();
        var names = metrics.ToDictionary(m => m.Key, m => (m.Value.Code, m.Value.Name));
        foreach (var r in reviews) names.TryAdd(r.SupplierId, (r.Code, r.Name));

        var rows = supplierIds
            .Select(id =>
            {
                var m = metrics.GetValueOrDefault(id);
                var r = byId.GetValueOrDefault(id);
                var (code, name) = names[id];
                return new SupplierReviewBoardRowDto(
                    id, code, name, m is { IsDue: true }, m?.Metrics.ReceiptAmount ?? 0, m?.Metrics.OnTimePct, m?.Metrics.ReturnPct,
                    m?.Metrics.QualityPct, m?.Metrics.SystemRating, r?.Id, r?.No, r?.Status.ToString(), r?.Date, r?.Score, r?.Result?.ToString(),
                    r?.Reviewer);
            })
            // Việc còn phải làm lên đầu: thuộc diện mà chưa hoàn thành, rồi giá trị nhập lớn trước.
            .OrderBy(r => r.IsDue && r.Status != nameof(SupplierReviewStatus.Completed) ? 0 : 1)
            .ThenByDescending(r => r.ReceiptAmount).ThenBy(r => r.SupplierCode)
            .ToList();
        var due = rows.Count(r => r.IsDue);
        var completed = rows.Count(r => r.IsDue && r.Status == nameof(SupplierReviewStatus.Completed));
        return new SupplierReviewBoard(year, quarter, from, to, due, completed, Pct(completed, due), rows);
    }

    /// <inheritdoc />
    public async Task<SupplierReviewDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureView();
        return await ProjectAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu đánh giá NCC.");
    }

    /// <inheritdoc />
    public async Task<SupplierReviewDto> CreateAsync(SaveSupplierReviewRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.MasterData.SupplierReviewManage);
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
            throw new BusinessRuleException("Không tìm thấy nhà cung cấp.");
        // Mỗi công ty × NCC × quý một phiếu chưa hủy – chỉ mục duy nhất có lọc chặn cả khi lập đồng thời.
        if (await _db.SupplierReviews.AnyAsync(r => r.SupplierId == request.SupplierId && r.Year == request.Year && r.Quarter == request.Quarter
                                                    && r.Status != SupplierReviewStatus.Cancelled, cancellationToken))
            throw new BusinessRuleException($"Nhà cung cấp đã có phiếu đánh giá quý {request.Quarter}/{request.Year}.");

        // Cấp số và lưu trong một transaction: lưu lỗi thì số không nhảy.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.SupplierReview, request.Date, cancellationToken);
        var review = SupplierReview.Create(companyId, no, request.SupplierId, request.Year, request.Quarter, Data(request), _clock.TodayInVietnam());
        review.SetMetrics(await SupplierMetricsAsync(review, cancellationToken));
        _db.SupplierReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(review.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierReviewDto> UpdateAsync(Guid id, SaveSupplierReviewRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierReviewManage);
        var review = await FindAsync(id, cancellationToken);
        review.Update(Data(request), _clock.TodayInVietnam());
        review.SetMetrics(await SupplierMetricsAsync(review, cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierReviewDto> CompleteAsync(Guid id, SaveSupplierReviewRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierReviewManage);
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var review = await FindAsync(id, cancellationToken);
        review.Update(Data(request), _clock.TodayInVietnam());
        // Chốt số liệu hệ thống tại lúc hoàn thành; sau đó phiếu giữ nguyên dù chứng từ trong quý thay đổi.
        review.SetMetrics(await SupplierMetricsAsync(review, cancellationToken));
        review.Complete(_clock.UtcNow, me);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierReviewDto> CancelAsync(Guid id, CancelSupplierReviewRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierReviewManage);
        var review = await FindAsync(id, cancellationToken);
        review.Cancel(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierReviewCompletion> CompletionAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        int due = 0, completed = 0;
        var quarters = new List<string>();
        // Chỉ quý đã kết thúc trong kỳ: quý chưa hết thì chưa đến hạn đánh giá, không tính "chưa hoàn thành" giả.
        var (year, quarter) = (from.Year, (from.Month - 1) / 3 + 1);
        while (true)
        {
            var (start, end) = SupplierReview.QuarterRange(year, quarter);
            if (start > to) break;
            if (end <= to)
            {
                quarters.Add($"{quarter}/{year}");
                var dueIds = (await MetricsAsync(start, end, cancellationToken)).Where(m => m.Value.IsDue).Select(m => m.Key).ToHashSet();
                var done = await _db.SupplierReviews.AsNoTracking()
                    .Where(r => r.Year == year && r.Quarter == quarter && r.Status == SupplierReviewStatus.Completed)
                    .Select(r => r.SupplierId).ToListAsync(cancellationToken);
                due += dueIds.Count;
                completed += done.Count(dueIds.Contains);
            }

            (year, quarter) = quarter == 4 ? (year + 1, 1) : (year, quarter + 1);
        }

        return new SupplierReviewCompletion(due, completed, quarters);
    }

    /// <summary>Số liệu hệ thống của NCC trong quý của phiếu.</summary>
    private async Task<SupplierReviewMetrics> SupplierMetricsAsync(SupplierReview review, CancellationToken cancellationToken)
    {
        var (from, to) = SupplierReview.QuarterRange(review.Year, review.Quarter);
        var metrics = await MetricsAsync(from, to, cancellationToken);
        return metrics.TryGetValue(review.SupplierId, out var m) ? m.Metrics : new SupplierReviewMetrics(0, null, null, null, null);
    }

    /// <summary>Một NCC trong quý: mã, tên, có thuộc diện phải đánh giá, số liệu tham khảo.</summary>
    private sealed record SupplierQuarter(string Code, string Name, bool IsDue, SupplierReviewMetrics Metrics);

    /// <summary>
    /// Số liệu theo NCC trong khoảng ngày, dùng đúng báo cáo SUPEVAL (giá trị nhập, đúng hạn, trả hàng, xếp loại) và QCDEV (tỷ lệ dòng đạt ÷
    /// dòng đã kiểm tra, ví dụ 19/20 → 95,00%). Thuộc diện phải đánh giá = có ít nhất một phiếu nhập mua đã ghi sổ.
    /// </summary>
    private async Task<Dictionary<Guid, SupplierQuarter>> MetricsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var evaluation = await _reports.GetSupplierEvaluationAsync(new PurchaseReportPeriod(from, to, false), cancellationToken);
        // Cùng cách đếm của QCDEV (dòng phiếu nhập mua đã ghi sổ, đi qua filter công ty, kho, nhóm hàng), nhóm theo NCC.
        var quality = await (
                from l in _db.InventoryDocumentLines.AsNoTracking()
                join d in _db.InventoryDocuments on l.DocumentId equals d.Id
                join i in _db.Items on l.ItemId equals i.Id
                where d.DocType == DocumentTypes.GoodsReceipt && d.Status == DocumentStatus.Posted && d.SupplierId != null
                      && d.Date >= @from && d.Date <= to && l.QcResult != QcResult.NotChecked
                group l by d.SupplierId!.Value into g
                select new { SupplierId = g.Key, Checked = g.Count(), Passed = g.Count(l => l.QcResult == QcResult.Passed) })
            .ToDictionaryAsync(q => q.SupplierId, q => Pct(q.Passed, q.Checked), cancellationToken);
        return evaluation.ToDictionary(e => e.SupplierId, e => new SupplierQuarter(
            e.SupplierCode, e.SupplierName, e.ReceiptCount > 0,
            new SupplierReviewMetrics(e.ReceiptAmount, e.OnTimePct, e.ReturnPct, quality.GetValueOrDefault(e.SupplierId), e.Rating)));
    }

    /// <summary>Tỷ lệ % 2 số lẻ, null khi mẫu số 0. Ví dụ 3 ÷ 4 → 75,00.</summary>
    private static decimal? Pct(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    /// <summary>Xem: quyền xem hoặc quyền lập phiếu đánh giá.</summary>
    private void EnsureView()
    {
        _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.MasterData.SupplierReviewView) && !_scope.HasPermission(Permissions.MasterData.SupplierReviewManage))
            throw new ForbiddenAccessException("Bạn không có quyền xem đánh giá NCC định kỳ.");
    }

    private async Task<SupplierReview> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.SupplierReviews.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy phiếu đánh giá NCC.");

    private static SupplierReviewData Data(SaveSupplierReviewRequest r) =>
        new(r.Date, new SupplierReviewScores(r.PriceScore, r.QualityScore, r.DeliveryScore, r.WarrantyScore, r.PaymentScore, r.ComplianceScore),
            string.IsNullOrWhiteSpace(r.Result) ? null : Enum.Parse<SupplierReviewResult>(r.Result), r.Comment);

    private async Task<SupplierReviewDto?> ProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from r in _db.SupplierReviews.AsNoTracking()
                where r.Id == id
                join s in _db.Suppliers on r.SupplierId equals s.Id
                select new
                {
                    Review = r, s.Code, s.Name,
                    CompletedByName = _db.Users.Where(u => u.Id == r.CompletedBy).Select(u => u.FullName).FirstOrDefault(),
                    CreatedByName = _db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var v = row.Review;
        return new SupplierReviewDto(
            v.Id, v.No, v.SupplierId, row.Code, row.Name, v.Year, v.Quarter, v.Date, v.PriceScore, v.QualityScore, v.DeliveryScore, v.WarrantyScore,
            v.PaymentScore, v.ComplianceScore, v.Score, v.Result?.ToString(), v.Comment, v.ReceiptAmount, v.OnTimePct, v.ReturnPct, v.QualityPct,
            v.SystemRating, v.Status.ToString(), v.CompletedAt, row.CompletedByName, v.CancelReason, v.CreatedAt, row.CreatedByName);
    }
}
