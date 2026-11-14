using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>Chốt "chỉ ở tổng công ty" của module vận hành (design.md mục 21.2 – N3).</summary>
public sealed class OperationsAccess : IOperationsAccess
{
    /// <summary>Thông báo khi gọi chức năng vận hành ở công ty thành viên.</summary>
    public const string HeadOfficeOnlyMessage = "Chức năng quản trị vận hành chỉ dùng khi làm việc ở tổng công ty.";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>Tạo chốt.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    public OperationsAccess(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task EnsureHeadOfficeAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.CompanyId ?? throw new ForbiddenAccessException(HeadOfficeOnlyMessage);
        if (!await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId && c.IsHeadOffice, cancellationToken))
            throw new ForbiddenAccessException(HeadOfficeOnlyMessage);
    }
}
