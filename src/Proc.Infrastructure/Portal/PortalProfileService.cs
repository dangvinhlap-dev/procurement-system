using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Portal;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Infrastructure.Documents;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Portal;

/// <summary>
/// Hồ sơ NCC trên Portal (lát P2a, design.md 19.5 P2-1…P2-6): mọi người dùng Portal của NCC xem hồ sơ và các đề nghị; chỉ **quản trị NCC** gửi,
/// rút đề nghị và thêm, xóa file. Mọi truy vấn lấy NCC từ phiên (P1b-4).
/// </summary>
public sealed class PortalProfileService : IPortalProfileService
{
    private readonly ProcDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUserScope _scope;
    private readonly AttachmentService _attachments;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="scope">Phạm vi người dùng (phiên Portal không có người dùng nội bộ – dùng cho bộ ghi thông báo).</param>
    /// <param name="attachments">Dịch vụ đính kèm.</param>
    public PortalProfileService(ProcDbContext db, IClock clock, ICurrentUserScope scope, AttachmentService attachments)
    {
        _db = db;
        _clock = clock;
        _scope = scope;
        _attachments = attachments;
    }

    /// <inheritdoc />
    public async Task<PortalProfileDto> GetAsync(PortalSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == session.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");
        var requests = await _db.SupplierProfileRequests.AsNoTracking().Where(r => r.SupplierId == session.SupplierId)
            .OrderByDescending(r => r.SubmittedAt).Take(100).ToListAsync(cancellationToken);
        var dtos = await SupplierProfileRequestMapper.ToDtosAsync(
            _db, requests, internalView: false, canProcess: false, canWithdraw: session.IsSupplierAdmin, cancellationToken);
        var pending = requests.FirstOrDefault(r => r.Status == SupplierProfileRequestStatus.Submitted);
        return new PortalProfileDto(
            supplier.Code, supplier.Name, supplier.ShortName, supplier.TaxCode, supplier.IsForeign, supplier.Address, supplier.Phone, supplier.Email,
            supplier.Website, supplier.SupplyScope, supplier.PaymentTerms, supplier.Status.ToString(), SupplierProfileRequestMapper.ContactsOf(supplier),
            SupplierProfileRequestMapper.BanksOf(supplier), session.IsSupplierAdmin && pending is null, pending?.No, dtos,
            _attachments.MaxFileSizeMb);
    }

