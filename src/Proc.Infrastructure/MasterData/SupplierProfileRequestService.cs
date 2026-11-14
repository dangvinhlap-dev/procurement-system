using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Đề nghị cập nhật hồ sơ NCC gửi từ Portal – phía nội bộ (lát P2a, design.md 19.5 P2-4, P2-5). NCC là danh mục dùng chung nên đề nghị không
/// thuộc công ty; người sửa được NCC (tổng công ty) chấp nhận / từ chối.
/// </summary>
public sealed class SupplierProfileRequestService : ISupplierProfileRequestService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IPortalEmailService _emails;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="emails">Thư báo NCC qua Portal.</param>
    public SupplierProfileRequestService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IPortalEmailService emails)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _emails = emails;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierProfileRequestDto>> ListAsync(SupplierProfileRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _scope.EnsurePermission(Permissions.MasterData.SupplierView);
        var rows = _db.SupplierProfileRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<SupplierProfileRequestStatus>(query.Status, true, out var status) || !Enum.IsDefined(status))
                throw new BusinessRuleException($"Trạng thái '{query.Status}' không hợp lệ.");
            rows = rows.Where(r => r.Status == status);
        }

        if (query.SupplierId is { } supplierId) rows = rows.Where(r => r.SupplierId == supplierId);
        var list = await rows.OrderByDescending(r => r.SubmittedAt).Take(1000).ToListAsync(cancellationToken);
        var canProcess = await CanProcessAsync(cancellationToken);
        return await SupplierProfileRequestMapper.ToDtosAsync(_db, list, internalView: true, canProcess, false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierProfileRequestDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.MasterData.SupplierView);
        var request = await _db.SupplierProfileRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đề nghị cập nhật hồ sơ NCC.");
        var canProcess = await CanProcessAsync(cancellationToken);
        return (await SupplierProfileRequestMapper.ToDtosAsync(_db, [request], internalView: true, canProcess, false, cancellationToken))[0];
    }

    /// <inheritdoc />
    public async Task<SupplierProfileRequestDto> AcceptAsync(Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await SharedCatalog.RequireEditAsync(_db, _scope, "nhà cung cấp", Permissions.MasterData.SupplierManage, cancellationToken);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Cần đăng nhập.");
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        // Khóa đề nghị: hai người cùng bấm, hoặc NCC rút đúng lúc chấp nhận – lần lượt, người sau thấy trạng thái mới.
        await RowLocks.LockAsync<SupplierProfileRequest>(_db, [id], cancellationToken);
        var profile = await FindAsync(id, cancellationToken);
        var supplier = await _db.Suppliers.SingleAsync(s => s.Id == profile.SupplierId, cancellationToken);
        var now = _clock.UtcNow;
        profile.Accept(supplier, userId, request.Comment, now);

        // Tài khoản ngân hàng là nơi nhận tiền: không áp ngay mà lập yêu cầu duyệt NCC hai chặng như khi Phòng CUVT tự sửa (Q7, mục 18.6).
        if (profile.ProposedBankAccounts is { } banks && supplier.BankAccountsDifferFrom(Supplier.NormalizeBankAccounts(banks)))
        {
            var companyId = _scope.RequireCompany();
            var change = await SupplierChangeRequestService.SubmitAsync(_db, new ApprovalEngine(_db, _scope, _clock), supplier,
                no => SupplierChangeRequest.ForBankAccounts(supplier, companyId, no, banks), cancellationToken);
            profile.LinkBankChangeRequest(change.No);
        }

        await _emails.QueueProfileRequestAsync(profile, SupplierPortalEmailKind.ProfileRequestAccepted, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierProfileRequestDto> RejectAsync(Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await SharedCatalog.RequireEditAsync(_db, _scope, "nhà cung cấp", Permissions.MasterData.SupplierManage, cancellationToken);
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Cần đăng nhập.");
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await RowLocks.LockAsync<SupplierProfileRequest>(_db, [id], cancellationToken);
        var profile = await FindAsync(id, cancellationToken);
        profile.Reject(userId, request.Comment, _clock.UtcNow);
        await _emails.QueueProfileRequestAsync(profile, SupplierPortalEmailKind.ProfileRequestRejected, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Người xem chấp nhận / từ chối được không (sửa được NCC – danh mục dùng chung).</summary>
    private Task<bool> CanProcessAsync(CancellationToken cancellationToken) =>
        SharedCatalog.CanEditAsync(_db, _scope, Permissions.MasterData.SupplierManage, cancellationToken);

    /// <summary>Tìm đề nghị (theo dõi thay đổi); không thấy thì 404.</summary>
    private async Task<SupplierProfileRequest> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.SupplierProfileRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy đề nghị cập nhật hồ sơ NCC.");
}

/// <summary>Đổi đề nghị cập nhật hồ sơ NCC sang DTO – dùng chung cho màn hình nội bộ và Portal (lát P2a).</summary>
internal static class SupplierProfileRequestMapper
{
    /// <summary>
    /// Đổi danh sách đề nghị sang DTO kèm hồ sơ hiện tại của NCC để so sánh.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="requests">Đề nghị.</param>
    /// <param name="internalView">true = màn hình nội bộ (có tên người xử lý, Id yêu cầu duyệt NCC); false = Portal.</param>
    /// <param name="canProcess">Người xem chấp nhận / từ chối được (chỉ áp cho đề nghị còn chờ).</param>
    /// <param name="canWithdraw">Người xem rút được (chỉ áp cho đề nghị còn chờ).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách DTO cùng thứ tự.</returns>
    public static async Task<IReadOnlyList<SupplierProfileRequestDto>> ToDtosAsync(
        ProcDbContext db, IReadOnlyList<SupplierProfileRequest> requests, bool internalView, bool canProcess, bool canWithdraw,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return [];
        var supplierIds = requests.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        var portalIds = requests.Select(r => r.PortalUserId).Distinct().ToList();
        var portalUsers = await db.SupplierPortalUsers.AsNoTracking().Where(u => portalIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var reviewerIds = requests.Select(r => r.ReviewedBy).OfType<Guid>().Distinct().ToList();
        var reviewers = internalView
            ? await db.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken)
            : [];
        var bankNos = requests.Select(r => r.BankChangeRequestNo).OfType<string>().Distinct().ToList();
        var bankRequests = internalView
            ? await db.SupplierChangeRequests.AsNoTracking().Where(c => bankNos.Contains(c.No)).ToDictionaryAsync(c => c.No, c => c.Id, cancellationToken)
            : [];
        return requests.Select(r =>
        {
            var supplier = suppliers[r.SupplierId];
            var pending = r.Status == SupplierProfileRequestStatus.Submitted;
            var fields = r.FieldChanges
                .Select(f => new SupplierProfileFieldDto(f.Field, f.Label, f.Old, f.New, CurrentValue(supplier, f.Field)))
                .ToList();
            var contacts = r.ProposedContacts?.Select(ToDto).ToList();
            var banks = r.ProposedBankAccounts?.Select(ToDto).ToList();
            return new SupplierProfileRequestDto(
                r.Id, r.No, r.SupplierId, supplier.Code, supplier.Name, r.Status.ToString(), r.SubmittedAt, r.SubmittedByEmail,
                portalUsers.GetValueOrDefault(r.PortalUserId), r.Note, Summary(r), fields, contacts, ContactsOf(supplier), banks, BanksOf(supplier),
                r.ReviewedBy is { } by ? reviewers.GetValueOrDefault(by) : null, r.ClosedAt, r.ReviewComment, r.BankChangeRequestNo,
                r.BankChangeRequestNo is { } no && bankRequests.TryGetValue(no, out var bankId) ? bankId : null,
                pending && canProcess, pending && canWithdraw);
        }).ToList();
    }

    /// <summary>Người liên hệ hiện tại của NCC.</summary>
    public static List<SupplierProfileContactDto> ContactsOf(Supplier supplier) =>
        supplier.Contacts.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c => new SupplierProfileContactDto(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)).ToList();

    /// <summary>Tài khoản ngân hàng hiện tại của NCC.</summary>
    public static List<SupplierProfileBankDto> BanksOf(Supplier supplier) =>
        supplier.BankAccounts.OrderByDescending(b => b.IsDefault).ThenBy(b => b.BankName, StringComparer.CurrentCulture)
            .Select(b => new SupplierProfileBankDto(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)).ToList();

    /// <summary>Tóm tắt phần đề nghị đổi, ví dụ "Địa chỉ, Website; người liên hệ; tài khoản ngân hàng".</summary>
    private static string Summary(SupplierProfileRequest r)
    {
        var parts = new List<string>();
        var fields = r.FieldChanges;
        if (fields.Count > 0) parts.Add(string.Join(", ", fields.Select(f => f.Label)));
        if (r.ContactsJson is not null) parts.Add("người liên hệ");
        if (r.BankAccountsJson is not null) parts.Add("tài khoản ngân hàng");
        return string.Join("; ", parts);
    }

    /// <summary>Giá trị hiện tại của một trường thông tin chung NCC theo tên trường đã lưu trong đề nghị.</summary>
    private static string? CurrentValue(Supplier s, string field) => field switch
    {
        nameof(Supplier.Name) => s.Name,
        nameof(Supplier.ShortName) => s.ShortName,
        nameof(Supplier.Address) => s.Address,
        nameof(Supplier.Phone) => s.Phone,
        nameof(Supplier.Email) => s.Email,
        nameof(Supplier.Website) => s.Website,
        nameof(Supplier.SupplyScope) => s.SupplyScope,
        _ => null,
    };

    private static SupplierProfileContactDto ToDto(SupplierContactData c) => new(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary);

    private static SupplierProfileBankDto ToDto(SupplierBankAccountData b) =>
        new(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault);
}
