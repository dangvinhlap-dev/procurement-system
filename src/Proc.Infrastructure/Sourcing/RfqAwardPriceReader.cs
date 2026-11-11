using Microsoft.EntityFrameworkCore;
using Proc.Application.Sourcing;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Sourcing;

/// <summary>
/// Đọc giá trúng thầu cho nguồn giá PO (design.md mục 18.10). Đi qua bảng RFQ nên giữ filter công ty: chỉ RFQ của công ty đang làm việc.
/// </summary>
public sealed class RfqAwardPriceReader : IRfqAwardPriceReader
{
    private readonly ProcDbContext _db;

    /// <summary>Tạo bộ đọc.</summary>
    public RfqAwardPriceReader(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RfqAwardPriceDto>> GetAsync(Guid supplierId, IReadOnlyList<Guid> rfqLineIds, CancellationToken cancellationToken)
    {
        if (rfqLineIds.Count == 0) return [];
        // Chỉ RFQ Đã chọn thầu: kết quả đang chờ hội đồng duyệt (AwardPending) chưa là giá đã duyệt (design.md mục 8.6).
        return await (
                from l in _db.Set<RfqLine>().AsNoTracking()
                join r in _db.Rfqs on l.RfqId equals r.Id
                join s in _db.Set<RfqSupplier>() on new { l.RfqId, SupplierId = l.AwardedSupplierId!.Value } equals new { s.RfqId, s.SupplierId }
                join q in _db.Set<QuotationLine>() on new { RfqSupplierId = s.Id, RfqLineId = l.Id } equals new { q.RfqSupplierId, q.RfqLineId }
                where rfqLineIds.Contains(l.Id) && r.Status == RfqStatus.Awarded && l.AwardedSupplierId == supplierId
                select new RfqAwardPriceDto(l.Id, r.No, l.ItemId, l.UomId, q.UnitPrice, q.DiscountPct, r.Id))
            .ToListAsync(cancellationToken);
    }
}
