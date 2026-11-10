using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Phê duyệt NCC độc quyền (design.md mục 18.9). Phiếu thuộc công ty lập nên tự lọc theo công ty đang làm việc, và theo phạm vi nhóm hàng
/// của mặt hàng; duyệt tại công ty lập qua <see cref="ApprovalEngine"/> theo quy trình loại NDQ (người lập không tự duyệt, người có kê khai
/// xung đột lợi ích với NCC không duyệt – mục 18.8).
/// </summary>
public sealed class SoleSourceService : ISoleSourceService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public SoleSourceService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SoleSourceListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var rows = await (
                from x in _db.SoleSourceApprovals.AsNoTracking()
                join s in _db.Suppliers on x.SupplierId equals s.Id
                join i in _db.Items on x.ItemId equals i.Id
                orderby x.CreatedAt descending
                select new { x, SupplierCode = s.Code, SupplierName = s.Name, ItemCode = i.Code, ItemName = i.Name })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.x.CreatedBy), cancellationToken);
        return rows.Select(r => new SoleSourceListItemDto(
                r.x.Id, r.x.No, r.SupplierCode, r.SupplierName, r.ItemCode, r.ItemName, r.x.ValidFrom, r.x.ReviewDate, r.x.Status.ToString(),
                r.x.IsEffectiveOn(today), r.x.IsOverdueOn(today),
                r.x.Status == SoleSourceStatus.Approved ? r.x.ReviewDate.DayNumber - today.DayNumber : null,
                r.x.CreatedBy is { } c ? users.GetValueOrDefault(c) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var approval = await _db.SoleSourceApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu NCC độc quyền.");
        return await ToDtoAsync(approval, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> CreateAsync(SaveSoleSourceRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var data = ToData(request);
        await CheckAsync(data, null, cancellationToken);
        var approval = await NewAsync(companyId, data, null, cancellationToken);
        return await GetAsync(approval.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> UpdateAsync(Guid id, SaveSoleSourceRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        var data = ToData(request);
        await CheckAsync(data, id, cancellationToken);
        approval.Update(data);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể đã vào danh sách đen, hoặc đã có phiếu khác được duyệt trùng thời gian từ lúc soạn nháp.
        await CheckAsync(ToData(approval), id, cancellationToken);
        await _approvals.SubmitAsync(approval, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        // Không để phiếu độc quyền của NCC vừa vào danh sách đen có tác dụng (design.md mục 5.8).
        await CheckSupplierAsync(approval.SupplierId, cancellationToken);
        await _approvals.ApproveAsync(approval, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        await _approvals.RejectAsync(approval, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(approval, cancellationToken);
        approval.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> EndAsync(Guid id, EndSoleSourceRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindAsync(id, cancellationToken);
        approval.End(_clock.TodayInVietnam(), request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SoleSourceDto> RenewAsync(Guid id, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var old = await FindAsync(id, cancellationToken);
        if (old.Status != SoleSourceStatus.Approved) throw new BusinessRuleException($"Chỉ lập phiếu đánh giá lại từ phiếu đã duyệt.");
        if (await _db.SoleSourceApprovals.AnyAsync(x => x.RenewedFromId == old.Id, cancellationToken))
            throw new BusinessRuleException($"Phiếu {old.No} đã có phiếu đánh giá lại.");
        var today = _clock.TodayInVietnam();
        // Nối tiếp phiếu cũ để không có ngày trống; phiếu cũ đã quá hạn thì bắt đầu từ hôm nay.
        var from = old.ReviewDate >= today ? old.ReviewDate.AddDays(1) : today;
        var data = new SoleSourceData(old.SupplierId, old.ItemId, old.Reason, from, from.AddMonths(SoleSourceApproval.MaxReviewMonths));
        await CheckAsync(data, null, cancellationToken);
        var approval = await NewAsync(companyId, data, old.Id, cancellationToken);
        return await GetAsync(approval.Id, cancellationToken);
    }

    /// <summary>Cấp số và lưu phiếu nháp trong một transaction (số không bị nhảy khi lưu lỗi).</summary>
    private async Task<SoleSourceApproval> NewAsync(Guid companyId, SoleSourceData data, Guid? renewedFromId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.SoleSource, data.ValidFrom, cancellationToken);
        var approval = SoleSourceApproval.Create(companyId, no, data, renewedFromId);
        _db.SoleSourceApprovals.Add(approval);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approval;
    }

    /// <summary>
    /// Kiểm tra NCC, mặt hàng (trong phạm vi), và không trùng thời gian với phiếu chưa kết thúc khác cùng công ty × NCC × mặt hàng
    /// (design.md mục 18.9: một giải trình cho một khoảng thời gian).
    /// </summary>
    private async Task CheckAsync(SoleSourceData data, Guid? exceptId, CancellationToken cancellationToken)
    {
        await CheckSupplierAsync(data.SupplierId, cancellationToken);
        var item = await _db.Items.AsNoTracking().Where(i => i.Id == data.ItemId).Select(i => new { i.Code, i.IsActive }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Mặt hàng không tồn tại hoặc ngoài phạm vi nhóm hàng của bạn.");
        if (!item.IsActive) throw new BusinessRuleException($"Mặt hàng {item.Code} đã ngừng dùng.");
        var overlap = await _db.SoleSourceApprovals.AsNoTracking()
            .Where(x => x.Id != exceptId && x.SupplierId == data.SupplierId && x.ItemId == data.ItemId && x.Status != SoleSourceStatus.Ended
                && x.ValidFrom <= data.ReviewDate && x.ReviewDate >= data.ValidFrom)
            .Select(x => x.No)
            .FirstOrDefaultAsync(cancellationToken);
        if (overlap is not null)
            throw new BusinessRuleException($"Đã có phiếu {overlap} cho NCC và mặt hàng này trùng thời gian – sửa phiếu đó hoặc chọn thời gian khác.");
    }

    /// <summary>Kiểm tra NCC tồn tại và không nằm trong danh sách đen.</summary>
    private async Task CheckSupplierAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => new { s.Code, s.Status }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Nhà cung cấp không tồn tại.");
        if (supplier.Status == SupplierStatus.Blacklisted) throw new BusinessRuleException($"NCC {supplier.Code} đang ở danh sách đen.");
    }

    /// <summary>Tìm phiếu NCC độc quyền theo Id trong công ty đang làm việc; không thấy thì báo lỗi 404.</summary>
    private async Task<SoleSourceApproval> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.SoleSourceApprovals.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu NCC độc quyền.");
    }

    /// <summary>Lấy họ tên theo danh sách Id người dùng (bỏ null, bỏ trùng).</summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    /// <summary>Đổi phiếu NCC độc quyền sang DTO kèm tên NCC, mặt hàng, người duyệt và cờ còn hiệu lực.</summary>
    private async Task<SoleSourceDto> ToDtoAsync(SoleSourceApproval x, CancellationToken cancellationToken)
    {
        var today = _clock.TodayInVietnam();
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == x.SupplierId).Select(s => new { s.Code, s.Name }).SingleAsync(cancellationToken);
        var item = await _db.Items.AsNoTracking().Where(i => i.Id == x.ItemId).Select(i => new { i.Code, i.Name }).SingleAsync(cancellationToken);
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == x.CompanyId).Select(c => c.Code).SingleAsync(cancellationToken);
        var renewedFromNo = x.RenewedFromId is { } fromId
            ? await _db.SoleSourceApprovals.AsNoTracking().Where(o => o.Id == fromId).Select(o => o.No).SingleOrDefaultAsync(cancellationToken)
            : null;
        var renewedBy = await _db.SoleSourceApprovals.AsNoTracking().Where(o => o.RenewedFromId == x.Id)
            .Select(o => new { o.Id, o.No }).FirstOrDefaultAsync(cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy], cancellationToken);

        var canManage = _scope.HasPermission(Permissions.MasterData.SoleSourceManage);
        var canApprove = x.IsAwaitingApproval
            && await _approvals.ApproveBlockerAsync(x, await _approvals.CurrentStepAsync(x, cancellationToken), cancellationToken) is null;
        var actions = new SoleSourceActions(
            canManage && x.Status == SoleSourceStatus.Draft,
            canApprove,
            canManage && x.Status == SoleSourceStatus.Submitted,
            canManage && x.Status == SoleSourceStatus.Approved && !x.IsOverdueOn(today),
            canManage && x.Status == SoleSourceStatus.Approved && renewedBy is null);
        var approvals = await _approvals.HistoryAsync(x.DocType, x.Id, cancellationToken, x);
        return new SoleSourceDto(
            x.Id, x.No, company, x.SupplierId, supplier.Code, supplier.Name, x.ItemId, item.Code, item.Name, x.Reason, x.ValidFrom, x.ReviewDate,
            x.Status.ToString(), x.IsEffectiveOn(today), x.IsOverdueOn(today), x.RejectReason, x.ApprovedAt, x.EndedOn, x.EndReason,
            x.RenewedFromId, renewedFromNo, renewedBy?.Id, renewedBy?.No, x.CreatedAt, x.CreatedBy is { } c ? users.GetValueOrDefault(c) : null,
            approvals, actions);
    }

    private static SoleSourceData ToData(SaveSoleSourceRequest r) => new(r.SupplierId, r.ItemId, r.Reason, r.ValidFrom, r.ReviewDate);

    private static SoleSourceData ToData(SoleSourceApproval x) => new(x.SupplierId, x.ItemId, x.Reason, x.ValidFrom, x.ReviewDate);
}
