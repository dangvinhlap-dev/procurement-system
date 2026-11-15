using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Tham số riêng của một công ty (design.md mục 5.4 "Tham số công ty"). Mỗi công ty tối đa một dòng; chưa có dòng nghĩa là
/// mọi tham số đang ở giá trị mặc định. Mỗi công ty là pháp nhân độc lập nên tự quyết tham số của mình (CLAUDE.md mục 4).
/// </summary>
[Auditable("Tham số công ty")]
public sealed class CompanyParameter : AuditableEntity, ICompanyOwned
{
    /// <summary>Số NCC tối thiểu mặc định khi gửi RFQ – design.md mục 8.4 bước 2 ("≥ 3 NCC").</summary>
    public const int DefaultRfqMinSuppliers = 3;

    /// <summary>Giới hạn trên của số NCC tối thiểu – tránh nhập nhầm khiến không RFQ nào gửi được.</summary>
    public const int MaxRfqMinSuppliers = 20;

    /// <summary>% markup nội bộ mặc định khi công ty chưa khai báo: 0% = gợi ý bán đúng giá vốn.</summary>
    public const decimal DefaultInternalMarkupPercent = 0m;

    /// <summary>Giới hạn trên của % markup nội bộ (gấp 11 lần giá vốn) – quá mức này gần như chắc chắn là gõ nhầm.</summary>
    public const decimal MaxInternalMarkupPercent = 1000m;

    private CompanyParameter()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số NCC phải mời tối thiểu mới gửi được RFQ (1–20).</summary>
    public int RfqMinSuppliers { get; private set; } = DefaultRfqMinSuppliers;

    /// <summary>
    /// % markup nội bộ mặc định – numeric(9,4). Dùng gợi ý giá bán nội bộ khi mặt hàng chưa có trong bảng giá nội bộ
    /// (design.md mục 1 câu hỏi 2): giá gợi ý = giá vốn kỳ đã đóng gần nhất × (1 + % markup).
    /// </summary>
    public decimal InternalMarkupPercent { get; private set; } = DefaultInternalMarkupPercent;

    /// <summary>
    /// Bắt buộc mọi dòng phiếu nhập mua có kết quả kiểm tra chất lượng mới ghi sổ được (design.md mục 18.14 – V7). Mặc định tắt để công ty
    /// chưa tổ chức kiểm tra không bị chặn nhận hàng.
    /// </summary>
    public bool RequireReceiptQc { get; private set; }

    /// <summary>
    /// Người lập đơn mua hàng không được ghi sổ phiếu nhập của chính đơn đó (design.md mục 18.14 – G4: người đặt hàng không một mình xác
    /// nhận giao nhận). Mặc định tắt (công ty nhỏ một người kiêm).
    /// </summary>
    public bool SeparatePoCreatorFromReceipt { get; private set; }

    /// <summary>
    /// Phiếu nhập mua từ NCC bắt buộc theo đơn mua hàng đã duyệt (lát R4, design.md mục 18.26): không lập, không ghi sổ phiếu nhập "thẳng"
    /// với đơn giá gõ tay – tránh mua hàng ngoài luồng PR/PO/duyệt. Mua gấp dùng PO mua khẩn cấp. Mặc định tắt (giữ cách làm cũ).
    /// </summary>
    public bool RequireReceiptPo { get; private set; }

    /// <summary>Bật/tắt bắt buộc nhập mua theo PO; null = về mặc định (tắt).</summary>
    public void ChangeRequireReceiptPo(bool? value) => RequireReceiptPo = value ?? false;

    /// <summary>
    /// Chọn thầu (gửi hội đồng duyệt kết quả) chỉ khi hồ sơ trình đủ mọi mục (design.md mục 8.6 – lát B3b). Mặc định tắt: thiếu mục vẫn gửi
    /// được, hộp thoại duyệt ghi "đủ x/7, còn thiếu…" để hội đồng quyết – công ty muốn chặn cứng thì bật.
    /// </summary>
    public bool RequireRfqDossierComplete { get; private set; }

