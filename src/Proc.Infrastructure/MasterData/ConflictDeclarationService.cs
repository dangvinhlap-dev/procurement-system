using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.MasterData;

/// <summary>
/// Kê khai xung đột lợi ích (design.md mục 18.8). Bảng không lọc theo công ty (kê khai gắn với con người, chặn ở mọi công ty) nên phạm vi
/// xem kiểm tra ở đây: không có quyền xem mọi kê khai thì chỉ thấy của mình. Việc chặn duyệt/chấm thầu nằm ở <c>ApprovalEngine</c>.
/// </summary>
public sealed class ConflictDeclarationService : IConflictDeclarationService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ kê khai.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="clock">Đồng hồ (ngày Việt Nam xét hiệu lực).</param>
    public ConflictDeclarationService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConflictDeclarationDto>> ListAsync(CancellationToken cancellationToken) =>
        await Project(Visible().OrderByDescending(d => d.CreatedAt)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ConflictDeclarationDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Project(Visible().Where(d => d.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Không tìm thấy kê khai.");

    /// <inheritdoc />
    public async Task<ConflictDeclarationDto> CreateAsync(CreateConflictDeclarationRequest request, CancellationToken cancellationToken)
    {
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var userId = request.UserId ?? me;
        // Tự kê khai không cần quyền (ai cũng phải kê khai được); ghi thay người khác là việc của KSNB/Trưởng ban.
        if (userId != me && !_scope.HasPermission(Permissions.MasterData.ConflictManage))
            throw new ForbiddenAccessException("Bạn không có quyền ghi kê khai thay người khác.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken)) throw new BusinessRuleException("Không tìm thấy người dùng.");
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
            throw new BusinessRuleException("Không tìm thấy nhà cung cấp.");

        var today = _clock.TodayInVietnam();
        var declaration = ConflictDeclaration.Create(
            userId, request.SupplierId, Enum.Parse<ConflictRelation>(request.Relation), request.Description, request.ValidFrom ?? today);
        // Một cặp người × NCC chỉ một kê khai đang mở: thêm thông tin thì kết thúc kê khai cũ rồi kê khai lại, tránh trùng khi KSNB đối chiếu.
        if (await _db.ConflictDeclarations.AnyAsync(d => d.UserId == userId && d.SupplierId == request.SupplierId && d.ValidTo == null, cancellationToken))
            throw new BusinessRuleException("Người này đã có kê khai chưa kết thúc với nhà cung cấp này.");

        _db.ConflictDeclarations.Add(declaration);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(declaration.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConflictDeclarationDto> EndAsync(Guid id, EndConflictDeclarationRequest request, CancellationToken cancellationToken)
    {
        var declaration = await _db.ConflictDeclarations.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kê khai.");
        // Người kê khai không tự kết thúc: kết thúc là gỡ chặn duyệt nên phải do người kiểm soát xác nhận (design.md mục 18.8).
        declaration.End(request.ValidTo, request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConflictUserOptionDto>> UserOptionsAsync(CancellationToken cancellationToken) =>
        await _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.UserName)
            .Select(u => new ConflictUserOptionDto(u.Id, u.UserName!, u.FullName)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConflictSupplierOptionDto>> SupplierOptionsAsync(CancellationToken cancellationToken) =>
        await _db.Suppliers.AsNoTracking().OrderBy(s => s.Code)
            .Select(s => new ConflictSupplierOptionDto(s.Id, s.Code, s.Name)).ToListAsync(cancellationToken);

    /// <summary>Kê khai người dùng được xem: toàn bộ khi có quyền xem mọi kê khai, không thì của chính mình.</summary>
    private IQueryable<ConflictDeclaration> Visible()
    {
        var me = _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập.");
        var all = _scope.HasPermission(Permissions.MasterData.ConflictView) || _scope.HasPermission(Permissions.MasterData.ConflictManage);
        return _db.ConflictDeclarations.AsNoTracking().Where(d => all || d.UserId == me);
    }

    private IQueryable<ConflictDeclarationDto> Project(IQueryable<ConflictDeclaration> declarations)
    {
        var today = _clock.TodayInVietnam();
        return from d in declarations
            join u in _db.Users on d.UserId equals u.Id
            join s in _db.Suppliers on d.SupplierId equals s.Id
            select new ConflictDeclarationDto(
                d.Id, d.UserId, u.UserName!, u.FullName, d.SupplierId, s.Code, s.Name, d.Relation.ToString(), d.Description, d.ValidFrom,
                d.ValidTo, d.EndReason, d.ValidFrom <= today && (d.ValidTo == null || d.ValidTo >= today), d.CreatedAt,
                _db.Users.Where(c => c.Id == d.CreatedBy).Select(c => c.FullName).FirstOrDefault());
    }
}
