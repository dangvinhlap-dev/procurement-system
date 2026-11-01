using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Yêu cầu duyệt NCC (design.md mục 18.6): lập khi lưu NCC có thay đổi trạng thái (Đã duyệt/Danh sách đen) hoặc tài khoản ngân hàng,
/// duyệt hai chặng qua <see cref="ApprovalEngine"/>, duyệt xong mới áp vào NCC.
/// </summary>
public sealed class SupplierChangeRequestService : ISupplierChangeRequestService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    public SupplierChangeRequestService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <summary>
    /// Lập và gửi duyệt một yêu cầu (chưa lưu – người gọi lưu cùng thay đổi khác của NCC). Công ty liên quan = công ty đang mua của NCC
    /// (<c>SupplierCompany</c> đang giao dịch), chốt lúc gửi; chặng cuối là Tổng công ty.
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi NCC đang có yêu cầu cùng loại chờ duyệt.</exception>
    internal static async Task SubmitAsync(
        ProcDbContext db, ApprovalEngine approvals, Supplier supplier, Func<string, SupplierChangeRequest> create,
        CancellationToken cancellationToken)
    {
        var count = await db.SupplierChangeRequests.CountAsync(r => r.SupplierId == supplier.Id, cancellationToken);
        var request = create($"{supplier.Code}-{count + 1:00}");
        if (await db.SupplierChangeRequests.AnyAsync(
                r => r.SupplierId == supplier.Id && r.Kind == request.Kind && r.Status == SupplierChangeRequestStatus.Submitted, cancellationToken))
        {
            throw new BusinessRuleException(request.Kind == SupplierChangeKind.Status
                ? $"NCC {supplier.Code} đang có yêu cầu đổi trạng thái chờ duyệt – chờ duyệt xong hoặc rút yêu cầu đó trước."
                : $"NCC {supplier.Code} đang có yêu cầu đổi tài khoản ngân hàng chờ duyệt – chờ duyệt xong hoặc rút yêu cầu đó trước.");
        }

        // Công ty liên quan ở mọi công ty (không chỉ công ty đang làm việc): bỏ filter công ty, chỉ đọc đúng NCC này.
        var related = await db.SupplierCompanies.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(sc => sc.SupplierId == supplier.Id && sc.IsActive)
            .Select(sc => sc.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var headOffice = await db.Companies.Where(c => c.IsHeadOffice).Select(c => c.Id).SingleAsync(cancellationToken);
        db.SupplierChangeRequests.Add(request);
        await approvals.SubmitStagedAsync(request, related, headOffice, cancellationToken);
        if (request.Status == SupplierChangeRequestStatus.Approved) request.ApplyTo(supplier);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierChangeRequestDto>> ListAsync(Guid? supplierId, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var requests = await _db.SupplierChangeRequests.AsNoTracking()
            .Where(r => supplierId == null || r.SupplierId == supplierId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken);
        return await ToDtosAsync(requests, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierChangeRequestDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var request = await _db.SupplierChangeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu duyệt NCC.");
        return (await ToDtosAsync([request], cancellationToken))[0];
    }

    /// <inheritdoc />
    public async Task<SupplierChangeRequestDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var change = await FindAsync(id, cancellationToken);
        await _approvals.ApproveAsync(change, request.Comment, cancellationToken);
        if (change.Status == SupplierChangeRequestStatus.Approved)
        {
            // Tổng công ty duyệt lần cuối: áp thay đổi vào NCC trong cùng lần lưu.
            var supplier = await _db.Suppliers.SingleAsync(s => s.Id == change.SupplierId, cancellationToken);
            change.ApplyTo(supplier);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierChangeRequestDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var change = await FindAsync(id, cancellationToken);
        await _approvals.RejectAsync(change, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierChangeRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var change = await FindAsync(id, cancellationToken);
        if (change.CreatedBy != _scope.UserId) throw new ForbiddenAccessException("Chỉ người lập được rút yêu cầu.");
        change.Cancel();
        await _approvals.SkipPendingAsync(change, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<SupplierChangeRequest> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.SupplierChangeRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu duyệt NCC.");
    }

    /// <summary>Đổi sang DTO kèm lịch sử duyệt, công ty đang chờ và nút được bấm.</summary>
    internal async Task<IReadOnlyList<SupplierChangeRequestDto>> ToDtosAsync(IReadOnlyList<SupplierChangeRequest> requests, CancellationToken cancellationToken)
    {
        var supplierIds = requests.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken);
        var userIds = requests.Select(r => r.CreatedBy).OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var result = new List<SupplierChangeRequestDto>();
        foreach (var r in requests)
        {
            var approvals = await _approvals.HistoryAsync(r.DocType, r.Id, cancellationToken);
            var waiting = r.IsAwaitingApproval ? await WaitingForAsync(r, cancellationToken) : null;
            var canApprove = r.IsAwaitingApproval
                && await _approvals.ApproveBlockerAsync(r, await _approvals.CurrentStepAsync(r, cancellationToken), cancellationToken) is null;
            var (code, name) = suppliers.GetValueOrDefault(r.SupplierId, (string.Empty, string.Empty));
            result.Add(new SupplierChangeRequestDto(
                r.Id, r.No, r.SupplierId, code, name, r.Kind.ToString(), r.CurrentSummary, r.ProposedSummary, r.Status.ToString(),
                r.CreatedBy is { } c ? users.GetValueOrDefault(c) : null, r.CreatedAt, r.ApprovedAt, r.RejectReason, waiting, approvals,
                canApprove, r.IsAwaitingApproval && r.CreatedBy == _scope.UserId));
        }

        return result;
    }

    /// <summary>Mã các công ty có bước đang đến lượt, ví dụ "BHA, BHB" (chặng 1) hoặc "TCT" (chặng 2).</summary>
    private async Task<string> WaitingForAsync(SupplierChangeRequest request, CancellationToken cancellationToken)
    {
        var steps = await _db.ApprovalSteps.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => s.DocType == request.DocType && s.DocumentId == request.Id && s.Round == request.ApprovalRound)
            .ToListAsync(cancellationToken);
        var companyIds = ApprovalProgress.Active(steps).Select(s => s.CompanyId).ToList();
        var codes = await _db.Companies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).OrderBy(c => c.Code).Select(c => c.Code).ToListAsync(cancellationToken);
        return string.Join(", ", codes);
    }
}
