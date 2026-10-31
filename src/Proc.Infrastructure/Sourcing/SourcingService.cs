using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.Organization;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Sourcing;

/// <summary>Danh sách tìm nguồn (design.md mục 8.3). Phiếu có CompanyId nên tự lọc theo công ty đang làm việc.</summary>
public sealed class SourcingService : ISourcingService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    public SourcingService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourcingListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        // Mặt hàng ngoài phạm vi nhóm hàng: phiếu vẫn hiện nhưng chỉ mô tả (join trái qua filter nhóm hàng).
        return await (
                from s in _db.SourcingRequests.AsNoTracking()
                join i in _db.Items on s.ItemId equals i.Id into items
                from i in items.DefaultIfEmpty()
                orderby s.Date descending, s.No descending
                select new SourcingListItemDto(
                    s.Id, s.No, s.Date, i != null ? i.Code + " – " + i.Name : s.Description ?? "", s.Qty, s.Deadline, s.Responsible, s.Candidates.Count,
                    s.Candidates.Count(c => c.Status == CandidateStatus.Passed), s.Status.ToString()))
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourcingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var request = await _db.SourcingRequests.AsNoTracking().Include(s => s.Candidates).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu tìm nguồn.");
        return await ToDtoAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourcingDto> CreateAsync(SaveSourcingRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        await CheckAsync(request, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.Sourcing, request.Date, cancellationToken);
        var sourcing = SourcingRequest.Create(companyId, no, ToHeader(request));
        sourcing.SetCandidates(ToCandidates(request));
        _db.SourcingRequests.Add(sourcing);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(sourcing.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourcingDto> UpdateAsync(Guid id, SaveSourcingRequest request, CancellationToken cancellationToken)
    {
        var sourcing = await LoadAsync(id, cancellationToken);
        await CheckAsync(request, cancellationToken);
        sourcing.Update(ToHeader(request));
        sourcing.SetCandidates(ToCandidates(request));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourcingDto> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        var sourcing = await LoadAsync(id, cancellationToken);
        sourcing.Close();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SourcingDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var sourcing = await LoadAsync(id, cancellationToken);
        sourcing.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<SourcingRequest> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.SourcingRequests.Include(s => s.Candidates).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiếu tìm nguồn.");
    }

    /// <summary>Mặt hàng trong phạm vi nhóm hàng; NCC có trong danh mục (kể cả Tiềm năng, vì đây là bước tìm NCC mới).</summary>
    private async Task CheckAsync(SaveSourcingRequest request, CancellationToken cancellationToken)
    {
        if (request.ItemId is { } itemId && !await _db.Items.AnyAsync(i => i.Id == itemId, cancellationToken))
            throw new NotFoundException("Không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
        var ids = request.Candidates.Select(c => c.SupplierId).Distinct().ToList();
        if (await _db.Suppliers.CountAsync(s => ids.Contains(s.Id), cancellationToken) < ids.Count)
            throw new NotFoundException("Không tìm thấy nhà cung cấp.");
    }

    private async Task<SourcingDto> ToDtoAsync(SourcingRequest request, CancellationToken cancellationToken)
    {
        var item = request.ItemId is { } itemId
            ? await (
                    from i in _db.Items.AsNoTracking()
                    join u in _db.Uoms on i.BaseUomId equals u.Id
                    where i.Id == itemId
                    select new { i.Code, i.Name, Uom = u.Code })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var supplierIds = request.Candidates.Select(c => c.SupplierId).ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name, s.Status }, cancellationToken);
        var createdBy = request.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var candidates = request.Candidates
            .Select(c =>
            {
                var s = suppliers.GetValueOrDefault(c.SupplierId);
                return new SourcingCandidateDto(c.Id, c.SupplierId, s?.Code ?? "", s?.Name ?? "", s?.Status.ToString() ?? "", c.Status.ToString(), c.Note);
            })
            .OrderBy(c => c.SupplierCode)
            .ToList();
        return new SourcingDto(
            request.Id, request.No, request.Date, request.ItemId, item?.Code, item?.Name, item?.Uom, request.Description, request.Specification, request.Qty,
            request.Deadline, request.Responsible, request.Note, request.Status.ToString(), request.CreatedAt, createdBy, candidates,
            request.Status == SourcingStatus.Open && _scope.HasPermission(Permissions.Sourcing.RfqManage));
    }

    private static SourcingHeader ToHeader(SaveSourcingRequest r) =>
        new(r.Date, r.ItemId, r.Description, r.Specification, r.Qty, r.Deadline, r.Responsible, r.Note);

    private static List<SourcingCandidateData> ToCandidates(SaveSourcingRequest r) =>
        r.Candidates.Select(c => new SourcingCandidateData(
                c.Id, c.SupplierId,
                Enum.TryParse<CandidateStatus>(c.Status, true, out var status) && Enum.IsDefined(status)
                    ? status
                    : throw new BusinessRuleException("Trạng thái NCC ứng viên phải là Contacted, RfqSent, Quoted, Passed hoặc Failed."),
                c.Note))
            .ToList();
}