    /// <summary>Bật/tắt bắt buộc đủ hồ sơ trình khi chọn thầu; null = về mặc định (tắt).</summary>
    public void ChangeRequireRfqDossierComplete(bool? value) => RequireRfqDossierComplete = value ?? false;

    /// <summary>
    /// Cho NCC tạo mới lên Đã duyệt bằng cách đổi trạng thái trên form NCC, không qua hồ sơ đánh giá NCC mới (HSN – lát G8, design.md
    /// mục 18.25). Mặc định tắt = bắt buộc hồ sơ (người dùng chốt 09/11/2026); chỉ đọc tham số của tổng công ty (danh mục NCC dùng chung).
    /// Bật khi chuyển tiếp/nhập NCC hàng loạt.
    /// </summary>
    public bool AllowSupplierWithoutOnboarding { get; private set; }

    /// <summary>Bật/tắt cho duyệt NCC mới không qua hồ sơ HSN; null = về mặc định (tắt – bắt buộc hồ sơ).</summary>
    public void ChangeAllowSupplierWithoutOnboarding(bool? value) => AllowSupplierWithoutOnboarding = value ?? false;

    /// <summary>Bật/tắt bắt buộc kiểm tra chất lượng khi ghi sổ nhận hàng; null = về mặc định (tắt).</summary>
    public void ChangeRequireReceiptQc(bool? value) => RequireReceiptQc = value ?? false;

    /// <summary>Bật/tắt tách người lập PO và người ghi sổ nhận hàng; null = về mặc định (tắt).</summary>
    public void ChangeSeparatePoCreatorFromReceipt(bool? value) => SeparatePoCreatorFromReceipt = value ?? false;

    /// <summary>SLA mặc định (ngày làm việc): hàng đã có hợp đồng/bảng giá – phát hành PO trong 1 ngày sau khi PR đủ duyệt (Ban CUVT).</summary>
    public const int DefaultSlaPricedDays = 1;

    /// <summary>SLA mặc định: hàng chưa có NCC được duyệt – 3–5 ngày tìm nguồn, so sánh, trình duyệt; lấy mức trên 5 ngày.</summary>
    public const int DefaultSlaSourcingDays = 5;

    /// <summary>SLA mặc định: hàng khẩn (PR mức ưu tiên Khẩn) – tiếp nhận ngay, 1 ngày làm việc.</summary>
    public const int DefaultSlaUrgentDays = 1;

    /// <summary>SLA tối đa (ngày làm việc) – lớn hơn gần như chắc chắn là gõ nhầm.</summary>
    public const int MaxSlaDays = 60;

    /// <summary>SLA hàng đã có hợp đồng/bảng giá (ngày làm việc từ lúc PR đủ duyệt đến lúc PO được duyệt) – design.md mục 18.13.</summary>
    public int SlaPricedDays { get; private set; } = DefaultSlaPricedDays;

    /// <summary>SLA hàng chưa có NCC được duyệt (ngày làm việc).</summary>
    public int SlaSourcingDays { get; private set; } = DefaultSlaSourcingDays;

    /// <summary>SLA hàng khẩn (ngày làm việc; 0 = trong ngày PR được duyệt).</summary>
    public int SlaUrgentDays { get; private set; } = DefaultSlaUrgentDays;

    /// <summary>Số ngày giữ nhật ký lỗi mặc định (tham số ERRLOG_KEEP_DAYS – lát QT2, design.md mục 21.4 ERR).</summary>
    public const int DefaultErrorLogKeepDays = 90;

    /// <summary>Số ngày giữ nhật ký lỗi tối thiểu – ít hơn thì lỗi cuối tuần chưa kịp xem đã bị dọn.</summary>
    public const int MinErrorLogKeepDays = 7;

    /// <summary>Số ngày giữ nhật ký lỗi tối đa (10 năm).</summary>
    public const int MaxErrorLogKeepDays = 3650;

    /// <summary>
    /// Số ngày giữ lần xảy ra lỗi trên màn hình ERR (lát QT2). Lỗi là của cả hệ thống nên <b>chỉ tham số của tổng công ty có tác dụng</b>.
    /// </summary>
    public int ErrorLogKeepDays { get; private set; } = DefaultErrorLogKeepDays;