    /// <inheritdoc />
    public async Task<SupplierProfileRequestDto> SubmitAsync(
        PortalSession session, PortalProfileUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        RequireAdmin(session);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        // Khóa NCC tới hết giao dịch: hai quản trị cùng NCC gửi cùng lúc thì lần lượt – người sau thấy đề nghị đang chờ, số không trùng.
        await RowLocks.LockAsync<Supplier>(_db, [session.SupplierId], cancellationToken);
        var supplier = await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == session.SupplierId, cancellationToken);
        var pending = await _db.SupplierProfileRequests.AsNoTracking()
            .Where(r => r.SupplierId == supplier.Id && r.Status == SupplierProfileRequestStatus.Submitted).Select(r => r.No)
            .FirstOrDefaultAsync(cancellationToken);
        if (pending is not null)
            throw new BusinessRuleException($"Đề nghị {pending} đang chờ Phòng Cung ứng vật tư xử lý – rút đề nghị đó nếu muốn gửi lại.");
        var count = await _db.SupplierProfileRequests.CountAsync(r => r.SupplierId == supplier.Id, cancellationToken);
        var profile = SupplierProfileRequest.Create(
            supplier, $"{supplier.Code}-HS{count + 1:00}", session.UserId, session.Email,
            new SupplierProfileData(request.Name, request.ShortName, request.Address, request.Phone, request.Email, request.Website,
                request.SupplyScope),
            (request.Contacts ?? []).Select(c => new SupplierContactData(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)),
            (request.BankAccounts ?? []).Select(b => new SupplierBankAccountData(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)),
            request.Note, _clock.UtcNow);
        _db.SupplierProfileRequests.Add(profile);
        await NotifyAsync(supplier, profile, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRequestAsync(session, profile.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SupplierProfileRequestDto> WithdrawAsync(
        PortalSession session, Guid id, SupplierProfileCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        RequireAdmin(session);
        await using var transaction = await RowLocks.BeginOrJoinAsync(_db, cancellationToken);
        await RowLocks.LockAsync<SupplierProfileRequest>(_db, [id], cancellationToken);
        var profile = await _db.SupplierProfileRequests.SingleOrDefaultAsync(r => r.Id == id && r.SupplierId == session.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đề nghị cập nhật hồ sơ.");
        profile.Withdraw(request.Comment, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRequestAsync(session, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentDto>> ListAttachmentsAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(session, id, cancellationToken);
        return await _attachments.ListForPortalAsync(AttachmentOwnerTypes.SupplierProfileFile, profile.Id, LockReason(session, profile), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentDto> UploadAttachmentAsync(
        PortalSession session, Guid id, string fileName, long sizeBytes, Stream content, string? note, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(session, id, cancellationToken);
        if (LockReason(session, profile) is { } reason) throw new BusinessRuleException(reason);
        return await _attachments.UploadForPortalAsync(AttachmentOwnerTypes.SupplierProfileFile, profile.Id, null, session.UserId,
            fileName, sizeBytes, content, note, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> OpenAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(session, id, cancellationToken);
        return await _attachments.OpenForPortalAsync(AttachmentOwnerTypes.SupplierProfileFile, attachmentId, profile.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(PortalSession session, Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(session, id, cancellationToken);
        if (LockReason(session, profile) is { } reason) throw new BusinessRuleException(reason);
        await _attachments.DeleteForPortalAsync(AttachmentOwnerTypes.SupplierProfileFile, attachmentId, profile.Id, session.UserId, cancellationToken);
    }

    /// <summary>Lý do file đã khóa: đề nghị đã xử lý / đã rút (mục 20 R1), hoặc người xem không phải quản trị NCC; null = còn thêm, xóa được.</summary>
    private static string? LockReason(PortalSession session, SupplierProfileRequest profile) =>
        profile.Status != SupplierProfileRequestStatus.Submitted ? $"Đề nghị {profile.No} đã xử lý hoặc đã rút – file chỉ xem."
        : !session.IsSupplierAdmin ? "Chỉ quản trị NCC thêm, xóa file của đề nghị."
        : null;

    /// <summary>Đề nghị của NCC phiên; khác NCC trả 404 như không tồn tại.</summary>
    private async Task<SupplierProfileRequest> FindAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return await _db.SupplierProfileRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.SupplierId == session.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đề nghị cập nhật hồ sơ.");
    }

    private async Task<SupplierProfileRequestDto> GetRequestAsync(PortalSession session, Guid id, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(session, id, cancellationToken);
        return (await SupplierProfileRequestMapper.ToDtosAsync(
            _db, [profile], internalView: false, canProcess: false, canWithdraw: session.IsSupplierAdmin, cancellationToken))[0];
    }

    /// <summary>Hồ sơ NCC là thông tin pháp lý, tài khoản nhận tiền: chỉ quản trị NCC (người Phòng CUVT mời) được đề nghị đổi (P2-1).</summary>
    private static void RequireAdmin(PortalSession session)
    {
        if (!session.IsSupplierAdmin)
            throw new ForbiddenAccessException("Chỉ quản trị NCC được gửi, rút đề nghị cập nhật hồ sơ – liên hệ quản trị NCC của công ty bạn.");
    }

    /// <summary>
    /// Báo người sửa được NCC ở tổng công ty (quyền "Thêm, sửa nhà cung cấp") – NCC là danh mục dùng chung, chỉ tổng công ty xử lý (P2-4).
    /// </summary>
    private async Task NotifyAsync(Supplier supplier, SupplierProfileRequest profile, CancellationToken cancellationToken)
    {
        var headOffice = await _db.Companies.AsNoTracking().Where(c => c.IsHeadOffice && c.IsActive).Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (headOffice is not { } companyId) return;
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        var receivers = await publisher.UsersWithPermissionAsync(companyId, Permissions.MasterData.SupplierManage, null, null, cancellationToken);
        if (receivers.Count == 0) return;
        publisher.Add(companyId, receivers, NotificationTypes.PortalProfileRequest,
            $"NCC {supplier.Code} đề nghị cập nhật hồ sơ {profile.No}",
            $"{supplier.Code} – {supplier.Name} gửi đề nghị cập nhật hồ sơ trên Portal. Xem so sánh và chấp nhận / từ chối ở màn hình "
            + "Đề nghị cập nhật hồ sơ NCC.",
            DocumentTypes.SupplierProfileRequest, profile.Id);
    }
}
