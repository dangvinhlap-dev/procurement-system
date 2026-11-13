using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Proc.Application.Common;
using Proc.Application.Purchasing;
using Proc.Domain.Documents;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Cấu hình nhắc quá hạn duyệt (lát O2c, design.md mục 19.3) – mục <c>ApprovalReminder</c> trong appsettings hoặc biến môi trường
/// <c>ApprovalReminder__Enabled</c>… Mặc định bật; môi trường test tắt để dịch vụ nền không sinh thông báo giữa chừng các test.
/// </summary>
public sealed class ApprovalReminderOptions
{
    /// <summary>Tên mục cấu hình.</summary>
    public const string Section = "ApprovalReminder";

    /// <summary>Bật nhắc quá hạn.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Bao nhiêu phút quét một lần (1–1440).</summary>
    public int PollMinutes { get; set; } = 15;

    /// <summary>Số lần nhắc tối đa cho một bước (1–30): lần đầu khi quá 1 ngày làm việc, mỗi lần sau cách lần trước 1 ngày làm việc.</summary>
    public int MaxReminders { get; set; } = 3;
}

/// <summary>
/// Nhắc quá hạn duyệt (lát O2c, design.md mục 19.3 – Q9 "nhắc sau 1 ngày làm việc"): bước đang đến lượt mà đã chờ quá 1 ngày làm việc kể từ mốc
/// đến lượt (<see cref="ApprovalOverdue.WaitingSince"/>) thì gửi thông báo nhắc tới người duyệt được bước (gồm người được ủy quyền); nhắc lại mỗi
/// ngày làm việc kể từ lần nhắc trước, tối đa <see cref="ApprovalReminderOptions.MaxReminders"/> lần. Chạy nền không có người dùng nên đọc bước
/// và phiếu bỏ filter phạm vi – chỉ đọc, không trả dữ liệu ra ngoài; thông báo gửi đúng người duyệt được như lúc gửi duyệt.
/// </summary>
public sealed class ApprovalReminderService : IApprovalReminderService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<ApprovalReminderOptions> _options;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    /// <param name="scope">Phạm vi người dùng (rỗng khi chạy nền).</param>
    /// <param name="clock">Đồng hồ.</param>
    /// <param name="options">Cấu hình nhắc.</param>
    public ApprovalReminderService(ProcDbContext db, ICurrentUserScope scope, IClock clock, IOptionsMonitor<ApprovalReminderOptions> options)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<ApprovalReminderResult> RemindAsync(CancellationToken cancellationToken, IReadOnlyCollection<Guid>? companyIds = null)
    {
        var now = _clock.UtcNow;
        // Không nhắc vào thứ 7, CN (dịch vụ chạy trễ vẫn đợi sang thứ 2).
        if (!ApprovalOverdue.IsWorkingDay(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, VietnamTime.Zone).DateTime))) return new(0, 0);
        var maxReminders = Math.Clamp(_options.CurrentValue.MaxReminders, 1, 30);

        var pendingQuery = _db.ApprovalSteps.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => s.Status == ApprovalStepStatus.Pending);
        if (companyIds is not null) pendingQuery = pendingQuery.Where(s => companyIds.Contains(s.CompanyId));
        var documentIds = await pendingQuery.Select(s => s.DocumentId).Distinct().ToListAsync(cancellationToken);
        if (documentIds.Count == 0) return new(0, 0);

        // Mọi bước chờ và đã duyệt của các phiếu đó ở mọi công ty: bước chờ của công ty khác quyết định chặng nào đang chạy (duyệt song song –
        // design.md mục 18.6), bước đã duyệt cho mốc đến lượt của cấp sau / chặng sau.
        var steps = await _db.ApprovalSteps.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(s => documentIds.Contains(s.DocumentId) && (s.Status == ApprovalStepStatus.Pending || s.Status == ApprovalStepStatus.Approved))
            .ToListAsync(cancellationToken);
        var rounds = steps.ToLookup(s => (s.DocType, s.DocumentId, s.Round));
        // Lượt cũ (đã bị từ chối) không còn bước chờ nên không có bước đến lượt.
        var active = rounds.SelectMany(ApprovalProgress.Active)
            .Where(s => companyIds is null || companyIds.Contains(s.CompanyId))
            .ToList();
        var activeIds = active.Select(s => s.Id).ToList();
        var history = (await _db.ApprovalReminders.AsNoTracking().IgnoreQueryFilters([ProcDbContext.CompanyFilter])
                .Where(r => activeIds.Contains(r.ApprovalStepId))
                .GroupBy(r => r.ApprovalStepId)
                .Select(g => new { StepId = g.Key, Count = g.Max(r => r.Sequence), Last = g.Max(r => r.RemindedAt) })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => r.StepId);

        var due = new List<(ApprovalStep Step, DateTimeOffset Since, int Sequence)>();
        foreach (var step in active)
        {
            if (ApprovalOverdue.WaitingSince(step, rounds[(step.DocType, step.DocumentId, step.Round)]) is not { } since) continue;
            var done = history.GetValueOrDefault(step.Id);
            if (done is not null && done.Count >= maxReminders) continue;
            // Lần đầu: đến lượt + 1 ngày làm việc; lần sau: lần nhắc trước + 1 ngày làm việc (dịch vụ dừng lâu cũng không nhắc dồn nhiều lần).
            var dueAt = ApprovalOverdue.AddWorkingDay(done?.Last ?? since, VietnamTime.Zone);
            if (now >= dueAt) due.Add((step, since, (done?.Count ?? 0) + 1));
        }

        if (due.Count == 0) return new(0, 0);
        var documents = await LoadDocumentsAsync(due.Select(d => d.Step).ToList(), cancellationToken);
        var engine = new ApprovalEngine(_db, _scope, _clock);
        var recipients = 0;
        var reminded = 0;
        foreach (var (step, since, sequence) in due)
        {
            // Phiếu đã rút/hủy/duyệt xong giữa lúc quét, hoặc đã sang lượt khác – không nhắc.
            if (!documents.TryGetValue((step.DocType, step.DocumentId), out var document)
                || !document.IsAwaitingApproval || document.ApprovalRound != step.Round) continue;
            var count = await engine.RemindOverdueAsync(document, step, since, sequence, cancellationToken);
            _db.ApprovalReminders.Add(ApprovalReminder.Create(step, sequence, now, count));
            recipients += count;
            reminded++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new(reminded, recipients);
    }

    /// <summary>
    /// Nạp phiếu của các bước theo loại (bỏ mọi filter phạm vi – chạy nền). Nạp kèm dòng ở loại cần dòng để tính giá trị duyệt hoặc NCC liên quan
    /// (RFQ: giá trị trúng thầu, NCC được mời; GTH: NCC chính/dự phòng). Loại chưa có ở đây thì bỏ qua (không nhắc).
    /// </summary>
    private async Task<Dictionary<(string, Guid), IApprovalSubject>> LoadDocumentsAsync(
        IReadOnlyCollection<ApprovalStep> steps, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, Guid), IApprovalSubject>();
        foreach (var group in steps.GroupBy(s => s.DocType))
        {
            var ids = group.Select(s => s.DocumentId).Distinct().ToList();
            IReadOnlyList<IApprovalSubject> documents = group.Key switch
            {
                DocumentTypes.PurchaseRequest => await _db.PurchaseRequests.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.PurchaseOrder => await _db.PurchaseOrders.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.PurchaseSummary => await _db.PurchaseSummaries.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.PaymentRequest => await _db.PaymentRequests.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.PriceList => await _db.SupplierPriceLists.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.RequestForQuotation => await _db.Rfqs.AsNoTracking().IgnoreQueryFilters()
                    .Include(r => r.Lines).Include(r => r.Suppliers).ThenInclude(s => s.QuotationLines).AsSplitQuery()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                ApprovalTypes.Supplier => await _db.SupplierChangeRequests.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.Contract => await _db.PurchaseContracts.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                ApprovalTypes.ContractAmendment => await _db.PurchaseContractAmendments.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.SoleSource => await _db.SoleSourceApprovals.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.MonthlySupplierApproval => await _db.MonthlySupplierApprovals.AsNoTracking().IgnoreQueryFilters()
                    .Include(x => x.Lines).AsSplitQuery()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                DocumentTypes.VolumeRebate => await _db.VolumeRebateAgreements.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => ids.Contains(d.Id)).ToListAsync(cancellationToken),
                _ => [],
            };
            foreach (var document in documents) result[(group.Key, document.Id)] = document;
        }

        return result;
    }
}