    /// <summary>Đổi số ngày giữ nhật ký lỗi; null = về mặc định 90.</summary>
    /// <param name="days">Số ngày.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 7–3650.</exception>
    public void ChangeErrorLogKeepDays(int? days)
    {
        var v = days ?? DefaultErrorLogKeepDays;
        if (v < MinErrorLogKeepDays || v > MaxErrorLogKeepDays)
            throw new BusinessRuleException($"Số ngày giữ nhật ký lỗi phải từ {MinErrorLogKeepDays} đến {MaxErrorLogKeepDays}.");
        ErrorLogKeepDays = v;
    }

    /// <summary>Ngày chạy Full mặc định: 7 = Chủ nhật (thứ theo ISO: 1 = Thứ hai … 7 = Chủ nhật – B1, design.md mục 21.1).</summary>
    public const int DefaultBackupFullDay = 7;

    /// <summary>Giờ sao lưu mặc định 01:00 giờ Việt Nam (B1).</summary>
    public static readonly TimeOnly DefaultBackupTime = new(1, 0);

    /// <summary>Số tuần giữ bản Full (kèm các bản Diff của nó) mặc định (B1).</summary>
    public const int DefaultBackupKeepWeeks = 4;

    /// <summary>Số tuần giữ tối đa (một năm).</summary>
    public const int MaxBackupKeepWeeks = 52;

    /// <summary>Số ngày giữ bản pg_dump mặc định (B3 – tự chọn: 14 ngày đủ lấy lại dữ liệu xóa nhầm phát hiện muộn trong hai tuần).</summary>
    public const int DefaultDumpKeepDays = 14;

    /// <summary>Số ngày giữ bản pg_dump tối đa.</summary>
    public const int MaxDumpKeepDays = 365;

    /// <summary>
    /// Ngày trong tuần chạy sao lưu Full (1 = Thứ hai … 7 = Chủ nhật); các ngày khác chạy Diff (lát QT3b, tham số BACKUP_FULL_DAY). Sao lưu là của
    /// cả hệ thống nên <b>chỉ tham số của tổng công ty có tác dụng</b>.
    /// </summary>
    public int BackupFullDay { get; private set; } = DefaultBackupFullDay;

    /// <summary>Giờ chạy sao lưu hằng đêm (giờ Việt Nam) – Full / Diff rồi pg_dump, đính kèm (tham số BACKUP_TIME).</summary>
    public TimeOnly BackupTime { get; private set; } = DefaultBackupTime;

    /// <summary>Số tuần giữ bản Full và các Diff của nó (tham số BACKUP_KEEP_WEEKS).</summary>
    public int BackupKeepWeeks { get; private set; } = DefaultBackupKeepWeeks;

    /// <summary>Số ngày giữ bản pg_dump (tham số DUMP_KEEP_DAYS).</summary>
    public int DumpKeepDays { get; private set; } = DefaultDumpKeepDays;

    /// <summary>Đổi ngày chạy Full; null = về mặc định Chủ nhật.</summary>
    /// <param name="day">1 = Thứ hai … 7 = Chủ nhật.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài 1–7.</exception>
    public void ChangeBackupFullDay(int? day)
    {
        var v = day ?? DefaultBackupFullDay;
        if (v is < 1 or > 7) throw new BusinessRuleException("Ngày chạy sao lưu Full phải từ 1 (Thứ hai) đến 7 (Chủ nhật).");
        BackupFullDay = v;
    }

    /// <summary>Đổi giờ sao lưu; null = về mặc định 01:00.</summary>
    /// <param name="time">Giờ (phút chẵn, giây bỏ).</param>
    public void ChangeBackupTime(TimeOnly? time)
    {
        var v = time ?? DefaultBackupTime;
        BackupTime = new TimeOnly(v.Hour, v.Minute);
    }

    /// <summary>Đổi số tuần giữ bản Full; null = về mặc định 4.</summary>
    /// <param name="weeks">Số tuần.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài 1–52.</exception>
    public void ChangeBackupKeepWeeks(int? weeks)
    {
        var v = weeks ?? DefaultBackupKeepWeeks;
        if (v < 1 || v > MaxBackupKeepWeeks)
            throw new BusinessRuleException($"Số tuần giữ bản sao lưu Full phải từ 1 đến {MaxBackupKeepWeeks}.");
        BackupKeepWeeks = v;
    }

