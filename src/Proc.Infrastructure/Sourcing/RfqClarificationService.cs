using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Sourcing;
using Proc.Domain.Common;
using Proc.Domain.Sourcing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Sourcing;

/// <summary>
/// Hỏi đáp làm rõ RFQ phía nội bộ (lát P3b-2 – design.md mục 19.5, P3-11…P3-14). RFQ nạp qua bộ lọc toàn cục (công ty đang làm việc) và
/// kiểm phạm vi nhóm hàng như <see cref="RfqService"/>: người không thao tác được RFQ thì không đọc, trả lời hỏi đáp của nó.
/// </summary>
public sealed class RfqClarificationService : IRfqClarificationService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ.</param>
    public RfqClarificationService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RfqClarificationDto>> ListAsync(Guid rfqId, CancellationToken cancellationToken)
    {
        var rfq = await LoadAsync(rfqId, cancellationToken);
        return await ToDtosAsync(rfq.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RfqClarificationDto>> ReplyAsync(
        Guid rfqId, Guid id, ReplyClarificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rfq = await LoadAsync(rfqId, cancellationToken);
        var item = await _db.RfqClarifications.SingleOrDefaultAsync(x => x.Id == id && x.RfqId == rfq.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi.");
        var (userId, name) = await UserAsync(cancellationToken);
        item.Reply(rfq, userId, name, request.Answer, _clock.UtcNow);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Câu hỏi vừa được người khác trả lời – tải lại để xem.");
        }

        return await ToDtosAsync(rfq.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RfqClarificationDto>> AnnounceAsync(
        Guid rfqId, AnnounceClarificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rfq = await LoadAsync(rfqId, cancellationToken);
        var (userId, name) = await UserAsync(cancellationToken);
        _db.RfqClarifications.Add(RfqClarification.Announce(rfq, userId, name, request.Text, _clock.UtcNow));
        await _db.SaveChangesAsync(cancellationToken);
        return await ToDtosAsync(rfq.Id, cancellationToken);
    }

    /// <summary>Nạp RFQ của công ty đang làm việc; có mặt hàng ngoài phạm vi nhóm hàng thì 403 như mọi thao tác RFQ.</summary>
    private async Task<Rfq> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rfq = await _db.Rfqs.AsNoTracking().Include(r => r.Lines).Include(r => r.Suppliers).AsSplitQuery()
                      .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new NotFoundException("Không tìm thấy RFQ.");
        var ids = rfq.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("RFQ có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
        return rfq;
    }

    private async Task<(Guid UserId, string Name)> UserAsync(CancellationToken cancellationToken)
    {
        var userId = _scope.UserId ?? throw new ForbiddenAccessException("Cần đăng nhập.");
        var name = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        return (userId, string.IsNullOrWhiteSpace(name) ? "(không rõ)" : name);
    }

    private async Task<IReadOnlyList<RfqClarificationDto>> ToDtosAsync(Guid rfqId, CancellationToken cancellationToken)
    {
        var items = await _db.RfqClarifications.AsNoTracking().Where(x => x.RfqId == rfqId)
            .OrderBy(x => x.AskedAt ?? x.AnsweredAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var supplierIds = items.Select(x => x.SupplierId).OfType<Guid>().Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().IgnoreQueryFilters().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name }, cancellationToken);
        return items.Select(x =>
            {
                var supplier = x.SupplierId is { } sid ? suppliers.GetValueOrDefault(sid) : null;
                return new RfqClarificationDto(x.Id, x.IsNotice, x.SupplierId, supplier?.Code, supplier?.Name, x.Question, x.AskedAt,
                    x.AskedByEmail, x.Answer, x.AnsweredAt, x.AnsweredByName);
            })
            .ToList();
    }
}
