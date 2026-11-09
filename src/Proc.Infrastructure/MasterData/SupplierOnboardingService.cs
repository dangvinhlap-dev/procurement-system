using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25; quyết định Q2). Hồ sơ thuộc tổng công ty (danh mục NCC dùng chung do tổng công ty
/// quản lý) nên mọi thao tác chỉ khi đang làm việc ở tổng công ty. Việc duyệt không có luồng riêng: đề xuất lập yêu cầu duyệt NCC sang
/// Đã duyệt (hai chặng, mục 18.6 – mẫu duyệt "Hội đồng"); yêu cầu kết thúc thì <see cref="SupplierOnboardingSync"/> đổi hồ sơ theo.
/// </summary>
public sealed class SupplierOnboardingService : ISupplierOnboardingService
{
    private const string What = "hồ sơ đánh giá NCC mới";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IDocumentNumberService _numbers;
    private readonly ApprovalEngine _approvals;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    public SupplierOnboardingService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IDocumentNumberService numbers)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _numbers = numbers;
        _approvals = new ApprovalEngine(db, scope, clock);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierOnboardingListItemDto>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        EnsureView();
        SupplierOnboardingStatus? wanted = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SupplierOnboardingStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new BusinessRuleException("Trạng thái hồ sơ không hợp lệ.");
            wanted = parsed;
        }

        var rows = await (
                from o in _db.SupplierOnboardings.AsNoTracking()
                where wanted == null || o.Status == wanted
                join s in _db.Suppliers on o.SupplierId equals s.Id
                join c in _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]) on o.CategoryId equals c.Id
                orderby o.CreatedAt descending
                select new
                {
                    o.Id, o.No, o.SupplierId, s.Code, s.Name, Category = c.Name, o.Date, o.Score, o.Result, o.Recommended, o.Status, o.CreatedAt,
                    Creator = _db.Users.Where(u => u.Id == o.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                })
            .Take(2000)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new SupplierOnboardingListItemDto(
                r.Id, r.No, r.SupplierId, r.Code, r.Name, r.Category, r.Date, r.Score, r.Result?.ToString(), r.Recommended, r.Status.ToString(),
                r.Creator, r.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureView();
        var onboarding = await _db.SupplierOnboardings.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ đánh giá NCC mới.");
        return await ToDtoAsync(onboarding, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> CreateAsync(SaveSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierOnboardingManage);
        var companyId = await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var supplier = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        SupplierOnboarding.EnsurePotential(supplier);
        // Mỗi NCC một hồ sơ còn mở – chỉ mục duy nhất có lọc UX_SupplierOnboarding_Open chặn cả khi lập đồng thời.
        var open = await _db.SupplierOnboardings.Where(o => o.SupplierId == supplier.Id
                && (o.Status == SupplierOnboardingStatus.Draft || o.Status == SupplierOnboardingStatus.Evaluated || o.Status == SupplierOnboardingStatus.Submitted))
            .Select(o => o.No).FirstOrDefaultAsync(cancellationToken);
        if (open is not null) throw new BusinessRuleException($"NCC {supplier.Code} đang có hồ sơ {open} chưa kết thúc.");
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);

        // Cấp số và lưu trong một transaction: lưu lỗi thì số không nhảy.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.SupplierOnboarding, request.Date, cancellationToken);
        var onboarding = SupplierOnboarding.Create(companyId, no, supplier, Data(request), _clock.TodayInVietnam());
        _db.SupplierOnboardings.Add(onboarding);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(onboarding.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> UpdateAsync(Guid id, SaveSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        var onboarding = await FindForManageAsync(id, cancellationToken);
        await SaveDataAsync(onboarding, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> EvaluateAsync(Guid id, SaveSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var onboarding = await FindForManageAsync(id, cancellationToken);
        await SaveDataAsync(onboarding, request, cancellationToken);
        onboarding.Evaluate(_clock.UtcNow, me);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        var onboarding = await FindForManageAsync(id, cancellationToken);
        onboarding.Reopen();
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> ProposeAsync(Guid id, ProposeSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierOnboardingPropose);
        var companyId = await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var onboarding = await FindAsync(id, cancellationToken);
        var now = _clock.UtcNow;
        if (!request.Recommend)
        {
            onboarding.NotRecommend(request.Comment, now, me);
            await _db.SaveChangesAsync(cancellationToken);
            return await GetAsync(id, cancellationToken);
        }

        var supplier = await _db.Suppliers.SingleAsync(s => s.Id == onboarding.SupplierId, cancellationToken);
        // NCC có thể đã đổi trạng thái sau khi lập hồ sơ (tạm ngưng, danh sách đen…) – chỉ đề xuất NCC còn Tiềm năng.
        SupplierOnboarding.EnsurePotential(supplier);
        await EnsureCategoryAsync(onboarding.CategoryId, cancellationToken);
        // Kiểm tra trên hồ sơ trước khi lập yêu cầu: chưa đánh giá / Không đạt / tự đề xuất thì không lập yêu cầu thừa.
        onboarding.EnsureCanRecommend(me);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var change = await SupplierChangeRequestService.SubmitAsync(_db, _approvals, supplier,
            no => SupplierChangeRequest.ForStatus(supplier, companyId, no, SupplierStatus.Approved, $"Theo hồ sơ đánh giá NCC mới {onboarding.No}"),
            cancellationToken);
        onboarding.Recommend(request.Comment, now, me, change.Id);
        // Quy trình duyệt luôn có ít nhất một cấp nên thường còn chờ; phòng khi yêu cầu được duyệt ngay thì hồ sơ đổi theo luôn.
        await SupplierOnboardingSync.AfterChangeRequestAsync(_db, change, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> WithdrawAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierOnboardingPropose);
        var onboarding = await FindAsync(id, cancellationToken);
        if (onboarding.Status != SupplierOnboardingStatus.Submitted || onboarding.ChangeRequestId is not { } changeId)
            throw new BusinessRuleException($"Hồ sơ {onboarding.No} không ở trạng thái Chờ duyệt.");
        var change = await _db.SupplierChangeRequests.SingleAsync(r => r.Id == changeId, cancellationToken);
        await SupplierChangeRequestService.WithdrawAsync(_db, _scope, _approvals, change, _clock.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierOnboardingDto> CancelAsync(Guid id, CancelSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        var onboarding = await FindForManageAsync(id, cancellationToken);
        onboarding.Cancel(request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private void EnsureView()
    {
        _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.MasterData.SupplierOnboardingView)
            && !_scope.HasPermission(Permissions.MasterData.SupplierOnboardingManage)
            && !_scope.HasPermission(Permissions.MasterData.SupplierOnboardingPropose))
            throw new ForbiddenAccessException("Bạn không có quyền xem hồ sơ đánh giá NCC mới.");
    }

    private async Task<SupplierOnboarding> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await HeadOffice.RequireAsync(_db, _scope, What, cancellationToken);
        return await _db.SupplierOnboardings.SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ đánh giá NCC mới.");
    }

    private async Task<SupplierOnboarding> FindForManageAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierOnboardingManage);
        return await FindAsync(id, cancellationToken);
    }

    /// <summary>Sửa nội dung hồ sơ nháp; NCC giữ như lúc lập.</summary>
    private async Task SaveDataAsync(SupplierOnboarding onboarding, SaveSupplierOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (request.SupplierId != onboarding.SupplierId) throw new BusinessRuleException("Không đổi NCC của hồ sơ đã lập – hủy hồ sơ rồi lập hồ sơ mới.");
        if (request.CategoryId != onboarding.CategoryId) await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        onboarding.Update(Data(request), _clock.TodayInVietnam());
    }

    /// <summary>Nhóm hàng đề xuất phải có và đang dùng (ngành hàng NCC không lọc theo phạm vi nhóm hàng – mục 5.3).</summary>
    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Where(c => c.Id == categoryId).Select(c => new { c.Code, c.IsActive }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhóm hàng đề xuất.");
        if (!category.IsActive) throw new BusinessRuleException($"Nhóm hàng {category.Code} đã ngừng dùng.");
    }

    private static SupplierOnboardingData Data(SaveSupplierOnboardingRequest r) =>
        new(r.Date, r.CategoryId, r.Source,
            (r.Documents ?? []).Select(d => new SupplierOnboardingDocument(d.Code.Trim().ToUpperInvariant(), d.Provided, d.ValidTo, d.Note)).ToList(),
            new SupplierOnboardingScores(r.PriceScore, r.SampleQualityScore, r.CapacityScore, r.DeliveryScore, r.PaymentScore, r.LegalScore),
            string.IsNullOrWhiteSpace(r.Result) ? null : Enum.Parse<SupplierOnboardingResult>(r.Result, true), r.Comment, r.Note);

    private async Task<SupplierOnboardingDto> ToDtoAsync(SupplierOnboarding o, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.AsNoTracking().Where(s => s.Id == o.SupplierId)
            .Select(s => new { s.Code, s.Name, s.Status }).SingleAsync(cancellationToken);
        var category = await _db.ItemCategories.IgnoreQueryFilters([ProcDbContext.ItemCategoryFilter]).AsNoTracking()
            .Where(c => c.Id == o.CategoryId).Select(c => new { c.Code, c.Name }).SingleAsync(cancellationToken);
        var userIds = new[] { o.CreatedBy, o.EvaluatedBy, o.ProposedBy }.OfType<Guid>().Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var changeNo = o.ChangeRequestId is { } changeId
            ? await _db.SupplierChangeRequests.AsNoTracking().Where(r => r.Id == changeId).Select(r => r.No).SingleOrDefaultAsync(cancellationToken)
            : null;
        var changeCreator = o.ChangeRequestId is { } cid
            ? await _db.SupplierChangeRequests.AsNoTracking().Where(r => r.Id == cid).Select(r => r.CreatedBy).SingleOrDefaultAsync(cancellationToken)
            : null;

        var me = _scope.UserId;
        var manage = _scope.HasPermission(Permissions.MasterData.SupplierOnboardingManage);
        var propose = _scope.HasPermission(Permissions.MasterData.SupplierOnboardingPropose);
        var actions = new SupplierOnboardingActions(
            CanEdit: manage && o.Status == SupplierOnboardingStatus.Draft,
            CanReopen: manage && o.Status == SupplierOnboardingStatus.Evaluated,
            CanPropose: propose && o.Status == SupplierOnboardingStatus.Evaluated && o.EvaluatedBy != me,
            CanWithdraw: propose && o.Status == SupplierOnboardingStatus.Submitted && changeCreator is not null && changeCreator == me,
            CanCancel: manage && o.Status is SupplierOnboardingStatus.Draft or SupplierOnboardingStatus.Evaluated);
        var documents = o.Documents.Select(d => new SupplierOnboardingDocumentDto(
            d.Code, SupplierOnboarding.NameOf(d.Code), d.Provided, d.ValidTo, d.Note, d.Code == SupplierOnboardingDocuments.License)).ToList();
        string? Name(Guid? id) => id is { } u ? users.GetValueOrDefault(u) : null;

        return new SupplierOnboardingDto(
            o.Id, o.No, o.SupplierId, supplier.Code, supplier.Name, supplier.Status.ToString(), o.CategoryId, category.Code, category.Name, o.Date,
            o.Source, documents, o.PriceScore, o.SampleQualityScore, o.CapacityScore, o.DeliveryScore, o.PaymentScore, o.LegalScore, o.Score,
            o.Result?.ToString(), o.Comment, o.Note, o.Status.ToString(), o.EvaluatedAt, Name(o.EvaluatedBy), o.Recommended, o.ProposalComment,
            o.ProposedAt, Name(o.ProposedBy), o.ChangeRequestId, changeNo, o.ApprovedAt, o.RejectReason, o.CancelReason, o.CreatedAt,
            Name(o.CreatedBy), actions);
    }
}