    /// <summary>Đổi số ngày giữ bản pg_dump; null = về mặc định 14.</summary>
    /// <param name="days">Số ngày.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài 1–365.</exception>
    public void ChangeDumpKeepDays(int? days)
    {
        var v = days ?? DefaultDumpKeepDays;
        if (v < 1 || v > MaxDumpKeepDays) throw new BusinessRuleException($"Số ngày giữ bản pg_dump phải từ 1 đến {MaxDumpKeepDays}.");
        DumpKeepDays = v;
    }

    /// <summary>Ngày bảo trì CSDL hằng tuần mặc định: 7 = Chủ nhật (lát QT4 – tự chọn, ngày ít người dùng nhất).</summary>
    public const int DefaultDbMaintenanceDay = 7;

    /// <summary>
    /// Giờ bảo trì CSDL mặc định 03:00 giờ Việt Nam (lát QT4 – tự chọn): sau lượt sao lưu 01:00 để VACUUM không chạy chồng lúc chụp bản Full.
    /// </summary>
    public static readonly TimeOnly DefaultDbMaintenanceTime = new(3, 0);

    /// <summary>
    /// Ngày trong tuần chạy <c>VACUUM (ANALYZE)</c> cả CSDL (1 = Thứ hai … 7 = Chủ nhật – tham số DB_MAINT_DAY, lát QT4). Bảo trì là của cả hệ
    /// thống nên <b>chỉ tham số của tổng công ty có tác dụng</b>.
    /// </summary>
    public int DbMaintenanceDay { get; private set; } = DefaultDbMaintenanceDay;

    /// <summary>Giờ chạy bảo trì CSDL hằng tuần (giờ Việt Nam – tham số DB_MAINT_TIME).</summary>
    public TimeOnly DbMaintenanceTime { get; private set; } = DefaultDbMaintenanceTime;

    /// <summary>Đổi ngày bảo trì CSDL; null = về mặc định Chủ nhật.</summary>
    /// <param name="day">1 = Thứ hai … 7 = Chủ nhật.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài 1–7.</exception>
    public void ChangeDbMaintenanceDay(int? day)
    {
        var v = day ?? DefaultDbMaintenanceDay;
        if (v is < 1 or > 7) throw new BusinessRuleException("Ngày bảo trì CSDL phải từ 1 (Thứ hai) đến 7 (Chủ nhật).");
        DbMaintenanceDay = v;
    }

    /// <summary>Đổi giờ bảo trì CSDL; null = về mặc định 03:00.</summary>
    /// <param name="time">Giờ (phút chẵn, giây bỏ).</param>
    public void ChangeDbMaintenanceTime(TimeOnly? time)
    {
        var v = time ?? DefaultDbMaintenanceTime;
        DbMaintenanceTime = new TimeOnly(v.Hour, v.Minute);
    }

    /// <summary>% cảnh báo giá cao trên PO mặc định – cùng mức tô màu mặc định của màn hình So sánh giá (design.md mục 8.2).</summary>
    public const decimal DefaultPoHighPriceWarnPercent = 10m;

    /// <summary>Giới hạn trên của % cảnh báo giá cao – lớn hơn gần như chắc chắn là gõ nhầm.</summary>
    public const decimal MaxPoHighPriceWarnPercent = 1000m;

    /// <summary>
    /// % cảnh báo giá cao trên PO – numeric(9,4) (design.md mục 8.5): dòng PO có giá sau CK theo ĐVT cơ sở cao hơn giá thấp nhất tập đoàn
    /// quá mức này thì hiện cảnh báo (không chặn). 0 = cảnh báo mọi giá cao hơn giá thấp nhất.
    /// </summary>
    public decimal PoHighPriceWarnPercent { get; private set; } = DefaultPoHighPriceWarnPercent;

