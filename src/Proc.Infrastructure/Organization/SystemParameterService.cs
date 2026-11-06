using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Tham số hệ thống tại công ty đang làm việc (design.md mục 5.4). Dòng <see cref="CompanyParameter"/> đọc qua global query
/// filter (chỉ công ty đang làm việc); công ty chưa có dòng thì dùng giá trị mặc định, lần sửa đầu tiên mới tạo dòng.
/// </summary>
public sealed class SystemParameterService : ISystemParameterService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ tham số.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi (công ty đang làm việc).</param>
    public SystemParameterService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemParameterDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var parameter = await _db.CompanyParameters.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var rfqMin = parameter?.RfqMinSuppliers ?? CompanyParameter.DefaultRfqMinSuppliers;
        var markup = parameter?.InternalMarkupPercent ?? CompanyParameter.DefaultInternalMarkupPercent;
        var warn = parameter?.PoHighPriceWarnPercent ?? CompanyParameter.DefaultPoHighPriceWarnPercent;

        // Chỉ thấy kho trong phạm vi kho được gán (global query filter) – đúng với những gì người dùng sửa được ở màn hình Kho.
        var negative = await _db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive && w.AllowNegativeStock)
            .OrderBy(w => w.Code)
            .Select(w => w.Code)
            .ToListAsync(cancellationToken);

        var defaultText = CompanyParameter.DefaultRfqMinSuppliers.ToString(CultureInfo.InvariantCulture);
        List<SystemParameterDto> list =
        [
            new(SystemParameterCodes.RfqMinSuppliers, "Đấu thầu", "Số NCC tối thiểu khi gửi RFQ",
                rfqMin.ToString(CultureInfo.InvariantCulture), defaultText, rfqMin == CompanyParameter.DefaultRfqMinSuppliers, true,
                $"Phải mời đủ số NCC này mới gửi được yêu cầu báo giá (1–{CompanyParameter.MaxRfqMinSuppliers}). RFQ đã gửi không bị ảnh hưởng."),
            Toggle(SystemParameterCodes.RfqDossierRequired, "Bắt buộc đủ hồ sơ trình khi chọn thầu", parameter?.RequireRfqDossierComplete ?? false,
                "Bật: hồ sơ trình của RFQ (7 mục) phải đủ mới chọn thầu, gửi hội đồng duyệt được. Tắt: thiếu mục vẫn gửi, hộp thoại duyệt ghi mục còn thiếu.", "Đấu thầu"),
            new(SystemParameterCodes.InternalMarkupPercent, "Giá", "% markup nội bộ mặc định",
                FormatPercent(markup), FormatPercent(CompanyParameter.DefaultInternalMarkupPercent),
                markup == CompanyParameter.DefaultInternalMarkupPercent, true,
                $"Mặt hàng chưa có trong Bảng giá nội bộ thì gợi ý giá bán = giá vốn kỳ đã đóng gần nhất × (1 + % markup), làm tròn đồng " +
                $"(0–{CompanyParameter.MaxInternalMarkupPercent:0}%). Chỉ là gợi ý ở Bảng giá nội bộ và phiếu xuất bán nội bộ; giá đã khai báo không bị ảnh hưởng."),
            new(SystemParameterCodes.PoHighPriceWarnPercent, "Giá", "% cảnh báo giá cao trên PO",
                FormatPercent(warn), FormatPercent(CompanyParameter.DefaultPoHighPriceWarnPercent),
                warn == CompanyParameter.DefaultPoHighPriceWarnPercent, true,
                "Dòng PO có giá sau chiết khấu (theo ĐVT cơ sở) cao hơn giá thấp nhất tập đoàn quá mức này thì hiện cảnh báo, không chặn. " +
                $"Giá thấp nhất tập đoàn = thấp nhất trong các giá mua gần nhất 12 tháng của từng công ty × NCC, như màn hình So sánh giá (0–{CompanyParameter.MaxPoHighPriceWarnPercent:0}%)."),
            Toggle(SystemParameterCodes.ReceiptQcRequired, "Bắt buộc kiểm tra chất lượng khi nhận hàng", parameter?.RequireReceiptQc ?? false,
                "Bật: mọi dòng phiếu nhập mua phải có kết quả kiểm tra (Đạt/Không đạt) mới ghi sổ được."),
            Toggle(SystemParameterCodes.ReceiptSeparatePoCreator, "Người lập PO không ghi sổ nhận hàng của PO đó", parameter?.SeparatePoCreatorFromReceipt ?? false,
                "Bật: người lập đơn mua hàng không ghi sổ được phiếu nhập theo đơn của mình – người khác xác nhận giao nhận (tách nhiệm)."),
            Toggle(SystemParameterCodes.ReceiptPoRequired, "Nhập mua bắt buộc theo đơn mua hàng", parameter?.RequireReceiptPo ?? false,
                "Bật: phiếu nhập mua từ NCC phải chọn đơn mua hàng đã duyệt (giá, SL theo PO) – không nhập thẳng với đơn giá gõ tay. Mua gấp dùng PO mua khẩn cấp."),
            Sla(SystemParameterCodes.SlaPricedDays, "SLA – hàng đã có hợp đồng/bảng giá", parameter?.SlaPricedDays, CompanyParameter.DefaultSlaPricedDays,
                "Số ngày làm việc từ lúc PR đủ duyệt đến lúc PO được duyệt cho hàng đã có bảng giá/hợp đồng hiệu lực."),
            Sla(SystemParameterCodes.SlaSourcingDays, "SLA – hàng chưa có NCC được duyệt", parameter?.SlaSourcingDays, CompanyParameter.DefaultSlaSourcingDays,
                "Số ngày làm việc để tìm nguồn, so sánh, trình duyệt và phát hành PO cho hàng chưa có bảng giá/hợp đồng (kể cả hàng mới chưa có mã)."),
            Sla(SystemParameterCodes.SlaUrgentDays, "SLA – hàng khẩn", parameter?.SlaUrgentDays, CompanyParameter.DefaultSlaUrgentDays,
                "PR mức ưu tiên Khẩn: số ngày làm việc tới lúc PO được duyệt (0 = trong ngày)."),
            new(SystemParameterCodes.CostingMethod, "Kho", "Phương pháp giá vốn",
                "Bình quân gia quyền cuối kỳ tháng", null, true, false,
                "Tính theo công ty × mặt hàng khi đóng kỳ; cố định cho mọi công ty (design.md mục 7)."),
            new(SystemParameterCodes.NegativeStock, "Kho", "Kho cho phép tồn âm",
                negative.Count == 0 ? "Không kho nào" : string.Join(", ", negative), null, negative.Count == 0, false,
                "Khai báo trên từng kho ở màn hình Danh mục kho; mặc định không cho tồn âm."),
        ];
        list.AddRange(KpiParameters(parameter));
        // F1 trên ô giá trị: tham số lưu ở cột nào của dòng tham số công ty (design.md mục 9.3a).
        return list.Select(p => AuditFields.TryGetValue(p.Code, out var field) ? p with { AuditId = parameter?.Id, AuditField = field } : p).ToList();
    }

    /// <summary>Cột của <see cref="CompanyParameter"/> lưu từng tham số sửa được.</summary>
    private static readonly Dictionary<string, string> AuditFields = new()
    {
        [SystemParameterCodes.RfqMinSuppliers] = nameof(CompanyParameter.RfqMinSuppliers),
        [SystemParameterCodes.InternalMarkupPercent] = nameof(CompanyParameter.InternalMarkupPercent),
        [SystemParameterCodes.PoHighPriceWarnPercent] = nameof(CompanyParameter.PoHighPriceWarnPercent),
        [SystemParameterCodes.RfqDossierRequired] = nameof(CompanyParameter.RequireRfqDossierComplete),
        [SystemParameterCodes.ReceiptQcRequired] = nameof(CompanyParameter.RequireReceiptQc),
        [SystemParameterCodes.ReceiptSeparatePoCreator] = nameof(CompanyParameter.SeparatePoCreatorFromReceipt),
        [SystemParameterCodes.ReceiptPoRequired] = nameof(CompanyParameter.RequireReceiptPo),
        [SystemParameterCodes.SlaPricedDays] = nameof(CompanyParameter.SlaPricedDays),
        [SystemParameterCodes.SlaSourcingDays] = nameof(CompanyParameter.SlaSourcingDays),
        [SystemParameterCodes.SlaUrgentDays] = nameof(CompanyParameter.SlaUrgentDays),
        [SystemParameterCodes.KpiTarget(KpiCode.Saving)] = nameof(CompanyParameter.KpiSavingTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.ContractCompliance)] = nameof(CompanyParameter.KpiContractTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.PurchaseSla)] = nameof(CompanyParameter.KpiSlaTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.OnTimeDelivery)] = nameof(CompanyParameter.KpiOnTimeTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Quality)] = nameof(CompanyParameter.KpiQualityTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Emergency)] = nameof(CompanyParameter.KpiEmergencyTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.SlowMoving)] = nameof(CompanyParameter.KpiSlowMovingTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.SupplierReview)] = nameof(CompanyParameter.KpiSupplierReviewTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Violation)] = nameof(CompanyParameter.KpiViolationTarget),
        [SystemParameterCodes.KpiSlowMovingDays] = nameof(CompanyParameter.KpiSlowMovingDays),
    };

    /// <summary>
    /// Tham số nhóm "KPI": mục tiêu 9 chỉ số và số ngày tính tồn chậm (design.md mục 18.18). Chỉ số cao là tốt ghi "≥", thấp là tốt ghi "≤".
    /// </summary>
    private static IEnumerable<SystemParameterDto> KpiParameters(CompanyParameter? parameter)
    {
        foreach (var k in KpiCatalog.All)
        {
            var value = parameter?.KpiTarget(k.Code) ?? k.DefaultTarget;
            var sign = k.HigherIsBetter ? "≥" : "≤";
            var range = k.IsCount ? $"số nguyên 0–{KpiCatalog.MaxViolationTarget}" : "0–100%";
            yield return new(SystemParameterCodes.KpiTarget(k.Code), "KPI", $"Mục tiêu KPI – {k.Name} ({sign})", FormatPercent(value),
                FormatPercent(k.DefaultTarget), value == k.DefaultTarget, true, $"Cách đo: {k.Formula}. Đạt khi kết quả {sign} mục tiêu; {range}.");
        }

        var days = parameter?.KpiSlowMovingDays ?? KpiCatalog.DefaultSlowMovingDays;
        yield return new(SystemParameterCodes.KpiSlowMovingDays, "KPI", "Số ngày không xuất tính tồn chậm luân chuyển",
            days.ToString(CultureInfo.InvariantCulture), KpiCatalog.DefaultSlowMovingDays.ToString(CultureInfo.InvariantCulture),
            days == KpiCatalog.DefaultSlowMovingDays, true, $"Mặt hàng còn tồn mà không xuất từ số ngày này trở lên; 1–{KpiCatalog.MaxSlowMovingDays}.");
    }

    /// <inheritdoc />
    public async Task<SystemParameterDto> UpdateAsync(string code, UpdateSystemParameterRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var key = code.Trim().ToUpperInvariant();
        switch (key)
        {
            case SystemParameterCodes.RfqMinSuppliers:
                int? value = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    if (!int.TryParse(request.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                        throw new BusinessRuleException("Số NCC tối thiểu phải là số nguyên.");
                    value = parsed;
                }

                (await GetOrCreateAsync(companyId, cancellationToken)).ChangeRfqMinSuppliers(value);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.InternalMarkupPercent:
            case SystemParameterCodes.PoHighPriceWarnPercent:
                decimal? percent = null;
                var isMarkup = key == SystemParameterCodes.InternalMarkupPercent;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    // Nhận cả "12,5" (kiểu Việt Nam, CLAUDE.md mục 5) lẫn "12.5" (ô số trên giao diện gửi lên); không nhận dấu nhóm nghìn.
                    var text = request.Value.Trim().Replace(',', '.');
                    if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                        throw new BusinessRuleException($"{(isMarkup ? "% markup nội bộ" : "% cảnh báo giá cao")} phải là số, ví dụ 10 hoặc 12,5.");
                    percent = parsed;
                }

                var parameterRow = await GetOrCreateAsync(companyId, cancellationToken);
                if (isMarkup) parameterRow.ChangeInternalMarkupPercent(percent);
                else parameterRow.ChangePoHighPriceWarnPercent(percent);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.ReceiptQcRequired:
            case SystemParameterCodes.ReceiptSeparatePoCreator:
            case SystemParameterCodes.ReceiptPoRequired:
            case SystemParameterCodes.RfqDossierRequired:
                var on = ParseToggle(request.Value);
                var row = await GetOrCreateAsync(companyId, cancellationToken);
                if (key == SystemParameterCodes.ReceiptQcRequired) row.ChangeRequireReceiptQc(on);
                else if (key == SystemParameterCodes.RfqDossierRequired) row.ChangeRequireRfqDossierComplete(on);
                else if (key == SystemParameterCodes.ReceiptPoRequired) row.ChangeRequireReceiptPo(on);
                else row.ChangeSeparatePoCreatorFromReceipt(on);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.SlaPricedDays:
            case SystemParameterCodes.SlaSourcingDays:
            case SystemParameterCodes.SlaUrgentDays:
                int? days = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    if (!int.TryParse(request.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedDays))
                        throw new BusinessRuleException("SLA phải là số ngày làm việc (số nguyên).");
                    days = parsedDays;
                }

                var category = key switch
                {
                    SystemParameterCodes.SlaPricedDays => Proc.Domain.Purchasing.SlaCategory.Priced,
                    SystemParameterCodes.SlaSourcingDays => Proc.Domain.Purchasing.SlaCategory.Sourcing,
                    _ => Proc.Domain.Purchasing.SlaCategory.Urgent,
                };
                (await GetOrCreateAsync(companyId, cancellationToken)).ChangeSlaDays(category, days);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.CostingMethod:
            case SystemParameterCodes.NegativeStock:
                throw new BusinessRuleException("Tham số này chỉ xem, không sửa ở màn hình Tham số hệ thống.");
            case SystemParameterCodes.KpiSlowMovingDays:
                int? idleDays = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    if (!int.TryParse(request.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIdle))
                        throw new BusinessRuleException("Số ngày phải là số nguyên.");
                    idleDays = parsedIdle;
                }

                (await GetOrCreateAsync(companyId, cancellationToken)).ChangeKpiSlowMovingDays(idleDays);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            default:
                var kpi = KpiCatalog.All.FirstOrDefault(k => SystemParameterCodes.KpiTarget(k.Code) == key)
                    ?? throw new NotFoundException($"Không có tham số {key}.");
                decimal? target = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    // Nhận "97,5" lẫn "97.5" như % markup nội bộ.
                    var text = request.Value.Trim().Replace(',', '.');
                    if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedTarget))
                        throw new BusinessRuleException("Mục tiêu KPI phải là số, ví dụ 95 hoặc 97,5.");
                    target = parsedTarget;
                }

                (await GetOrCreateAsync(companyId, cancellationToken)).ChangeKpiTarget(kpi.Code, target);
                await _db.SaveChangesAsync(cancellationToken);
                break;
        }

        return (await ListAsync(cancellationToken)).Single(x => x.Code == key);
    }

    /// <inheritdoc />
    public async Task<int> RfqMinSuppliersAsync(CancellationToken cancellationToken) =>
        await _db.CompanyParameters.AsNoTracking().Select(p => (int?)p.RfqMinSuppliers).FirstOrDefaultAsync(cancellationToken)
        ?? CompanyParameter.DefaultRfqMinSuppliers;

    /// <inheritdoc />
    public async Task<decimal> InternalMarkupPercentAsync(CancellationToken cancellationToken) =>
        await _db.CompanyParameters.AsNoTracking().Select(p => (decimal?)p.InternalMarkupPercent).FirstOrDefaultAsync(cancellationToken)
        ?? CompanyParameter.DefaultInternalMarkupPercent;

    /// <summary>Dòng tham số của công ty đang làm việc; chưa có thì tạo với giá trị mặc định (lần sửa đầu tiên).</summary>
    private async Task<CompanyParameter> GetOrCreateAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var parameter = await _db.CompanyParameters.FirstOrDefaultAsync(cancellationToken);
        if (parameter is not null) return parameter;
        parameter = CompanyParameter.Create(companyId);
        _db.CompanyParameters.Add(parameter);
        return parameter;
    }

    /// <summary>Tham số bật/tắt (nhóm "Nhận hàng"): giá trị "Bật"/"Tắt", mặc định tắt.</summary>
    private static SystemParameterDto Toggle(string code, string name, bool value, string note, string group = "Nhận hàng") =>
        new(code, group, name, value ? "Bật" : "Tắt", "Tắt", !value, true, note);

    /// <summary>Giá trị bật/tắt gửi lên: true/false, 1/0, bật/tắt; trống = về mặc định.</summary>
    /// <exception cref="BusinessRuleException">Khi không đọc được.</exception>
    private static bool? ParseToggle(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "true" or "1" or "bật" or "bat" => true,
        "false" or "0" or "tắt" or "tat" => false,
        _ => throw new BusinessRuleException("Giá trị phải là Bật hoặc Tắt."),
    };

    /// <summary>Một tham số SLA (nhóm "Mua hàng"): giá trị hiện hành hoặc mặc định.</summary>
    private static SystemParameterDto Sla(string code, string name, int? value, int defaultValue, string note)
    {
        var v = value ?? defaultValue;
        return new(code, "Mua hàng", name, v.ToString(CultureInfo.InvariantCulture), defaultValue.ToString(CultureInfo.InvariantCulture),
            v == defaultValue, true, $"{note} Từ 0 đến {CompanyParameter.MaxSlaDays}; tính thứ 2–6, chưa trừ ngày lễ. Đo ở báo cáo SLA mua hàng.");
    }

    /// <summary>Số % kiểu Việt Nam, bỏ số 0 thừa: 12,5000 → "12,5"; 10 → "10".</summary>
    private static string FormatPercent(decimal value) => value.ToString("0.####", CultureInfo.GetCultureInfo("vi-VN"));
}
