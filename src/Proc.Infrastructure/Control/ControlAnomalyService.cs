using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Control;
using Proc.Application.Reports;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Persistence;
using Proc.Infrastructure.Purchasing;
using Proc.Infrastructure.Reports;

namespace Proc.Infrastructure.Control;

/// <summary>
/// Rà dấu hiệu bất thường cho Kiểm soát nội bộ (design.md mục 18.19). Mỗi dấu hiệu dùng lại đúng số liệu của báo cáo nguồn (PRCW – nguồn
/// giá dòng PO, MATCH3 – đối chiếu phiếu nhận/hóa đơn) để KSNB đối chiếu được.
/// <para>
/// Toàn tập đoàn (quyền <c>Report.Purchase.ViewAllCompanies</c>) bỏ filter công ty và kho như các báo cáo mua hàng (mục 11.3); filter
/// nhóm hàng vẫn giữ vì mọi truy vấn dòng hàng đều join mặt hàng. Chỉ bỏ filter sau khi đã kiểm quyền.
/// </para>
/// </summary>
public sealed class ControlAnomalyService : IControlAnomalyService
{
    /// <summary>Khoảng ngày tối đa mỗi lần rà (1 năm) – như báo cáo đối chiếu MATCH3.</summary>
    public const int MaxDays = 366;

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext đã gắn phạm vi người dùng.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="clock">Đồng hồ (hạn ghi nhận chiết khấu/tài trợ so với hôm nay).</param>
    public ControlAnomalyService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AnomalyResult> GetAsync(AnomalyQuery query, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (query.From > query.To) throw new BusinessRuleException("Từ ngày phải trước hoặc bằng đến ngày.");
        if (query.To.DayNumber - query.From.DayNumber > MaxDays) throw new BusinessRuleException("Khoảng ngày tối đa 1 năm.");
        if (query.PriceGapPct is <= 0 or > 1000) throw new BusinessRuleException("Mức chênh giá phải lớn hơn 0 và không quá 1.000%.");
        if (query.NearThresholdPct is <= 0 or >= 100) throw new BusinessRuleException("Mức sát ngưỡng phải lớn hơn 0 và nhỏ hơn 100%.");
        if (query.NearThresholdCount is < 2 or > 100) throw new BusinessRuleException("Số phiếu sát ngưỡng phải từ 2 đến 100.");
        if (query.OffContractPct is < 0 or >= 100) throw new BusinessRuleException("Tỷ lệ mua ngoài hợp đồng phải từ 0 đến dưới 100%.");
        if (query.AllCompanies) _scope.EnsurePermission(Permissions.Report.PurchaseViewAllCompanies);

        var companies = await _db.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var lines = await PurchaseReportService.OrderLinesAsync(_db, query.From, query.To, query.AllCompanies, null, cancellationToken);
        var items = new List<AnomalyDto>();
        items.AddRange(HighPriceRepeat(lines, query.PriceGapPct));
        items.AddRange(await NearThresholdAsync(query, companies, cancellationToken));
        items.AddRange(await BankAccountChangesAsync(query, companies, cancellationToken));
        items.AddRange(OffContract(lines, query.OffContractPct));
        items.AddRange(await ReceiptIssuesAsync(query, companies, cancellationToken));
        items.AddRange(await ConflictsAsync(query, companies, cancellationToken));
        items.AddRange(await UnrecordedRebatesAsync(query, companies, cancellationToken));

        // Họ tên người liên quan: dịch vụ ghi Id người dùng vào UserName, đổi sang họ tên một lần ở đây.
        var userIds = items.Select(i => Guid.TryParse(i.UserName, out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var result = items
            .Select(i => Guid.TryParse(i.UserName, out var id) ? i with { UserName = names.GetValueOrDefault(id) } : i)
            .OrderBy(i => i.Severity == AnomalySeverities.High ? 0 : 1).ThenByDescending(i => i.Date).ThenBy(i => i.Code).ThenBy(i => i.RefNo)
            .ToList();

        var summary = new List<AnomalySummaryDto>
        {
            Summary(AnomalyCodes.HighPriceRepeat, $"NCC được mua từ 2 PO trở lên có giá BQ cao hơn NCC rẻ nhất khác cùng mặt hàng từ {Pct(query.PriceGapPct)}."),
            Summary(AnomalyCodes.NearThreshold,
                $"Từ {query.NearThresholdCount} PO cùng người lập × NCC có giá trị từ {Pct(100 - query.NearThresholdPct)} đến dưới ngưỡng một cấp duyệt PO."),
            Summary(AnomalyCodes.BankAccountChange, "Yêu cầu đổi tài khoản ngân hàng NCC được duyệt trong kỳ."),
            Summary(AnomalyCodes.OffContract, $"Người lập PO có giá trị mua ngoài giá/hợp đồng đã duyệt trên {Pct(query.OffContractPct)} (như báo cáo PRCW)."),
            Summary(AnomalyCodes.InvoiceShortage, "Hóa đơn cao hơn tiền thực nhận dù có hàng nhận thiếu/không đạt (như báo cáo MATCH3, dung sai 0 đ)."),
            Summary(AnomalyCodes.BackdatedOrder, "Hàng nhận hoặc hóa đơn có trước ngày PO, hàng nhận trước khi PO được duyệt."),
            Summary(AnomalyCodes.ConflictOfInterest, "Người lập hoặc người duyệt PO có kê khai quan hệ lợi ích với NCC còn hiệu lực vào ngày PO."),
            Summary(AnomalyCodes.UnrecordedRebate,
                $"Kỳ quyết toán chiết khấu/tài trợ của hợp đồng nguyên tắc quá hạn ghi nhận (cuối kỳ + {ContractRebateTracker.GraceDays} ngày) trong khoảng xem mà thực nhận ít hơn phải nhận."),
        };
        return new AnomalyResult(query.From, query.To, query.AllCompanies, summary, result);

        AnomalySummaryDto Summary(string code, string note) => new(code, result.Count(i => i.Code == code), true, note);
    }

    /// <summary>
    /// Chiết khấu/tài trợ chưa ghi nhận (P10): kỳ quyết toán của cam kết trên hợp đồng nguyên tắc có hạn ghi nhận (cuối kỳ + 30 ngày) nằm
    /// trong khoảng xem và đã qua, mà thực nhận ít hơn phải nhận – cùng số với tab Chiết khấu – tài trợ của hợp đồng (mục 18.21). Chưa ghi
    /// nhận đồng nào → Cao; nhận thiếu → Trung bình. Tiền = còn thiếu. Ví dụ quý 3 phải nhận 25.000.000, đã nhận 10.000.000, hạn 30/10 →
    /// dấu hiệu Trung bình, thiếu 15.000.000.
    /// </summary>
    private async Task<IEnumerable<AnomalyDto>> UnrecordedRebatesAsync(
        AnomalyQuery query, Dictionary<Guid, string> companies, CancellationToken cancellationToken)
    {
        var today = _clock.TodayInVietnam();
        // Kỳ có hạn trong khoảng xem thì cuối kỳ ≥ From − hạn; hợp đồng kết thúc trước đó không cần nạp.
        var earliestEnd = query.From.AddDays(-ContractRebateTracker.GraceDays);
        var source = query.AllCompanies ? _db.PurchaseContracts.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.PurchaseContracts;
        var contracts = await source.AsNoTracking().Include(c => c.Rebates)
            .Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Terminated) && c.Rebates.Any()
                        && (c.TerminatedOn ?? c.ValidTo) >= earliestEnd && c.ValidFrom <= query.To)
            .ToListAsync(cancellationToken);
        var rows = await ContractRebateTracker.TrackAsync(_db, contracts, query.AllCompanies, today, cancellationToken);
        var supplierIds = contracts.Select(c => c.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken);
        return rows
            .Where(r => r.Status == "Short" && r.Due >= query.From && r.Due <= query.To)
            .Select(r =>
            {
                var supplier = suppliers.GetValueOrDefault(r.Contract.SupplierId);
                return new AnomalyDto(
                    AnomalyCodes.UnrecordedRebate, r.Received == 0 ? AnomalySeverities.High : AnomalySeverities.Medium, r.Contract.CompanyId,
                    companies.GetValueOrDefault(r.Contract.CompanyId, string.Empty), r.Due, "CTR", r.Contract.Id, r.Contract.No, supplier.Code,
                    supplier.Name, r.Contract.CreatedBy?.ToString(), r.Short,
                    $"{r.Rebate.Description} – kỳ {r.From:dd/MM/yyyy}–{r.To:dd/MM/yyyy}: doanh số {Money(r.Purchase)}, phải nhận {Money(r.Expected)}, " +
                    $"đã ghi nhận {Money(r.Received)}, hạn ghi nhận {r.Due:dd/MM/yyyy}.");
            })
            .ToList();
    }

