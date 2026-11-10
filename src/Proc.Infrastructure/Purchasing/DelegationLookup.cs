using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Đọc ủy quyền duyệt đang có tác dụng hôm nay (lát O2, design.md mục 19.3): chưa thu hồi, hôm nay (ngày Việt Nam) trong khoảng hiệu lực, người
/// ủy quyền và người được ủy quyền đều còn hoạt động. Bỏ filter công ty vì bước duyệt có thể ở công ty khác công ty đang làm việc (duyệt NCC
/// hai chặng) – luôn giới hạn đúng công ty của bước.
/// </summary>
internal sealed class DelegationLookup
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;

    /// <summary>Tạo bộ đọc ủy quyền.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ (ngày Việt Nam xét hiệu lực).</param>
    public DelegationLookup(ProcDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Một ủy quyền đang có tác dụng.</summary>
    /// <param name="CompanyId">Công ty.</param>
    /// <param name="DelegatorUserId">Người ủy quyền.</param>
    /// <param name="DelegateUserId">Người được ủy quyền.</param>
    /// <param name="DocType">Loại phiếu; null = mọi loại.</param>
    public sealed record Active(Guid CompanyId, Guid DelegatorUserId, Guid DelegateUserId, string? DocType)
    {
        /// <summary>Áp cho bước duyệt ở công ty và loại phiếu này.</summary>
        /// <param name="companyId">Công ty của bước.</param>
        /// <param name="docType">Loại phiếu của bước.</param>
        public bool Covers(Guid companyId, string docType) => CompanyId == companyId && (DocType is null || DocType == docType);
    }

    /// <summary>Ủy quyền đang có tác dụng tại các công ty, lọc thêm theo người ủy quyền hoặc người được ủy quyền.</summary>
    /// <param name="companyIds">Công ty của các bước.</param>
    /// <param name="delegatorIds">Chỉ ủy quyền của những người này (null = không lọc).</param>
    /// <param name="delegateUserId">Chỉ ủy quyền cho người này (null = không lọc).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách ủy quyền.</returns>
    public async Task<List<Active>> ActiveAsync(
        IReadOnlyCollection<Guid> companyIds, IReadOnlyCollection<Guid>? delegatorIds, Guid? delegateUserId, CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0 || delegatorIds is { Count: 0 }) return [];
        var today = _clock.TodayInVietnam();
        var query = _db.ApprovalDelegations.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(d => companyIds.Contains(d.CompanyId) && d.RevokedAt == null && d.ValidFrom <= today && d.ValidTo >= today)
            .Where(d => _db.Users.Any(u => u.Id == d.DelegatorUserId && u.IsActive) && _db.Users.Any(u => u.Id == d.DelegateUserId && u.IsActive));
        if (delegatorIds is not null) query = query.Where(d => delegatorIds.Contains(d.DelegatorUserId));
        if (delegateUserId is { } id) query = query.Where(d => d.DelegateUserId == id);
        return await query
            .Select(d => new Active(d.CompanyId, d.DelegatorUserId, d.DelegateUserId, d.DocType))
            .ToListAsync(cancellationToken);
    }
}