    /// <summary>Đổi % cảnh báo giá cao trên PO.</summary>
    /// <param name="value">Giá trị mới (làm tròn 4 số lẻ); null = về mặc định <see cref="DefaultPoHighPriceWarnPercent"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxPoHighPriceWarnPercent"/>.</exception>
    public void ChangePoHighPriceWarnPercent(decimal? value)
    {
        var v = Math.Round(value ?? DefaultPoHighPriceWarnPercent, 4, MidpointRounding.AwayFromZero);
        if (v < 0 || v > MaxPoHighPriceWarnPercent)
            throw new BusinessRuleException($"% cảnh báo giá cao phải từ 0 đến {MaxPoHighPriceWarnPercent:0}.");
        PoHighPriceWarnPercent = v;
    }

    /// <summary>Giới hạn trên của ngưỡng so sánh 3 NCC (1.000 tỷ đồng) – lớn hơn gần như chắc chắn là gõ nhầm.</summary>
    public const decimal MaxRfqComparisonThreshold = 1_000_000_000_000m;

    /// <summary>
    /// Ngưỡng so sánh 3 NCC cho vật tư công trình (loại A) – numeric(19,2), VND chưa VAT (lát Y6, design.md mục 19.1 Q7): RFQ loại A có giá
    /// trị trúng thầu từ ngưỡng trở lên in biểu mẫu so sánh <b>3A</b> (bắt buộc lý do chọn), dưới ngưỡng in bảng trình giá 1 NCC <b>3a</b>.
    /// Mặc định 0 = mọi RFQ loại A đều so sánh (an toàn nhất, đúng biểu mẫu khi chưa quyết ngưỡng).
    /// </summary>
    public decimal RfqComparisonThreshold { get; private set; }

    /// <summary>Đổi ngưỡng so sánh 3 NCC loại A.</summary>
    /// <param name="value">Số tiền mới (làm tròn đồng); null = về mặc định 0.</param>
    /// <exception cref="BusinessRuleException">Khi âm hoặc quá <see cref="MaxRfqComparisonThreshold"/>.</exception>
    public void ChangeRfqComparisonThreshold(decimal? value)
    {
        var v = Math.Round(value ?? 0m, 0, MidpointRounding.AwayFromZero);
        if (v < 0 || v > MaxRfqComparisonThreshold)
            throw new BusinessRuleException("Ngưỡng so sánh 3 NCC phải từ 0 đến 1.000.000.000.000 đồng.");
        RfqComparisonThreshold = v;
    }

    /// <summary>% biên độ phân bổ đặt hàng gộp mặc định (lát G7, design.md mục 18.25).</summary>
    public const decimal DefaultPooledMarginPercent = 10m;

    /// <summary>Giới hạn trên của % biên độ phân bổ.</summary>
    public const decimal MaxPooledMarginPercent = 100m;

    /// <summary>
    /// % biên độ phân bổ đặt hàng gộp – numeric(9,4), dùng tham số của tổng công ty (nơi lập phiếu DHG): SL phân bổ cho mỗi công ty được lệch
    /// so với đề xuất theo tỷ lệ kế hoạch nhu cầu tối đa max(tổng SL dòng × % ÷ 100, 1 ĐVT). Quyết định: Phòng cung ứng chung chỉ điều chỉnh
    /// phân bổ trong biên độ Ban cho phép.
    /// </summary>
    public decimal PooledMarginPercent { get; private set; } = DefaultPooledMarginPercent;

    /// <summary>Đổi % biên độ phân bổ đặt hàng gộp.</summary>
    /// <param name="value">Giá trị mới (làm tròn 4 số lẻ); null = về mặc định <see cref="DefaultPooledMarginPercent"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxPooledMarginPercent"/>.</exception>
    public void ChangePooledMarginPercent(decimal? value)
    {
        var v = Math.Round(value ?? DefaultPooledMarginPercent, 4, MidpointRounding.AwayFromZero);
        if (v < 0 || v > MaxPooledMarginPercent)
            throw new BusinessRuleException($"% biên độ phân bổ phải từ 0 đến {MaxPooledMarginPercent:0}.");
        PooledMarginPercent = v;
    }