    /// <summary>
    /// Mua lặp lại một NCC giá cao: theo công ty × NCC × mặt hàng có từ 2 PO, giá BQ theo ĐVT cơ sở = Σ thành tiền ÷ Σ SL cơ sở, so với giá
    /// BQ thấp nhất của NCC khác cùng mặt hàng trong phạm vi đang xem. Ví dụ NCC A 2 PO BQ 55.000, NCC B 50.000 → cao hơn 10,00%,
    /// tiền chênh = (55.000 − 50.000) × SL của A. Chênh từ gấp đôi mức cho phép thì mức Cao.
    /// </summary>
    private static IEnumerable<AnomalyDto> HighPriceRepeat(List<PurchaseReportService.OrderLine> lines, decimal gapPct)
    {
        var bySupplier = lines.Where(l => l.BaseQty > 0)
            .GroupBy(l => (l.CompanyId, l.SupplierId, l.ItemId))
            .Select(g => new
            {
                g.Key.CompanyId, g.Key.SupplierId, g.Key.ItemId, Lines = g.ToList(), Orders = g.Select(l => l.OrderId).Distinct().Count(),
                Qty = g.Sum(l => l.BaseQty), Avg = g.Sum(l => l.Amount) / g.Sum(l => l.BaseQty),
            })
            .ToList();
        foreach (var s in bySupplier.Where(s => s.Orders >= 2))
        {
            var others = bySupplier.Where(o => o.ItemId == s.ItemId && o.SupplierId != s.SupplierId).ToList();
            if (others.Count == 0) continue;
            var cheapest = others.MinBy(o => o.Avg)!;
            if (cheapest.Avg <= 0) continue;
            var gap = Math.Round((s.Avg - cheapest.Avg) * 100m / cheapest.Avg, 2, MidpointRounding.AwayFromZero);
            if (gap < gapPct) continue;
            var last = s.Lines.MaxBy(l => (l.Date, l.OrderNo))!;
            var other = cheapest.Lines[0];
            yield return new AnomalyDto(
                AnomalyCodes.HighPriceRepeat, gap >= 2 * gapPct ? AnomalySeverities.High : AnomalySeverities.Medium, s.CompanyId, last.CompanyCode,
                last.Date, "PO", last.OrderId, last.OrderNo, last.SupplierCode, last.SupplierName, last.CreatedBy?.ToString(),
                Math.Round((s.Avg - cheapest.Avg) * s.Qty, 2, MidpointRounding.AwayFromZero),
                $"{last.ItemCode} – {last.ItemName}: {s.Orders} PO, giá BQ {Money(s.Avg)}/{last.BaseUomCode} cao hơn NCC {other.SupplierCode} " +
                $"({Money(cheapest.Avg)}) {Pct(gap)}.");
        }
    }

