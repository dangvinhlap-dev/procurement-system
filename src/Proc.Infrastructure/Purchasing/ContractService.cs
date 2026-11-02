using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Hợp đồng nguyên tắc phương án 2 (lát B4a – design.md mục 18.7). Hợp đồng thuộc công ty ký (filter công ty tự áp); bảng giá gắn vào
/// là bảng của chính công ty hoặc bảng toàn tập đoàn đã hiệu lực (đọc bỏ filter công ty có điều kiện – giống
/// <see cref="Pricing.PriceListService"/>). Duyệt qua <see cref="ApprovalEngine"/> theo quy trình loại CTR của công ty, xét giá trị dự kiến.
/// </summary>
public sealed partial class ContractService : IContractService
{
    /// <summary>Số ngày trước ngày hết hiệu lực bắt đầu cảnh báo "sắp hết hiệu lực" (như bảng giá – lát B1).</summary>
    public const int ExpiryWarningDays = 30;

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
    public ContractService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContractListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var today = _clock.TodayInVietnam();
        var warnUntil = today.AddDays(ExpiryWarningDays);
        return await (
                from c in _db.PurchaseContracts.AsNoTracking()
                join s in _db.Suppliers on c.SupplierId equals s.Id
                join u in _db.Users on c.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby c.ValidFrom descending, c.No descending
                select new ContractListItemDto(
                    c.Id, c.No, c.ContractNo, c.Title, s.Code, s.Name, c.ValidFrom, c.ValidTo, c.Lines.Sum(l => l.ExpectedAmount), c.Status.ToString(),
                    // Cùng điều kiện với PurchaseContract.ExpiresWithin, viết lại để EF dịch được sang SQL.
                    c.Status == ContractStatus.Active && c.ValidTo >= today && c.ValidTo <= warnUntil,
                    u == null ? null : u.FullName))
            .Take(1000)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var contract = await Query().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hợp đồng.");
        return await ToDtoAsync(contract, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> CreateAsync(SaveContractRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var priceList = await CheckHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, priceList, cancellationToken);
        await CheckDepartmentsAsync(request.DepartmentIds, cancellationToken);
        // Nhập Excel (lát B4c) gọi trong giao dịch chung của cả file – khi đó dùng giao dịch đó, không mở giao dịch lồng.
        await using var transaction = _db.Database.CurrentTransaction is null ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        var no = await _numbers.NextAsync(DocumentTypes.Contract, request.ValidFrom, cancellationToken);
        var contract = PurchaseContract.Create(companyId, no, ToHeader(request));
        contract.SetLines(lines);
        contract.SetDepartments(request.DepartmentIds);
        if (request.Rebates is { } rebates) contract.SetRebates(ToRebates(rebates));
        _db.PurchaseContracts.Add(contract);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetAsync(contract.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> UpdateAsync(Guid id, SaveContractRequest request, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        var priceList = await CheckHeaderAsync(request, cancellationToken);
        var lines = await ResolveLinesAsync(request.Lines, priceList, cancellationToken);
        await CheckDepartmentsAsync(request.DepartmentIds, cancellationToken);
        // Đổi NCC: bỏ dòng cũ trước (dòng gắn với bảng giá của NCC cũ) rồi mới đổi đầu.
        if (request.SupplierId != contract.SupplierId) contract.SetLines([]);
        contract.Update(ToHeader(request));
        contract.SetLines(lines);
        contract.SetDepartments(request.DepartmentIds);
        if (request.Rebates is { } rebates) contract.SetRebates(ToRebates(rebates));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        await CheckReadyAsync(contract, false, cancellationToken);
        await _approvals.SubmitAsync(contract, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        // Kiểm tra lại lúc duyệt: NCC/bảng giá có thể đã đổi trạng thái từ lúc gửi; duyệt cấp cuối là đưa hợp đồng vào hiệu lực.
        await CheckReadyAsync(contract, true, cancellationToken);
        await _approvals.ApproveAsync(contract, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        await _approvals.RejectAsync(contract, request.Comment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> RecallAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        await _approvals.SkipPendingAsync(contract, cancellationToken);
        contract.ReturnToDraft(null);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> TerminateAsync(Guid id, TerminateContractRequest request, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        // Phụ lục đang mở sẽ áp vào hợp đồng đã chấm dứt – bắt hủy/rút phụ lục trước cho rõ ràng.
        if (await _db.PurchaseContractAmendments.AnyAsync(
                a => a.ContractId == id && (a.Status == ContractAmendmentStatus.Draft || a.Status == ContractAmendmentStatus.Submitted), cancellationToken))
            throw new BusinessRuleException($"Hợp đồng {contract.No} đang có phụ lục chưa duyệt xong – hủy hoặc rút phụ lục trước khi chấm dứt.");
        contract.Terminate(request.Date, request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContractDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var contract = await LoadAsync(id, cancellationToken);
        contract.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<PurchaseContract> Query() =>
        _db.PurchaseContracts.Include(c => c.Lines).Include(c => c.Departments).Include(c => c.Rebates).AsSplitQuery();

    private async Task<PurchaseContract> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var contract = await Query().SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw new NotFoundException("Không tìm thấy hợp đồng.");
        var ids = contract.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Hợp đồng có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
        return contract;
    }

    /// <summary>
    /// Bảng giá gắn được với hợp đồng của công ty đang làm việc: bảng của chính công ty (chưa ngừng) hoặc bảng toàn tập đoàn đã hiệu lực –
    /// phương án 2: các pháp nhân dùng chung bảng giá đã đàm phán (đề xuất Ban CUVT Bước 13). Bỏ filter công ty có điều kiện như bảng giá.
    /// </summary>
    private IQueryable<SupplierPriceList> UsablePriceLists()
    {
        var companyId = _scope.RequireCompany();
        return _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(p => (p.CompanyId == companyId && p.Status != PriceListStatus.Inactive) || (p.IsGroupWide && p.Status == PriceListStatus.Active));
    }

    /// <summary>NCC có trong danh mục, không ở danh sách đen; bảng giá dùng được và cùng NCC.</summary>
    private async Task<SupplierPriceList> CheckHeaderAsync(SaveContractRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        if (supplier.Status == SupplierStatus.Blacklisted)
            throw new BusinessRuleException($"NCC {supplier.Code} đang ở danh sách đen – không lập hợp đồng được.");
        var priceList = await UsablePriceLists().AsNoTracking().Include(p => p.Lines).SingleOrDefaultAsync(p => p.Id == request.PriceListId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bảng giá (hoặc bảng giá đã ngừng / chưa hiệu lực).");
        if (priceList.SupplierId != request.SupplierId) throw new BusinessRuleException($"Bảng giá {priceList.No} không phải của NCC {supplier.Code}.");
        return priceList;
    }

    /// <summary>
    /// Giá dòng lấy từ bảng giá gắn với hợp đồng: bậc có SL tối thiểu nhỏ nhất của cùng mặt hàng × ĐVT (giá cơ bản). Mặt hàng/ĐVT không có
    /// trong bảng giá thì chặn – hợp đồng chỉ ghi hàng đã có giá đàm phán.
    /// </summary>
    private async Task<List<(ContractLineData, Item, ContractLinePrice)>> ResolveLinesAsync(
        IReadOnlyList<SaveContractLineRequest> lines, SupplierPriceList priceList, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) =>
        {
            var item = items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).");
            var tier = priceList.Lines.Where(p => p.ItemId == l.ItemId && p.UomId == l.UomId).MinBy(p => p.MinQty)
                ?? throw new BusinessRuleException($"Dòng {index + 1}: mặt hàng {item.Code} (theo ĐVT đã chọn) không có trong bảng giá {priceList.No}.");
            return (new ContractLineData(l.Id, l.ItemId, l.UomId, l.ExpectedQty, l.MinCommitQty, l.Note), item, new ContractLinePrice(tier.UnitPrice, tier.DiscountPct));
        }).ToList();
    }

    /// <summary>Bộ phận phải thuộc công ty đang làm việc và đang dùng (filter công ty tự áp).</summary>
    private async Task CheckDepartmentsAsync(IReadOnlyList<Guid> departmentIds, CancellationToken cancellationToken)
    {
        var ids = departmentIds.Distinct().ToList();
        if (await _db.Departments.CountAsync(d => ids.Contains(d.Id) && d.IsActive, cancellationToken) < ids.Count)
            throw new NotFoundException("Không tìm thấy bộ phận (hoặc bộ phận đã ngừng dùng, thuộc công ty khác).");
    }

    /// <summary>
    /// Điều kiện gửi/duyệt: NCC đã duyệt (design.md mục 5.8); không chồng hiệu lực với hợp đồng khác (chờ duyệt/hiệu lực) của cùng công ty ×
    /// NCC – mỗi pháp nhân một hợp đồng với NCC trong một thời kỳ (phương án 2). Khi duyệt: bảng giá phải đang hiệu lực.
    /// </summary>
    private async Task CheckReadyAsync(PurchaseContract contract, bool approving, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == contract.SupplierId).Select(s => new { s.Code, s.Status }).SingleAsync(cancellationToken);
        if (supplier.Status != SupplierStatus.Approved)
            throw new BusinessRuleException($"NCC {supplier.Code} chưa ở trạng thái Đã duyệt – chưa ký hợp đồng nguyên tắc được.");
        if (await OverlappingContractAsync(contract, contract.ValidFrom, contract.ValidTo, cancellationToken) is { } overlap)
            throw new BusinessRuleException($"Hợp đồng {overlap} với NCC {supplier.Code} đang chờ duyệt/hiệu lực trùng thời gian – mỗi công ty một hợp đồng với NCC trong một thời kỳ.");
        if (!approving) return;
        var priceList = await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(p => p.Id == contract.PriceListId).Select(p => new { p.No, p.Status }).SingleAsync(cancellationToken);
        if (priceList.Status != PriceListStatus.Active)
            throw new BusinessRuleException($"Bảng giá {priceList.No} chưa hiệu lực – duyệt bảng giá trước khi duyệt hợp đồng.");
    }

    /// <summary>
    /// Số hợp đồng khác (chờ duyệt/hiệu lực) của cùng công ty × NCC có hiệu lực chồng lên [<paramref name="from"/>, <paramref name="to"/>];
    /// null nếu không có. Filter công ty tự áp.
    /// </summary>
    private Task<string?> OverlappingContractAsync(PurchaseContract contract, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        _db.PurchaseContracts.AsNoTracking()
            .Where(c => c.Id != contract.Id && c.SupplierId == contract.SupplierId
                        && (c.Status == ContractStatus.Submitted || c.Status == ContractStatus.Active)
                        && c.ValidFrom <= to && from <= c.ValidTo)
            .Select(c => c.No).FirstOrDefaultAsync(cancellationToken);

    private async Task<ContractDto> ToDtoAsync(PurchaseContract contract, CancellationToken cancellationToken)
    {
        var itemIds = contract.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = contract.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == contract.SupplierId).Select(s => new { s.Code, s.Name }).SingleAsync(cancellationToken);
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == contract.CompanyId).Select(c => c.Code).SingleAsync(cancellationToken);
        // Bảng giá đã gắn đọc bỏ filter công ty: bảng toàn tập đoàn thuộc tổng công ty; hợp đồng đã được nạp qua phạm vi người dùng.
        var priceList = await _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter]).AsNoTracking()
            .Where(p => p.Id == contract.PriceListId).Select(p => new { p.No, p.Title, p.Status }).SingleAsync(cancellationToken);
        var departmentIds = contract.Departments.Select(d => d.DepartmentId).ToList();
        var departments = await _db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).OrderBy(d => d.Code)
            .Select(d => new { d.Id, Label = d.Code + " – " + d.Name }).ToListAsync(cancellationToken);
        var createdBy = contract.CreatedBy is { } uid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var lines = contract.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            return new ContractLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "", l.ExpectedQty,
                l.MinCommitQty, l.UnitPrice, l.DiscountPct, l.ExpectedAmount, l.Note);
        }).ToList();

        string? readOnly = items.Count < itemIds.Count ? "Hợp đồng có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Purchase.ContractManage) ? "Bạn không có quyền lập, sửa hợp đồng nguyên tắc." : null;
        var canEdit = contract.Status == ContractStatus.Draft && readOnly is null;
        // Người duyệt có thể chỉ có quyền xem + duyệt nên nút Duyệt tính riêng, không theo readOnly.
        var canApprove = contract.IsAwaitingApproval && items.Count == itemIds.Count
            && await _approvals.ApproveBlockerAsync(contract, await _approvals.CurrentStepAsync(contract, cancellationToken), cancellationToken) is null;
        var amendments = await AmendmentsOfAsync(contract.Id, cancellationToken);
        var hasOpenAmendment = amendments.Any(a => a.Status is nameof(ContractAmendmentStatus.Draft) or nameof(ContractAmendmentStatus.Submitted));
        var isActive = contract.Status == ContractStatus.Active && readOnly is null;
        var actions = new ContractActions(
            canEdit, canEdit, canApprove, contract.Status == ContractStatus.Submitted && readOnly is null,
            isActive && !hasOpenAmendment, canEdit, isActive && !hasOpenAmendment, readOnly);
        var today = _clock.TodayInVietnam();
        return new ContractDto(
            contract.Id, contract.No, company, contract.SupplierId, supplier.Code, supplier.Name, contract.PriceListId, priceList.No, priceList.Title,
            priceList.Status.ToString(), contract.ContractNo, contract.Title, contract.SignDate, contract.ValidFrom, contract.ValidTo,
            contract.PriceAdjustmentTerms, contract.PaymentTerms, contract.CreditLimit, contract.DeliveryTerms, contract.DiscountTerms,
            contract.SponsorshipTerms, contract.PenaltyTerms, contract.OtherTerms, contract.Note, contract.Status.ToString(), contract.ExpectedAmount,
            departments.Select(d => d.Id).ToList(), departments.Select(d => d.Label).ToList(), lines, contract.CreatedAt, createdBy,
            contract.ActivatedAt, contract.TerminatedOn, contract.TerminationReason, contract.RejectReason, contract.DaysToExpiry(today),
            contract.ExpiresWithin(today, ExpiryWarningDays), await _approvals.HistoryAsync(contract.DocType, contract.Id, cancellationToken), amendments,
            actions, contract.Rebates.OrderBy(r => r.LineNo).Select(ToRebateDto).ToList());
    }

    /// <summary>Cam kết chiết khấu/tài trợ ra DTO: Value là % hoặc số tiền mỗi kỳ tùy cách tính.</summary>
    internal static ContractRebateDto ToRebateDto(PurchaseContractRebate r) =>
        new(r.Id, r.LineNo, r.Kind.ToString(), r.Method.ToString(), (r.Method == RebateMethod.Percent ? r.Rate : r.Amount) ?? 0m, r.MinPurchaseAmount,
            r.Period.ToString(), r.Description);

    private static List<ContractRebateData> ToRebates(IReadOnlyList<SaveContractRebateRequest> rebates) =>
        rebates.Select((r, index) => new ContractRebateData(
                r.Id, Parse<RebateKind>(r.Kind, index, "loại"), Parse<RebateMethod>(r.Method, index, "cách tính"), r.Value, r.MinPurchaseAmount,
                Parse<RebatePeriod>(r.Period, index, "kỳ quyết toán"), r.Description))
            .ToList();

    private static T Parse<T>(string? value, int index, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new BusinessRuleException($"Cam kết {index + 1}: {field} không hợp lệ.");

    private static ContractHeader ToHeader(SaveContractRequest r) =>
        new(r.SupplierId, r.PriceListId, r.ContractNo, r.Title, r.SignDate, r.ValidFrom, r.ValidTo, r.PriceAdjustmentTerms, r.PaymentTerms, r.CreditLimit,
            r.DeliveryTerms, r.DiscountTerms, r.SponsorshipTerms, r.PenaltyTerms, r.OtherTerms, r.Note);
}