    /// <summary>
    /// Tạo bộ tham số với giá trị mặc định.
    /// </summary>
    /// <param name="companyId">Công ty.</param>
    /// <returns>Tham số mới.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty.</exception>
    public static CompanyParameter Create(Guid companyId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Tham số phải thuộc một công ty.");
        return new CompanyParameter { CompanyId = companyId };
    }

    /// <summary>
    /// Đổi số NCC tối thiểu khi gửi RFQ.
    /// </summary>
    /// <param name="value">Giá trị mới; null = về mặc định <see cref="DefaultRfqMinSuppliers"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 1–<see cref="MaxRfqMinSuppliers"/>.</exception>
    public void ChangeRfqMinSuppliers(int? value)
    {
        var v = value ?? DefaultRfqMinSuppliers;
        // Cho phép 1 (chỉ định thầu) vì là quyết định của từng công ty; trên 20 gần như chắc chắn là gõ nhầm.
        if (v < 1 || v > MaxRfqMinSuppliers)
            throw new BusinessRuleException($"Số NCC tối thiểu khi gửi RFQ phải từ 1 đến {MaxRfqMinSuppliers}.");
        RfqMinSuppliers = v;
    }

    /// <summary>
    /// Đổi % markup nội bộ mặc định.
    /// </summary>
    /// <param name="value">Giá trị mới (làm tròn 4 số lẻ theo cột numeric(9,4)); null = về mặc định <see cref="DefaultInternalMarkupPercent"/>.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxInternalMarkupPercent"/>.</exception>
    public void ChangeInternalMarkupPercent(decimal? value)
    {
        var v = Math.Round(value ?? DefaultInternalMarkupPercent, 4, MidpointRounding.AwayFromZero);
        // Không cho âm: bán nội bộ dưới giá vốn phải là quyết định riêng từng mặt hàng ở bảng giá, không phải mặc định.
        if (v < 0 || v > MaxInternalMarkupPercent)
            throw new BusinessRuleException($"% markup nội bộ phải từ 0 đến {MaxInternalMarkupPercent:0}.");
        InternalMarkupPercent = v;
    }

    /// <summary>Đổi SLA một loại yêu cầu (design.md mục 18.13).</summary>
    /// <param name="category">Loại yêu cầu.</param>
    /// <param name="days">Số ngày làm việc; null = về mặc định.</param>
    /// <exception cref="BusinessRuleException">Khi ngoài khoảng 0–<see cref="MaxSlaDays"/>.</exception>
    public void ChangeSlaDays(Purchasing.SlaCategory category, int? days)
    {
        var v = days ?? category switch
        {
            Purchasing.SlaCategory.Priced => DefaultSlaPricedDays,
            Purchasing.SlaCategory.Sourcing => DefaultSlaSourcingDays,
            _ => DefaultSlaUrgentDays,
        };
        if (v < 0 || v > MaxSlaDays) throw new BusinessRuleException($"SLA phải từ 0 đến {MaxSlaDays} ngày làm việc.");
        switch (category)
        {
            case Purchasing.SlaCategory.Priced: SlaPricedDays = v; break;
            case Purchasing.SlaCategory.Sourcing: SlaSourcingDays = v; break;
            default: SlaUrgentDays = v; break;
        }
    }

    /// <summary>Mục tiêu KPI tiết kiệm mua hàng (%) – numeric(9,4) (design.md mục 18.18).</summary>
    public decimal KpiSavingTarget { get; private set; } = KpiCatalog.Get(KpiCode.Saving).DefaultTarget;

    /// <summary>Mục tiêu KPI mua theo giá/hợp đồng đã duyệt (%).</summary>
    public decimal KpiContractTarget { get; private set; } = KpiCatalog.Get(KpiCode.ContractCompliance).DefaultTarget;

    /// <summary>Mục tiêu KPI đơn hàng đúng SLA (%).</summary>
    public decimal KpiSlaTarget { get; private set; } = KpiCatalog.Get(KpiCode.PurchaseSla).DefaultTarget;

    /// <summary>Mục tiêu KPI NCC giao đúng hạn (%).</summary>
    public decimal KpiOnTimeTarget { get; private set; } = KpiCatalog.Get(KpiCode.OnTimeDelivery).DefaultTarget;