    /// <summary>
    /// Nhiều PO sát dưới ngưỡng duyệt (V5 – chia nhỏ đơn để né cấp duyệt cao): PO đã gửi duyệt trở đi (không tính nháp, hủy), giá trị xét
    /// duyệt = tổng thanh toán gồm VAT, nằm trong [ngưỡng × (100 − p)%, ngưỡng) của một cấp duyệt PO có ngưỡng > 0 tại công ty. Gom theo công
    /// ty × người lập × NCC × ngưỡng; từ N phiếu trở lên là dấu hiệu (mức Cao khi gấp đôi N). Ví dụ ngưỡng 50.000.000, p = 10%: PO
    /// 46.000.000, 48.500.000, 49.900.000 của cùng người lập, cùng NCC → 3 phiếu sát ngưỡng.
    /// </summary>
    private async Task<IEnumerable<AnomalyDto>> NearThresholdAsync(
        AnomalyQuery query, Dictionary<Guid, string> companies, CancellationToken cancellationToken)
    {
        var rules = query.AllCompanies ? _db.ApprovalRules.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.ApprovalRules;
        var thresholds = (await rules.AsNoTracking()
                .Where(r => r.DocType == DocumentTypes.PurchaseOrder && r.MinAmount > 0)
                .Select(r => new { r.CompanyId, r.MinAmount })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.MinAmount).Distinct().Order().ToList());
        if (thresholds.Count == 0) return [];

