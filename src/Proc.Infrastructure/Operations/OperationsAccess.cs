using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Operations;
using Proc.Domain.Operations;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Chốt chung của module vận hành (design.md mục 21.2 – N3): chỉ ở tổng công ty; lát QT7 thêm chỉ từ dải IP cho phép của cấu hình truy cập CFG.
/// </summary>
public sealed class OperationsAccess : IOperationsAccess
{
    /// <summary>Thông báo khi gọi chức năng vận hành ở công ty thành viên.</summary>
    public const string HeadOfficeOnlyMessage = "Chức năng quản trị vận hành chỉ dùng khi làm việc ở tổng công ty.";

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IRequestContext _request;
    private readonly IAccessPolicyProvider _policy;
    private readonly IOperationLog _operationLog;
    private readonly IOptionsMonitor<SecurityOptions> _security;

    /// <summary>Tạo chốt.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="request">IP của request.</param>
    /// <param name="policy">Cấu hình truy cập – dải IP quản trị.</param>
    /// <param name="operationLog">Nhật ký vận hành (ghi lần bị chặn vì IP).</param>
    /// <param name="security">Cấu hình theo máy – <see cref="SecurityOptions.IgnoreOpsIpRanges"/> (đọc lại khi file cấu hình đổi).</param>
    public OperationsAccess(
        ProcDbContext db, ICurrentUserScope scope, IRequestContext request, IAccessPolicyProvider policy, IOperationLog operationLog,
        IOptionsMonitor<SecurityOptions> security)
    {
        _security = security;
        _db = db;
        _scope = scope;
        _request = request;
        _policy = policy;
        _operationLog = operationLog;
    }

    /// <inheritdoc />
    public async Task EnsureHeadOfficeAsync(CancellationToken cancellationToken)
    {
        var companyId = _scope.CompanyId ?? throw new ForbiddenAccessException(HeadOfficeOnlyMessage);
        if (!await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == companyId && c.IsHeadOffice, cancellationToken))
            throw new ForbiddenAccessException(HeadOfficeOnlyMessage);

        // Dải IP quản trị (lát QT7): ngoài dải thì chặn mọi API QT dù có quyền – phiên bị lộ dùng từ máy lạ không chạy được phục hồi, ngắt phiên…
        // Cách gỡ khi lỡ khai sai dải: Security:IgnoreOpsIpRanges = true trong file cấu hình trên máy chủ.
        if (_security.CurrentValue.IgnoreOpsIpRanges) return;
        var policy = await _policy.GetAsync(cancellationToken);
        var ip = IPAddress.TryParse(_request.IpAddress, out var parsed) ? parsed : null;
        if (AccessPolicy.IsIpAllowed(policy.OpsIpRanges, ip)) return;

        await _operationLog.WriteAsync(new OperationLogEntry(OperationKinds.OpsIpDenied, _request.IpAddress, null, OperationResult.Failed,
            "Gọi chức năng quản trị vận hành từ IP ngoài dải cho phép (CFG)."), cancellationToken);
        throw new ForbiddenAccessException(
            $"Máy của bạn ({_request.IpAddress}) không nằm trong dải IP được dùng chức năng quản trị vận hành. Liên hệ quản trị hệ thống.");
    }
}
