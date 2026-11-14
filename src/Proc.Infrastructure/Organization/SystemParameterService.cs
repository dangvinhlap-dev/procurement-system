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
        var warn = parameter?.PoHighPriceWarnPercent ?? CompanyParameter.DefaultPoHighPriceWarnPercent;
        var pooledMargin = parameter?.PooledMarginPercent ?? CompanyParameter.DefaultPooledMarginPercent;
        var threshold = parameter?.RfqComparisonThreshold ?? 0m;

        var defaultText = CompanyParameter.DefaultRfqMinSuppliers.ToString(CultureInfo.InvariantCulture);
        List<SystemParameterDto> list =
        [
            new(SystemParameterCodes.RfqMinSuppliers, "Đấu thầu", "Số NCC tối thiểu khi gửi RFQ",
                rfqMin.ToString(CultureInfo.InvariantCulture), defaultText, rfqMin == CompanyParameter.DefaultRfqMinSuppliers, true,
                $"Phải mời đủ số NCC này mới gửi được yêu cầu báo giá (1–{CompanyParameter.MaxRfqMinSuppliers}). RFQ đã gửi không bị ảnh hưởng."),
            new(SystemParameterCodes.RfqComparisonThreshold, "Đấu thầu", "Ngưỡng so sánh 3 NCC – vật tư công trình (loại A)",
                FormatMoney(threshold), "0", threshold == 0, true,
                "RFQ vật tư công trình có giá trị trúng thầu (chưa VAT) từ ngưỡng này trở lên in biểu mẫu so sánh chọn NCC 3A (bắt buộc lý do chọn); " +
                "dưới ngưỡng in bảng trình giá 1 NCC 3a. Hàng hóa, dịch vụ (loại B) luôn in 3B. 0 = mọi RFQ công trình đều so sánh 3A. " +
                "Quy tắc mời / chào giá tối thiểu số NCC vẫn áp như cũ."),
            Toggle(SystemParameterCodes.RfqDossierRequired, "Bắt buộc đủ hồ sơ trình khi chọn thầu", parameter?.RequireRfqDossierComplete ?? false,
                "Bật: hồ sơ trình của RFQ (7 mục) phải đủ mới chọn thầu, gửi hội đồng duyệt được. Tắt: thiếu mục vẫn gửi, hộp thoại duyệt ghi mục còn thiếu.", "Đấu thầu"),
            new(SystemParameterCodes.PoHighPriceWarnPercent, "Giá", "% cảnh báo giá cao trên PO",
                FormatPercent(warn), FormatPercent(CompanyParameter.DefaultPoHighPriceWarnPercent),
                warn == CompanyParameter.DefaultPoHighPriceWarnPercent, true,
                "Dòng PO có giá sau chiết khấu (theo ĐVT cơ sở) cao hơn giá thấp nhất tập đoàn quá mức này thì hiện cảnh báo, không chặn. " +
                $"Giá thấp nhất tập đoàn = thấp nhất trong các giá mua gần nhất 12 tháng của từng công ty × NCC, như màn hình So sánh giá (0–{CompanyParameter.MaxPoHighPriceWarnPercent:0}%)."),
            new(SystemParameterCodes.PooledMarginPercent, "Mua hàng", "% biên độ phân bổ đặt hàng gộp",
                FormatPercent(pooledMargin), FormatPercent(CompanyParameter.DefaultPooledMarginPercent),
                pooledMargin == CompanyParameter.DefaultPooledMarginPercent, true,
                "Phiếu đặt hàng gộp (DHG) lập ở tổng công ty dùng tham số của tổng công ty: SL phân bổ cho mỗi công ty được lệch so với đề xuất theo " +
                $"tỷ lệ kế hoạch nhu cầu tháng tối đa max(tổng SL dòng × % ÷ 100, 1 ĐVT); vượt thì không tạo PO được (0–{CompanyParameter.MaxPooledMarginPercent:0}%)."),
            Toggle(SystemParameterCodes.SupplierOnboardingOptional, "Cho duyệt NCC mới không qua hồ sơ đánh giá (HSN)",
                parameter?.AllowSupplierWithoutOnboarding ?? false,
                "Tắt (mặc định): NCC tạo mới muốn lên Đã duyệt phải qua hồ sơ đánh giá NCC mới – Phòng cung ứng đánh giá, Văn phòng Ban đề xuất, " +
                "Hội đồng duyệt. Bật: đổi trạng thái trên form NCC như NCC cũ (dùng khi chuyển tiếp). Chỉ tham số của tổng công ty có tác dụng.", "Nhà cung cấp"),
            Toggle(SystemParameterCodes.ReceiptQcRequired, "Bắt buộc kiểm tra chất lượng khi nhận hàng", parameter?.RequireReceiptQc ?? false,
                "Bật: mọi dòng phiếu xác nhận nhận hàng (XNH) phải có kết quả kiểm tra (Đạt/Không đạt) mới xác nhận được."),
            Toggle(SystemParameterCodes.ReceiptSeparatePoCreator, "Người lập PO không xác nhận nhận hàng của PO đó", parameter?.SeparatePoCreatorFromReceipt ?? false,
                "Bật: người lập đơn mua hàng không xác nhận được phiếu XNH theo đơn của mình – người khác xác nhận giao nhận (tách nhiệm)."),
            Sla(SystemParameterCodes.SlaPricedDays, "SLA – hàng đã có hợp đồng/bảng giá", parameter?.SlaPricedDays, CompanyParameter.DefaultSlaPricedDays,
                "Số ngày làm việc từ lúc PR đủ duyệt đến lúc PO được duyệt cho hàng đã có bảng giá/hợp đồng hiệu lực."),
            Sla(SystemParameterCodes.SlaSourcingDays, "SLA – hàng chưa có NCC được duyệt", parameter?.SlaSourcingDays, CompanyParameter.DefaultSlaSourcingDays,
                "Số ngày làm việc để tìm nguồn, so sánh, trình duyệt và phát hành PO cho hàng chưa có bảng giá/hợp đồng (kể cả hàng mới chưa có mã)."),
            Sla(SystemParameterCodes.SlaUrgentDays, "SLA – hàng khẩn", parameter?.SlaUrgentDays, CompanyParameter.DefaultSlaUrgentDays,
                "PR mức ưu tiên Khẩn: số ngày làm việc tới lúc PO được duyệt (0 = trong ngày)."),
        ];
        list.AddRange(KpiParameters(parameter));
        // F1 trên ô giá trị: tham số lưu ở cột nào của dòng tham số công ty (design.md mục 9.3a).
        return list.Select(p => AuditFields.TryGetValue(p.Code, out var field) ? p with { AuditId = parameter?.Id, AuditField = field } : p).ToList();
    }

    /// <summary>Cột của <see cref="CompanyParameter"/> lưu từng tham số sửa được.</summary>
    private static readonly Dictionary<string, string> AuditFields = new()
    {
        [SystemParameterCodes.RfqMinSuppliers] = nameof(CompanyParameter.RfqMinSuppliers),
        [SystemParameterCodes.RfqComparisonThreshold] = nameof(CompanyParameter.RfqComparisonThreshold),
        [SystemParameterCodes.PoHighPriceWarnPercent] = nameof(CompanyParameter.PoHighPriceWarnPercent),
        [SystemParameterCodes.PooledMarginPercent] = nameof(CompanyParameter.PooledMarginPercent),
        [SystemParameterCodes.RfqDossierRequired] = nameof(CompanyParameter.RequireRfqDossierComplete),
        [SystemParameterCodes.SupplierOnboardingOptional] = nameof(CompanyParameter.AllowSupplierWithoutOnboarding),
        [SystemParameterCodes.ReceiptQcRequired] = nameof(CompanyParameter.RequireReceiptQc),
        [SystemParameterCodes.ReceiptSeparatePoCreator] = nameof(CompanyParameter.SeparatePoCreatorFromReceipt),
        [SystemParameterCodes.SlaPricedDays] = nameof(CompanyParameter.SlaPricedDays),
        [SystemParameterCodes.SlaSourcingDays] = nameof(CompanyParameter.SlaSourcingDays),
        [SystemParameterCodes.SlaUrgentDays] = nameof(CompanyParameter.SlaUrgentDays),
        [SystemParameterCodes.KpiTarget(KpiCode.Saving)] = nameof(CompanyParameter.KpiSavingTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.ContractCompliance)] = nameof(CompanyParameter.KpiContractTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.PurchaseSla)] = nameof(CompanyParameter.KpiSlaTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.OnTimeDelivery)] = nameof(CompanyParameter.KpiOnTimeTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Quality)] = nameof(CompanyParameter.KpiQualityTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Emergency)] = nameof(CompanyParameter.KpiEmergencyTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.SupplierReview)] = nameof(CompanyParameter.KpiSupplierReviewTarget),
        [SystemParameterCodes.KpiTarget(KpiCode.Violation)] = nameof(CompanyParameter.KpiViolationTarget),
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
            case SystemParameterCodes.RfqComparisonThreshold:
                decimal? money = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    // Nhận "50.000.000" (dấu nhóm nghìn kiểu Việt Nam), "50000000" hoặc "50 000 000"; tiền VND không có số lẻ.
                    var digits = request.Value.Trim().Replace(".", "").Replace(" ", "").Replace(" ", "");
                    if (!decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMoney))
                        throw new BusinessRuleException("Ngưỡng so sánh 3 NCC phải là số tiền đồng, ví dụ 50.000.000.");
                    money = parsedMoney;
                }

                (await GetOrCreateAsync(companyId, cancellationToken)).ChangeRfqComparisonThreshold(money);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.PoHighPriceWarnPercent:
            case SystemParameterCodes.PooledMarginPercent:
                decimal? percent = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    // Nhận cả "12,5" (kiểu Việt Nam, CLAUDE.md mục 5) lẫn "12.5" (ô số trên giao diện gửi lên); không nhận dấu nhóm nghìn.
                    var text = request.Value.Trim().Replace(',', '.');
                    if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                        throw new BusinessRuleException($"{(key == SystemParameterCodes.PooledMarginPercent ? "% biên độ phân bổ" : "% cảnh báo giá cao")} phải là số, ví dụ 10 hoặc 12,5.");
                    percent = parsed;
                }

                var parameterRow = await GetOrCreateAsync(companyId, cancellationToken);
                if (key == SystemParameterCodes.PooledMarginPercent) parameterRow.ChangePooledMarginPercent(percent);
                else parameterRow.ChangePoHighPriceWarnPercent(percent);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.ReceiptQcRequired:
            case SystemParameterCodes.ReceiptSeparatePoCreator:
            case SystemParameterCodes.RfqDossierRequired:
            case SystemParameterCodes.SupplierOnboardingOptional:
                var on = ParseToggle(request.Value);
                var row = await GetOrCreateAsync(companyId, cancellationToken);
                if (key == SystemParameterCodes.ReceiptQcRequired) row.ChangeRequireReceiptQc(on);
                else if (key == SystemParameterCodes.SupplierOnboardingOptional) row.ChangeAllowSupplierWithoutOnboarding(on);
                else if (key == SystemParameterCodes.RfqDossierRequired) row.ChangeRequireRfqDossierComplete(on);
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
    public async Task<decimal> RfqComparisonThresholdAsync(CancellationToken cancellationToken) =>
        await _db.CompanyParameters.AsNoTracking().Select(p => (decimal?)p.RfqComparisonThreshold).FirstOrDefaultAsync(cancellationToken) ?? 0m;

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

    /// <summary>Số tiền VND kiểu Việt Nam, không số lẻ: 50.000.000.</summary>
    private static string FormatMoney(decimal value) => value.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));
}