        var orders = await Orders(query.AllCompanies)
            .Where(o => o.Status != ApprovalDocumentStatus.Draft && o.Status != ApprovalDocumentStatus.Cancelled
                        && o.Date >= query.From && o.Date <= query.To)
            .Join(_db.Suppliers, o => o.SupplierId, s => s.Id, (o, s) => new
            {
                o.Id, o.CompanyId, o.No, o.Date, o.CreatedBy, o.SupplierId, SupplierCode = s.Code, SupplierName = s.Name,
                Amount = o.TotalAmount + o.TotalVat,
            })
            .ToListAsync(cancellationToken);
        var factor = (100m - query.NearThresholdPct) / 100m;
        return orders
            .Select(o => new
            {
                Order = o,
                Threshold = thresholds.GetValueOrDefault(o.CompanyId)?.FirstOrDefault(t => o.Amount >= t * factor && o.Amount < t),
            })
            .Where(x => x.Threshold is > 0)
            .GroupBy(x => (x.Order.CompanyId, x.Order.CreatedBy, x.Order.SupplierId, Threshold: x.Threshold!.Value))
            .Where(g => g.Count() >= query.NearThresholdCount)
            .Select(g =>
            {
                var list = g.Select(x => x.Order).OrderBy(o => o.Date).ThenBy(o => o.No).ToList();
                var last = list[^1];
                return new AnomalyDto(
                    AnomalyCodes.NearThreshold, list.Count >= 2 * query.NearThresholdCount ? AnomalySeverities.High : AnomalySeverities.Medium,
                    g.Key.CompanyId, companies.GetValueOrDefault(g.Key.CompanyId, string.Empty), last.Date, "PO", last.Id, last.No, last.SupplierCode,
                    last.SupplierName, g.Key.CreatedBy?.ToString(), list.Sum(o => o.Amount),
                    $"{list.Count} PO sát dưới ngưỡng duyệt {Money(g.Key.Threshold)}: {string.Join(", ", list.Select(o => $"{o.No} ({Money(o.Amount)})"))}.");
            })
            .ToList();
    }

    /// <summary>Đổi tài khoản ngân hàng NCC được duyệt trong kỳ (N2): KSNB đối chiếu với chứng từ thanh toán ở phần mềm kế toán.</summary>
    private async Task<IEnumerable<AnomalyDto>> BankAccountChangesAsync(
        AnomalyQuery query, Dictionary<Guid, string> companies, CancellationToken cancellationToken)
    {
        // ApprovedAt lưu UTC: đổi khoảng ngày Việt Nam sang khoảng UTC để so sánh trong CSDL.
        var start = VietnamStart(query.From);
        var end = VietnamStart(query.To.AddDays(1));
        var requests = query.AllCompanies
            ? _db.SupplierChangeRequests.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            : _db.SupplierChangeRequests;
        var rows = await requests.AsNoTracking()
            .Where(r => r.Kind == SupplierChangeKind.BankAccounts && r.Status == SupplierChangeRequestStatus.Approved
                        && r.ApprovedAt >= start && r.ApprovedAt < end)
            .Join(_db.Suppliers, r => r.SupplierId, s => s.Id, (r, s) => new
            {
                r.Id, r.CompanyId, r.No, r.ApprovedAt, r.CreatedBy, r.CurrentSummary, r.ProposedSummary, SupplierCode = s.Code, SupplierName = s.Name,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new AnomalyDto(
            AnomalyCodes.BankAccountChange, AnomalySeverities.Medium, r.CompanyId, companies.GetValueOrDefault(r.CompanyId, string.Empty),
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.ApprovedAt!.Value, VietnamTime.Zone).DateTime), "SUPAPR", r.Id, r.No, r.SupplierCode,
            r.SupplierName, r.CreatedBy?.ToString(), null, $"Tài khoản cũ: {Blank(r.CurrentSummary)} → mới: {Blank(r.ProposedSummary)}."));
    }

    /// <summary>
    /// Tỷ lệ mua ngoài hợp đồng theo người lập PO (P5), cùng cách tính báo cáo PRCW (mục 18.17): ngoài = dòng lệch giá + dòng không có giá
    /// đã duyệt; mẫu số bỏ dòng không xác định nguồn (PO trước 0.12.0). Ví dụ người lập có 1.269.000 đã biết nguồn, ngoài 794.000 → 62,57%.
    /// Vượt gấp đôi mức cho phép thì mức Cao.
    /// </summary>
    private static IEnumerable<AnomalyDto> OffContract(List<PurchaseReportService.OrderLine> lines, decimal limitPct)
    {
        foreach (var g in lines.Where(l => l.Kind != "Unknown").GroupBy(l => (l.CompanyId, l.CreatedBy)))
        {
            var known = g.Sum(l => l.Amount);
            var off = g.Where(l => l.Kind is "Deviation" or "None").Sum(l => l.Amount);
            if (known <= 0 || off <= 0) continue;
            var pct = Math.Round(off * 100m / known, 2, MidpointRounding.AwayFromZero);
            if (pct <= limitPct) continue;
            var last = g.MaxBy(l => (l.Date, l.OrderNo))!;
            var orders = g.Where(l => l.Kind is "Deviation" or "None").Select(l => l.OrderNo).Distinct().Order().ToList();
            yield return new AnomalyDto(
                AnomalyCodes.OffContract, pct >= 2 * limitPct ? AnomalySeverities.High : AnomalySeverities.Medium,
                g.Key.CompanyId, last.CompanyCode, last.Date, "PRCW", null, null, null, null, g.Key.CreatedBy?.ToString(), off,
                $"Mua ngoài giá/hợp đồng đã duyệt {Money(off)} / {Money(known)} = {Pct(pct)} ({orders.Count} PO: {string.Join(", ", orders.Take(10))}" +
                $"{(orders.Count > 10 ? ", …" : string.Empty)}).");
        }
    }

    /// <summary>
    /// Dấu hiệu từ đối chiếu PO – nhận – hóa đơn (MATCH3, mục 18.15, dung sai 0 đ): hóa đơn tính cả hàng thiếu/không đạt (mức Cao); hàng nhận
    /// hoặc hóa đơn trước ngày PO (Cao – hợp thức hóa); hàng nhận trước ngày PO được duyệt (Trung bình).
    /// </summary>
    private async Task<IEnumerable<AnomalyDto>> ReceiptIssuesAsync(
        AnomalyQuery query, Dictionary<Guid, string> companies, CancellationToken cancellationToken)
    {
        var (rows, _) = await PurchaseReportService.ThreeWayRowsAsync(_db, query.From, query.To, 0m, query.AllCompanies, cancellationToken);
        var creators = await DocumentCreatorsAsync(rows.Select(r => r.Row.DocumentId).ToList(), query.AllCompanies, cancellationToken);
        var result = new List<AnomalyDto>();
        foreach (var (companyId, r) in rows)
        {
            AnomalyDto Make(string code, string severity, decimal? amount, string detail) => new(
                code, severity, companyId, companies.GetValueOrDefault(companyId, string.Empty), r.Date, "GRN", r.DocumentId, r.DocumentNo,
                r.SupplierCode, r.SupplierName, creators.GetValueOrDefault(r.DocumentId)?.ToString(), amount, detail);

            if (r.Issues.Contains(ThreeWayMatchIssues.InvoiceIncludesShortage))
                result.Add(Make(AnomalyCodes.InvoiceShortage, AnomalySeverities.High, r.Difference,
                    $"Hóa đơn {r.InvoiceNo} {Money(r.InvoiceAmount ?? 0)} cao hơn tiền thực nhận {Money(r.ReceiptTotal)} dù có hàng nhận thiếu/không đạt."));
            var reasons = new List<string>();
            if (r.Issues.Contains(ThreeWayMatchIssues.ReceivedBeforeOrder)) reasons.Add($"nhận hàng {r.Date:dd/MM/yyyy} trước ngày PO {r.PurchaseOrderDate:dd/MM/yyyy}");
            if (r.Issues.Contains(ThreeWayMatchIssues.InvoiceBeforeOrder)) reasons.Add($"hóa đơn {r.InvoiceDate:dd/MM/yyyy} trước ngày PO {r.PurchaseOrderDate:dd/MM/yyyy}");
            var beforeApproval = r.Issues.Contains(ThreeWayMatchIssues.ReceivedBeforeApproval);
            if (beforeApproval) reasons.Add($"nhận hàng {r.Date:dd/MM/yyyy} trước ngày duyệt PO {r.PurchaseOrderApprovedDate:dd/MM/yyyy}");
            if (reasons.Count > 0)
                result.Add(Make(AnomalyCodes.BackdatedOrder, reasons.Count == 1 && beforeApproval ? AnomalySeverities.Medium : AnomalySeverities.High,
                    r.ReceiptTotal, $"PO {r.PurchaseOrderNo}: {string.Join("; ", reasons)}."));
        }

        return result;
    }

    /// <summary>
    /// PO có người lập hoặc người đã duyệt (mọi chặng) có kê khai quan hệ lợi ích với NCC còn hiệu lực vào ngày PO (N4, mục 18.8). Chặn duyệt
    /// chỉ áp từ lúc kê khai; dấu hiệu này bắt cả trường hợp kê khai sau với ngày hiệu lực lùi về trước, và người lập (không bị chặn lập PO).
    /// </summary>
    private async Task<IEnumerable<AnomalyDto>> ConflictsAsync(
        AnomalyQuery query, Dictionary<Guid, string> companies, CancellationToken cancellationToken)
    {
        var orders = await Orders(query.AllCompanies)
            .Where(o => o.Status != ApprovalDocumentStatus.Draft && o.Status != ApprovalDocumentStatus.Cancelled
                        && o.Date >= query.From && o.Date <= query.To)
            .Join(_db.Suppliers, o => o.SupplierId, s => s.Id, (o, s) => new
            {
                o.Id, o.CompanyId, o.No, o.Date, o.CreatedBy, o.SupplierId, SupplierCode = s.Code, SupplierName = s.Name,
                Amount = o.TotalAmount + o.TotalVat,
            })
            .ToListAsync(cancellationToken);
        if (orders.Count == 0) return [];
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        // Kê khai không thuộc công ty nào (gắn với con người) nên không có filter công ty.
        var declarations = await _db.ConflictDeclarations.AsNoTracking().Where(d => supplierIds.Contains(d.SupplierId)).ToListAsync(cancellationToken);
        if (declarations.Count == 0) return [];
        var orderIds = orders.Select(o => o.Id).ToList();
        var steps = query.AllCompanies ? _db.ApprovalSteps.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.ApprovalSteps;
        var approvers = (await steps.AsNoTracking()
                .Where(s => orderIds.Contains(s.DocumentId) && s.Status == ApprovalStepStatus.Approved && s.ActedBy != null)
                .Select(s => new { s.DocumentId, s.ActedBy })
                .ToListAsync(cancellationToken))
            .ToLookup(s => s.DocumentId, s => s.ActedBy!.Value);

        var result = new List<AnomalyDto>();
        foreach (var o in orders)
        {
            var people = new List<(Guid User, string Role)>();
            if (o.CreatedBy is { } creator) people.Add((creator, "lập"));
            people.AddRange(approvers[o.Id].Distinct().Select(a => (a, "duyệt")));
            foreach (var (user, role) in people)
            {
                var declaration = declarations.FirstOrDefault(d => d.UserId == user && d.SupplierId == o.SupplierId && d.IsActiveOn(o.Date));
                if (declaration is null) continue;
                result.Add(new AnomalyDto(
                    AnomalyCodes.ConflictOfInterest, AnomalySeverities.High, o.CompanyId, companies.GetValueOrDefault(o.CompanyId, string.Empty), o.Date,
                    "PO", o.Id, o.No, o.SupplierCode, o.SupplierName, user.ToString(), o.Amount,
                    $"Người {role} PO có kê khai quan hệ lợi ích với NCC từ {declaration.ValidFrom:dd/MM/yyyy}: {declaration.Description}"));
            }
        }

        return result;
    }

    /// <summary>Người lập phiếu nhập (để KSNB biết ai nhận hàng).</summary>
    private async Task<Dictionary<Guid, Guid?>> DocumentCreatorsAsync(List<Guid> documentIds, bool allCompanies, CancellationToken cancellationToken)
    {
        var documents = allCompanies
            ? _db.InventoryDocuments.IgnoreQueryFilters([ProcDbContext.CompanyFilter, ProcDbContext.WarehouseFilter])
            : _db.InventoryDocuments;
        return await documents.AsNoTracking().Where(d => documentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.CreatedBy, cancellationToken);
    }

    /// <summary>Đơn mua hàng theo phạm vi xem (toàn tập đoàn đã kiểm quyền ở GetAsync).</summary>
    private IQueryable<PurchaseOrder> Orders(bool allCompanies) =>
        (allCompanies ? _db.PurchaseOrders.IgnoreQueryFilters([ProcDbContext.CompanyFilter]) : _db.PurchaseOrders).AsNoTracking();

    /// <summary>Đầu ngày (00:00 giờ Việt Nam) của một ngày, đổi sang UTC để so với cột UTC (Npgsql chỉ nhận offset 0).</summary>
    private static DateTimeOffset VietnamStart(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, VietnamTime.Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static string Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", Vi);

    private static string Pct(decimal value) => value.ToString("0.##", Vi) + "%";

    private static string Blank(string text) => string.IsNullOrWhiteSpace(text) ? "(không có)" : text;
}
