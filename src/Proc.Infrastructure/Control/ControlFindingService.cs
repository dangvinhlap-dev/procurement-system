using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.Documents;
using Proc.Domain.Common;
using Proc.Domain.Control;
using Proc.Domain.Documents;
using Proc.Domain.Notifications;
using Proc.Domain.Organization;
using Proc.Infrastructure.Notifications;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Control;

/// <summary>
/// Sổ vi phạm KSNB (design.md mục 18.19). Bảng lọc theo công ty đang làm việc (global query filter). Quyền ghi kiểm tra ở endpoint và lại
/// ở đây vì ghi nhận gửi thông báo cho lãnh đạo.
/// </summary>
public sealed class ControlFindingService : IControlFindingService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IDocumentNumberService _numbers;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    public ControlFindingService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IDocumentNumberService numbers)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _numbers = numbers;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ControlFindingDto>> ListAsync(CancellationToken cancellationToken)
    {
        EnsureView();
        return await Project(_db.ControlFindings.OrderByDescending(f => f.Date).ThenByDescending(f => f.No)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ControlFindingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureView();
        return await Project(_db.ControlFindings.Where(f => f.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy vi phạm.");
    }

    /// <inheritdoc />
    public async Task<ControlFindingDto> CreateAsync(SaveControlFindingRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        _scope.EnsurePermission(Permissions.Control.FindingManage);
        await CheckSupplierAsync(request.SupplierId, cancellationToken);

        // Cấp số, lưu và sinh thông báo trong một transaction: lưu lỗi thì số không nhảy và không có thông báo "ma".
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.ControlFinding, request.Date, cancellationToken);
        var finding = ControlFinding.Create(companyId, no, Data(request) with
        {
            AnomalyCode = request.AnomalyCode, RefScreen = request.RefScreen, RefId = request.RefId, RefNo = request.RefNo,
        });
        _db.ControlFindings.Add(finding);
        await NotifyAsync(finding, $"KSNB ghi nhận vi phạm {finding.No}", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(finding.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ControlFindingDto> UpdateAsync(Guid id, SaveControlFindingRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Control.FindingManage);
        var finding = await FindAsync(id, cancellationToken);
        await CheckSupplierAsync(request.SupplierId, cancellationToken);
        finding.Update(Data(request));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ControlFindingDto> ConcludeAsync(Guid id, ConcludeControlFindingRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Control.FindingManage);
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var finding = await FindAsync(id, cancellationToken);
        finding.Conclude(request.Conclusion, Enum.Parse<FindingSeverity>(request.Severity), _clock.UtcNow, me);
        await NotifyAsync(finding, $"KSNB kết luận vi phạm {finding.No}", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Xem sổ vi phạm: KSNB (xem dấu hiệu) hoặc lãnh đạo (nhận báo cáo vi phạm).</summary>
    private void EnsureView()
    {
        _scope.RequireCompany();
        if (!_scope.HasPermission(Permissions.Control.AnomalyView) && !_scope.HasPermission(Permissions.Control.FindingReceive))
            throw new ForbiddenAccessException("Bạn không có quyền xem sổ vi phạm.");
    }

    /// <summary>
    /// Thông báo thẳng người có quyền nhận báo cáo vi phạm tại công ty (lãnh đạo) – không qua Nhóm Đàm phán/Vận hành (design.md mục 18.19).
    /// </summary>
    private async Task NotifyAsync(ControlFinding finding, string title, CancellationToken cancellationToken)
    {
        var publisher = new NotificationPublisher(_db, _scope, _clock);
        var receivers = await publisher.UsersWithPermissionAsync(finding.CompanyId, Permissions.Control.FindingReceive, null, null, cancellationToken);
        var severity = finding.Severity == FindingSeverity.Serious ? "Nghiêm trọng" : "Thường";
        var message = finding.Status == FindingStatus.Concluded
            ? $"{severity}: {finding.Title}. Kết luận: {finding.Conclusion}"
            : $"{severity}: {finding.Title}{(finding.RefNo is null ? string.Empty : $" (chứng từ {finding.RefNo})")}";
        publisher.Add(finding.CompanyId, receivers, NotificationTypes.ControlFinding, title, message, "VIOL", finding.Id);
    }

    /// <summary>Tìm phát hiện vi phạm theo Id; không thấy thì báo lỗi 404.</summary>
    private async Task<ControlFinding> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.ControlFindings.SingleOrDefaultAsync(f => f.Id == id, cancellationToken) ?? throw new NotFoundException("Không tìm thấy vi phạm.");

    /// <summary>Kiểm tra NCC gắn với phát hiện vi phạm có tồn tại.</summary>
    private async Task CheckSupplierAsync(Guid? supplierId, CancellationToken cancellationToken)
    {
        if (supplierId is { } id && !await _db.Suppliers.AnyAsync(s => s.Id == id, cancellationToken))
            throw new BusinessRuleException("Không tìm thấy nhà cung cấp.");
    }

    /// <summary>Đổi yêu cầu lưu từ API thành dữ liệu cho Domain.</summary>
    private static ControlFindingData Data(SaveControlFindingRequest request) =>
        new(request.Date, Enum.Parse<FindingSeverity>(request.Severity), request.Title, request.Description, request.SupplierId);

    /// <summary>Chiếu danh sách phát hiện vi phạm sang DTO trả về giao diện.</summary>
    private IQueryable<ControlFindingDto> Project(IQueryable<ControlFinding> query) =>
        from f in query.AsNoTracking()
        select new ControlFindingDto(
            f.Id, f.No, f.Date, f.Severity.ToString(), f.Status.ToString(), f.AnomalyCode, f.Title, f.Description, f.RefScreen, f.RefId, f.RefNo,
            f.SupplierId,
            _db.Suppliers.Where(s => s.Id == f.SupplierId).Select(s => s.Code).FirstOrDefault(),
            _db.Suppliers.Where(s => s.Id == f.SupplierId).Select(s => s.Name).FirstOrDefault(),
            f.Conclusion, f.ConcludedAt,
            _db.Users.Where(u => u.Id == f.ConcludedBy).Select(u => u.FullName).FirstOrDefault(),
            f.CreatedAt,
            _db.Users.Where(u => u.Id == f.CreatedBy).Select(u => u.FullName).FirstOrDefault());
}
