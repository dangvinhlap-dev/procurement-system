using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Pricing;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Phiếu duyệt NCC và giá tháng (design.md mục 18.25 – lát G2). Phiếu thuộc tổng công ty (lập, sửa, duyệt khi đang làm việc ở tổng công ty, qua
/// <see cref="ApprovalEngine"/> theo quy trình loại GTH – người lập không tự duyệt, người có xung đột lợi ích với NCC trên phiếu không duyệt);
/// công ty thành viên chỉ đọc phiếu đã duyệt/đã thay áp cho mình – các truy vấn đó bỏ filter công ty có điều kiện, ghi rõ tại chỗ. Phiếu có
/// ngoại lệ qua thêm chặng Chủ tịch/TGĐ (loại GTX – lát H2).
/// </summary>
public sealed class MonthlySupplierApprovalService : IMonthlySupplierApprovalService
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
    public MonthlySupplierApprovalService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonthlyApprovalListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from x in Visible().AsNoTracking()
                join c in _db.ItemCategories on x.ItemCategoryId equals c.Id
                orderby x.Period descending, x.No descending
                select new
                {
                    x.Id, x.No, x.Period, CategoryCode = c.Code, CategoryName = c.Name, Lines = x.Lines.Count, x.Status, x.ReplacesId, x.CreatedBy,
                    x.HasException,
                })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var companies = await CompanyCodesAsync(ids, cancellationToken);
        var replacedIds = rows.Select(r => r.ReplacesId).OfType<Guid>().ToList();
        var replacedNos = await Visible().AsNoTracking().Where(x => replacedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.No, cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedBy), cancellationToken);
        return rows.Select(r => new MonthlyApprovalListItemDto(
                r.Id, r.No, r.Period, r.CategoryCode, r.CategoryName, string.Join(", ", companies[r.Id]), r.Lines, r.Status.ToString(),
                r.ReplacesId is { } rid ? replacedNos.GetValueOrDefault(rid) : null, r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
                r.HasException))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await Visible().AsNoTracking().Include(x => x.Lines).Include(x => x.Companies).AsSplitQuery()
                           .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                       ?? throw new NotFoundException("Không tìm thấy phiếu duyệt NCC và giá tháng.");
        return await ToDtoAsync(approval, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonthlyApprovalCompanyDto>> CompaniesAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        return await _db.Companies.AsNoTracking().Where(c => c.IsActive)
            .OrderByDescending(c => c.IsHeadOffice).ThenBy(c => c.Code)
            .Select(c => new MonthlyApprovalCompanyDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> CreateAsync(SaveMonthlyApprovalRequest request, CancellationToken cancellationToken)
    {
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        var approval = await NewAsync(header, lines, null, cancellationToken);
        return await GetAsync(approval.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> UpdateAsync(Guid id, SaveMonthlyApprovalRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(approval, cancellationToken);
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckAsync(header, lines.Select(l => l.Data).ToList(), approval.Id, approval.ReplacesId, cancellationToken);
        approval.Update(header);
        approval.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        await EnsureItemsVisibleAsync(approval, cancellationToken);
        // Kiểm tra lại lúc gửi: NCC có thể đã bị tạm ngưng/đưa vào danh sách đen, hoặc đã có phiếu khác trùng mặt hàng từ lúc soạn nháp.
        await CheckAsync(approval.HeaderFor(approval.Period), approval.CopyLines(), approval.Id, approval.ReplacesId, cancellationToken);
        // Ngoại lệ chốt lúc gửi (Q6 – lát H2): có ngoại lệ thì sau các cấp Hội đồng (GTH) là chặng Chủ tịch/TGĐ (GTX, luôn ít nhất một cấp).
        var exceptions = (await ExceptionsAsync(approval, cancellationToken)).SelectMany(e => e.Value).ToList();
        if (approval.PrepareSubmit(exceptions))
            await _approvals.SubmitWithStagesAsync(
                approval, [(ApprovalTypes.MonthlyApprovalException, ApprovalTypes.MonthlyApprovalExceptionStage)], cancellationToken);
        else
            await _approvals.SubmitAsync(approval, cancellationToken);
        await CompleteReplacementAsync(approval, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        // Duyệt cấp cuối là kết quả tháng có tác dụng: không để NCC vừa bị tạm ngưng/danh sách đen thành NCC được đặt hàng (mục 5.8).
        await CheckSuppliersAsync(approval.CopyLines(), cancellationToken);
        await _approvals.ApproveAsync(approval, request.Comment, cancellationToken);
        await CompleteReplacementAsync(approval, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        await _approvals.RejectAsync(approval, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(approval, cancellationToken);
        approval.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> CancelAsync(Guid id, CancelMonthlyApprovalRequest request, CancellationToken cancellationToken)
    {
        var approval = await FindOwnAsync(id, cancellationToken);
        // Phiếu đã duyệt đang có phiếu điều chỉnh soạn dở: hủy phiếu điều chỉnh trước, tránh hai quyết định ngược nhau cho cùng tháng.
        var pending = await _db.MonthlySupplierApprovals.AsNoTracking()
            .Where(x => x.ReplacesId == id && (x.Status == MonthlyApprovalStatus.Draft || x.Status == MonthlyApprovalStatus.Submitted))
            .Select(x => x.No).FirstOrDefaultAsync(cancellationToken);
        if (pending is not null) throw new BusinessRuleException($"Phiếu đang có phiếu điều chỉnh {pending} – hủy hoặc duyệt phiếu đó trước.");
        approval.Cancel(_clock.TodayInVietnam(), request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> AdjustAsync(Guid id, CancellationToken cancellationToken)
    {
        var old = await FindOwnAsync(id, cancellationToken);
        if (old.Status != MonthlyApprovalStatus.Approved) throw new BusinessRuleException("Chỉ điều chỉnh được phiếu đã duyệt.");
        if (await _db.MonthlySupplierApprovals.AnyAsync(x => x.ReplacesId == id && x.Status != MonthlyApprovalStatus.Cancelled, cancellationToken))
            throw new BusinessRuleException($"Phiếu {old.No} đã có phiếu điều chỉnh.");
        var lines = await ResolveLinesAsync(old.CopyLines().Select(ToRequest).ToList(), cancellationToken);
        var approval = await NewAsync(old.HeaderFor(old.Period), lines, old.Id, cancellationToken);
        return await GetAsync(approval.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyApprovalDto> CopyNextMonthAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await FindOwnAsync(id, cancellationToken);
        var lines = await ResolveLinesAsync(source.CopyLines().Select(ToRequest).ToList(), cancellationToken);
        var approval = await NewAsync(source.HeaderFor(source.Period.AddMonths(1)), lines, null, cancellationToken);
        return await GetAsync(approval.Id, cancellationToken);
    }

    /// <summary>
    /// Phiếu đọc được ở công ty đang làm việc: của công ty mình (tổng công ty thấy mọi phiếu nó lập) cộng phiếu Đã duyệt/Đã thay áp cho công
    /// ty mình. Bỏ filter công ty ở nhánh thứ hai vì phiếu thuộc tổng công ty nhưng là kết quả mua của công ty áp dụng (design.md 18.25);
    /// nháp, chờ duyệt, đã hủy của tổng công ty thì công ty thành viên không thấy (chưa duyệt thì chưa công bố). Filter nhóm hàng giữ nguyên.
    /// </summary>
    private IQueryable<MonthlySupplierApproval> Visible()
    {
        var companyId = _scope.RequireCompany();
        return _db.MonthlySupplierApprovals.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(x => x.CompanyId == companyId
                        || ((x.Status == MonthlyApprovalStatus.Approved || x.Status == MonthlyApprovalStatus.Replaced)
                            && x.Companies.Any(c => c.CompanyId == companyId)));
    }

    /// <summary>Cấp số và lưu phiếu nháp trong một transaction (số không bị nhảy khi lưu lỗi). Chỉ tổng công ty lập phiếu.</summary>
    private async Task<MonthlySupplierApproval> NewAsync(
        MonthlyApprovalHeader header, List<(MonthlyApprovalLineData Data, Item Item)> lines, Guid? replacesId, CancellationToken cancellationToken)
    {
        var companyId = await RequireHeadOfficeAsync(cancellationToken);
        await CheckAsync(header, lines.Select(l => l.Data).ToList(), null, replacesId, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.MonthlySupplierApproval, header.Period, cancellationToken);
        var approval = MonthlySupplierApproval.Create(companyId, no, header, replacesId);
        approval.SetLines(lines);
        _db.MonthlySupplierApprovals.Add(approval);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approval;
    }

    /// <summary>Phiếu điều chỉnh vừa duyệt đủ (kể cả khi không có cấp duyệt nào): phiếu cũ thành Đã thay.</summary>
    private async Task CompleteReplacementAsync(MonthlySupplierApproval approval, CancellationToken cancellationToken)
    {
        if (approval.Status != MonthlyApprovalStatus.Approved || approval.ReplacesId is not { } oldId) return;
        var old = await _db.MonthlySupplierApprovals.SingleAsync(x => x.Id == oldId, cancellationToken);
        if (old.Status == MonthlyApprovalStatus.Approved) old.MarkReplaced();
    }

    /// <summary>
    /// Quy tắc của phiếu (design.md 18.25): tháng từ tháng hiện tại trở đi; nhóm hàng trong phạm vi; công ty đang hoạt động; mặt hàng thuộc nhóm
    /// hàng (hoặc nhóm con); NCC chính, dự phòng ở trạng thái Đã duyệt; không trùng mặt hàng × tháng × công ty với phiếu khác còn hiệu lực
    /// (trừ phiếu được điều chỉnh).
    /// </summary>
    private async Task CheckAsync(
        MonthlyApprovalHeader header, IReadOnlyList<MonthlyApprovalLineData> lines, Guid? exceptId, Guid? replacesId, CancellationToken cancellationToken)
    {
        var today = _clock.TodayInVietnam();
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        // Không lập/gửi phiếu cho tháng đã qua: kết quả tháng phải có trước khi đặt hàng, không hợp thức hóa đơn đã đặt.
        if (header.Period < currentMonth)
            throw new BusinessRuleException($"Tháng {header.Period:MM/yyyy} đã qua – chỉ lập phiếu cho tháng hiện tại hoặc các tháng sau.");
        var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        if (!parents.ContainsKey(header.ItemCategoryId))
            throw new BusinessRuleException("Nhóm hàng không tồn tại hoặc ngoài phạm vi nhóm hàng của bạn.");
        var companyIds = header.CompanyIds.Distinct().ToList();
        var activeCompanies = await _db.Companies.AsNoTracking().CountAsync(c => companyIds.Contains(c.Id) && c.IsActive, cancellationToken);
        if (activeCompanies < companyIds.Count) throw new BusinessRuleException("Có công ty áp dụng không tồn tại hoặc đã ngừng dùng.");

        var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.CategoryId }).ToListAsync(cancellationToken);
        var outside = items.FirstOrDefault(i => !ItemAdministration.IsUnder(i.CategoryId, header.ItemCategoryId, parents));
        if (outside is not null) throw new BusinessRuleException($"Mặt hàng {outside.Code} không thuộc nhóm hàng của phiếu.");
        await CheckSuppliersAsync(lines, cancellationToken);

        // Bỏ filter nhóm hàng khi dò trùng: phiếu nhóm hàng khác (người lập không được xem) vẫn có thể chứa cùng mặt hàng ở nhóm con.
        var conflict = await _db.MonthlySupplierApprovals.AsNoTracking().IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter])
            .Where(x => x.Id != exceptId && x.Id != replacesId && x.Period == header.Period
                        && (x.Status == MonthlyApprovalStatus.Draft || x.Status == MonthlyApprovalStatus.Submitted || x.Status == MonthlyApprovalStatus.Approved)
                        && x.Companies.Any(c => companyIds.Contains(c.CompanyId)))
            .SelectMany(x => x.Lines.Where(l => itemIds.Contains(l.ItemId)).Select(l => new { x.No, l.ItemId }))
            .FirstOrDefaultAsync(cancellationToken);
        if (conflict is not null)
        {
            var code = items.FirstOrDefault(i => i.Id == conflict.ItemId)?.Code;
            throw new BusinessRuleException(
                $"Mặt hàng {code} đã có trong phiếu {conflict.No} cùng tháng {header.Period:MM/yyyy} cho cùng công ty – sửa phiếu đó hoặc lập phiếu điều chỉnh.");
        }
    }

    /// <summary>NCC chính và dự phòng phải Đã duyệt – Ban chỉ chọn trong danh sách NCC đủ điều kiện (design.md 18.25, G1).</summary>
    private async Task CheckSuppliersAsync(IReadOnlyList<MonthlyApprovalLineData> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.MainSupplierId).Concat(lines.Select(l => l.BackupSupplierId).OfType<Guid>()).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.Code, s.Status }).ToListAsync(cancellationToken);
        if (suppliers.Count < ids.Count) throw new BusinessRuleException("Có nhà cung cấp không tồn tại.");
        var bad = suppliers.FirstOrDefault(s => s.Status != SupplierStatus.Approved);
        if (bad is not null) throw new BusinessRuleException($"NCC {bad.Code} chưa ở trạng thái Đã duyệt – không chọn làm NCC chính/dự phòng được.");
    }

    /// <summary>
    /// Ngoại lệ tự phát hiện của từng dòng (lát H2, <see cref="MonthlyApprovalExceptions"/>), khóa theo mặt hàng (mỗi mặt hàng một dòng), mô tả
    /// có tiền tố mã hàng. NCC đủ điều kiện = NCC Đã duyệt có dòng NCC – sản phẩm của mặt hàng (như SUPCOV – lát G1) cộng NCC chính/dự phòng
    /// của dòng (đã kiểm tra Đã duyệt). Đọc qua filter phạm vi nhóm hàng và công ty đang làm việc (tổng công ty – phiếu độc quyền của tổng
    /// công ty) như mọi truy vấn khác.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> ExceptionsAsync(MonthlySupplierApproval approval, CancellationToken cancellationToken)
    {
        var today = _clock.TodayInVietnam();
        var itemIds = approval.Lines.Select(l => l.ItemId).Distinct().ToList();
        var links = (await _db.SupplierItems.AsNoTracking().Where(l => itemIds.Contains(l.ItemId))
                .Join(_db.Suppliers.AsNoTracking().Where(s => s.Status == SupplierStatus.Approved), l => l.SupplierId, s => s.Id,
                    (l, s) => new { l.ItemId, SupplierId = s.Id })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.ItemId, x => x.SupplierId);
        var soleSources = (await _db.SoleSourceApprovals.AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId) && x.Status == SoleSourceStatus.Approved && x.ValidFrom <= today && x.ReviewDate >= today)
                .Select(x => new { x.ItemId, x.No, x.ValidFrom })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ValidFrom).First().No);
        var codes = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
        return approval.Lines.ToDictionary(l => l.ItemId, l =>
        {
            var suppliers = links[l.ItemId].Append(l.MainSupplierId).ToList();
            if (l.BackupSupplierId is { } backup) suppliers.Add(backup);
            return (IReadOnlyList<string>)MonthlyApprovalExceptions
                .ForLine(suppliers.Distinct().Count(), soleSources.GetValueOrDefault(l.ItemId), l.UnitPrice, l.BackupUnitPrice, l.TargetPrice)
                .Select(e => $"{codes.GetValueOrDefault(l.ItemId)}: {e}").ToList();
        });
    }

    private async Task<Guid> RequireHeadOfficeAsync(CancellationToken cancellationToken)
    {
        if (!await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken))
            throw new ForbiddenAccessException("Phiếu duyệt NCC và giá tháng chỉ lập khi đang làm việc ở tổng công ty.");
        return _scope.RequireCompany();
    }

    /// <summary>Phiếu của công ty đang làm việc (tổng công ty) để thao tác; phiếu thấy được nhưng của công ty khác thì báo chỉ xem.</summary>
    private async Task<MonthlySupplierApproval> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var approval = await _db.MonthlySupplierApprovals.Include(x => x.Lines).Include(x => x.Companies).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (approval is not null) return approval;
        if (await Visible().AnyAsync(x => x.Id == id, cancellationToken))
            throw new ForbiddenAccessException("Phiếu do tổng công ty lập – công ty áp dụng chỉ xem.");
        throw new NotFoundException("Không tìm thấy phiếu duyệt NCC và giá tháng.");
    }

    private async Task<List<(MonthlyApprovalLineData Data, Item Item)>> ResolveLinesAsync(
        IReadOnlyList<SaveMonthlyApprovalLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) => (
                new MonthlyApprovalLineData(l.Id, l.ItemId, l.UomId, l.MainSupplierId, l.UnitPrice, l.BackupSupplierId, l.BackupUnitPrice,
                    l.QuantityLimit, l.Note, l.StandardPrice, l.CeilingPrice, l.TargetPrice),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    private async Task EnsureItemsVisibleAsync(MonthlySupplierApproval approval, CancellationToken cancellationToken)
    {
        var ids = approval.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
    }

    private async Task<ILookup<Guid, string>> CompanyCodesAsync(IReadOnlyList<Guid> approvalIds, CancellationToken cancellationToken) =>
        (await (
                from a in _db.MonthlySupplierApprovalCompanies.AsNoTracking()
                where approvalIds.Contains(a.ApprovalId)
                join c in _db.Companies on a.CompanyId equals c.Id
                orderby c.Code
                select new { a.ApprovalId, c.Code })
            .ToListAsync(cancellationToken))
        .ToLookup(x => x.ApprovalId, x => x.Code);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    private async Task<MonthlyApprovalDto> ToDtoAsync(MonthlySupplierApproval x, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var itemIds = x.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = x.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplierIds = x.RelatedSupplierIds.ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Code, s.Name })
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var category = await _db.ItemCategories.AsNoTracking().Where(c => c.Id == x.ItemCategoryId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(cancellationToken);
        var appliedIds = x.Companies.Select(c => c.CompanyId).ToList();
        var companies = await _db.Companies.AsNoTracking().Where(c => appliedIds.Contains(c.Id) || c.Id == x.CompanyId)
            .Select(c => new { c.Id, c.Code, c.Name, c.IsHeadOffice }).ToListAsync(cancellationToken);
        var replacesNo = x.ReplacesId is { } rid
            ? await _db.MonthlySupplierApprovals.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter]).Where(o => o.Id == rid).Select(o => o.No).SingleOrDefaultAsync(cancellationToken)
            : null;
        var replacedBy = await Visible().AsNoTracking().Where(o => o.ReplacesId == x.Id && o.Status != MonthlyApprovalStatus.Cancelled)
            .Select(o => new { o.Id, o.No }).FirstOrDefaultAsync(cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy], cancellationToken);

        var own = x.CompanyId == companyId;
        var canManage = own && _scope.HasPermission(Permissions.Pricing.MonthlyApprovalManage);
        string? readOnly = !own ? "Phiếu do tổng công ty lập, Ban cung ứng đã duyệt – công ty áp dụng chỉ xem."
            : items.Count < itemIds.Count ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !canManage ? "Bạn không có quyền lập, sửa phiếu duyệt NCC và giá tháng." : null;
        var manageable = readOnly is null;
        var canApprove = own && x.IsAwaitingApproval
            && await _approvals.ApproveBlockerAsync(x, await _approvals.CurrentStepAsync(x, cancellationToken), cancellationToken) is null;
        var actions = new MonthlyApprovalActions(
            manageable && x.Status == MonthlyApprovalStatus.Draft,
            canApprove,
            manageable && x.Status == MonthlyApprovalStatus.Submitted,
            manageable && x.Status == MonthlyApprovalStatus.Approved && replacedBy is null && x.Period >= currentMonth,
            manageable && (x.Status == MonthlyApprovalStatus.Draft || (x.Status == MonthlyApprovalStatus.Approved && replacedBy is null)),
            manageable && x.Status is MonthlyApprovalStatus.Approved or MonthlyApprovalStatus.Replaced && x.Period.AddMonths(1) >= currentMonth,
            readOnly);

        // Ngoại lệ tính lúc đọc khi phiếu còn trên đường duyệt ở tổng công ty (H2) – phiếu đã có quyết định chỉ giữ cờ chốt lúc gửi.
        var exceptions = own && x.Status is MonthlyApprovalStatus.Draft or MonthlyApprovalStatus.Submitted && items.Count == itemIds.Count
            ? await ExceptionsAsync(x, cancellationToken)
            : [];
        var lines = x.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var main = suppliers.GetValueOrDefault(l.MainSupplierId);
            var backup = l.BackupSupplierId is { } b ? suppliers.GetValueOrDefault(b) : null;
            return new MonthlyApprovalLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.MainSupplierId, main?.Code ?? "", main?.Name ?? "", l.UnitPrice, l.BackupSupplierId, backup?.Code, backup?.Name, l.BackupUnitPrice,
                l.QuantityLimit, l.Note, l.UomFactor, l.StandardPrice, l.CeilingPrice, l.TargetPrice,
                exceptions.GetValueOrDefault(l.ItemId)?.Select(e => e[(e.IndexOf(": ", StringComparison.Ordinal) + 2)..]).ToList() ?? []);
        }).ToList();
        var approvals = await _approvals.HistoryAsync(x.DocType, x.Id, cancellationToken, x);
        return new MonthlyApprovalDto(
            x.Id, x.No, companies.FirstOrDefault(c => c.Id == x.CompanyId)?.Code ?? "", x.Period, x.ItemCategoryId, category?.Code ?? "",
            category?.Name ?? "",
            companies.Where(c => appliedIds.Contains(c.Id)).OrderByDescending(c => c.IsHeadOffice).ThenBy(c => c.Code)
                .Select(c => new MonthlyApprovalCompanyDto(c.Id, c.Code, c.Name)).ToList(),
            x.Note, x.Status.ToString(), x.RejectReason, x.ApprovedAt, x.CancelledOn, x.CancelReason, x.ReplacesId, replacesNo, replacedBy?.Id,
            replacedBy?.No, x.CreatedAt, x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null, lines, approvals, actions, x.ExceptionNote,
            x.HasException);
    }

    /// <summary>Tháng lưu là ngày đầu tháng của ngày gửi lên.</summary>
    private static MonthlyApprovalHeader ToHeader(SaveMonthlyApprovalRequest r) =>
        new(new DateOnly(r.Period.Year, r.Period.Month, 1), r.ItemCategoryId, r.CompanyIds, r.Note, r.ExceptionNote);

    private static SaveMonthlyApprovalLineRequest ToRequest(MonthlyApprovalLineData d) =>
        new(null, d.ItemId, d.UomId, d.MainSupplierId, d.UnitPrice, d.BackupSupplierId, d.BackupUnitPrice, d.QuantityLimit, d.Note,
            d.StandardPrice, d.CeilingPrice, d.TargetPrice);
}