    /// <summary>Mục tiêu KPI hàng đạt chất lượng (%).</summary>
    public decimal KpiQualityTarget { get; private set; } = KpiCatalog.Get(KpiCode.Quality).DefaultTarget;

    /// <summary>Mức trần KPI tỷ lệ mua khẩn cấp (%).</summary>
    public decimal KpiEmergencyTarget { get; private set; } = KpiCatalog.Get(KpiCode.Emergency).DefaultTarget;

    /// <summary>Mức trần KPI tồn kho chậm luân chuyển (%) – <b>ngừng dùng từ 0.31.0</b> (lát K3), giữ cột, xóa ở bản sau (CLAUDE.md mục 8).</summary>
    public decimal KpiSlowMovingTarget { get; private set; } = 10m;

    /// <summary>Mục tiêu KPI hoàn thành đánh giá NCC (%).</summary>
    public decimal KpiSupplierReviewTarget { get; private set; } = KpiCatalog.Get(KpiCode.SupplierReview).DefaultTarget;

    /// <summary>Mức trần KPI vi phạm nghiêm trọng (số vụ).</summary>
    public int KpiViolationTarget { get; private set; }

    /// <summary>Số ngày không xuất tính KPI tồn chậm luân chuyển – <b>ngừng dùng từ 0.31.0</b> (lát K3), giữ cột, xóa ở bản sau.</summary>
    public int KpiSlowMovingDays { get; private set; } = 90;

    /// <summary>Mục tiêu đang dùng của một KPI.</summary>
    /// <param name="code">Mã chỉ số.</param>
    /// <returns>Mục tiêu.</returns>
    public decimal KpiTarget(KpiCode code) => code switch
    {
        KpiCode.Saving => KpiSavingTarget,
        KpiCode.ContractCompliance => KpiContractTarget,
        KpiCode.PurchaseSla => KpiSlaTarget,
        KpiCode.OnTimeDelivery => KpiOnTimeTarget,
        KpiCode.Quality => KpiQualityTarget,
        KpiCode.Emergency => KpiEmergencyTarget,
        KpiCode.SupplierReview => KpiSupplierReviewTarget,
        _ => KpiViolationTarget,
    };

    /// <summary>Đổi mục tiêu một KPI (design.md mục 18.18).</summary>
    /// <param name="code">Mã chỉ số.</param>
    /// <param name="value">Mục tiêu mới (tỷ lệ làm tròn 4 số lẻ); null = về mặc định.</param>
    /// <exception cref="BusinessRuleException">Tỷ lệ ngoài 0–100; số vụ không nguyên hoặc ngoài 0–<see cref="KpiCatalog.MaxViolationTarget"/>.</exception>
    public void ChangeKpiTarget(KpiCode code, decimal? value)
    {
        var definition = KpiCatalog.Get(code);
        var v = Math.Round(value ?? definition.DefaultTarget, 4, MidpointRounding.AwayFromZero);
        if (definition.IsCount)
        {
            if (v != decimal.Truncate(v) || v < 0 || v > KpiCatalog.MaxViolationTarget)
                throw new BusinessRuleException($"Mục tiêu \"{definition.Name}\" phải là số nguyên từ 0 đến {KpiCatalog.MaxViolationTarget}.");
            KpiViolationTarget = (int)v;
            return;
        }

        if (v < 0 || v > 100) throw new BusinessRuleException($"Mục tiêu \"{definition.Name}\" phải từ 0 đến 100%.");
        switch (code)
        {
            case KpiCode.Saving: KpiSavingTarget = v; break;
            case KpiCode.ContractCompliance: KpiContractTarget = v; break;
            case KpiCode.PurchaseSla: KpiSlaTarget = v; break;
            case KpiCode.OnTimeDelivery: KpiOnTimeTarget = v; break;
            case KpiCode.Quality: KpiQualityTarget = v; break;
            case KpiCode.Emergency: KpiEmergencyTarget = v; break;
            default: KpiSupplierReviewTarget = v; break;
        }
    }

}
