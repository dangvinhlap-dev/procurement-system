using Proc.Domain.Common;

namespace Proc.Domain.Control;

/// <summary>Mức vi phạm KSNB (design.md mục 18.19).</summary>
public enum FindingSeverity
{
    /// <summary>Thường.</summary>
    Normal = 1,

    /// <summary>Nghiêm trọng – tính vào KPI "Vi phạm nghiêm trọng" (mục tiêu 0 vụ, mục 18.18).</summary>
    Serious = 2,
}

/// <summary>Trạng thái vi phạm.</summary>
public enum FindingStatus
{
    /// <summary>Đang xác minh, xử lý – còn sửa được.</summary>
    Open = 1,

    /// <summary>Đã có kết luận – khóa, không sửa.</summary>
    Concluded = 2,
}

/// <summary>
/// Một vi phạm do Kiểm soát nội bộ ghi nhận (design.md mục 18.19): thường lập từ một dấu hiệu bất thường, kèm chứng từ liên quan.
/// Không xóa: ghi nhận nhầm thì kết luận "không vi phạm" và hạ mức – giữ dấu vết; mọi thay đổi vào AuditLog. Lãnh đạo được thông báo thẳng
/// khi ghi nhận và khi kết luận.
/// </summary>
[Auditable("Vi phạm KSNB", LabelProperty = nameof(No))]
public sealed class ControlFinding : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài tiêu đề tối đa.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Độ dài mô tả, kết luận tối đa.</summary>
    public const int TextMaxLength = 2000;

    private ControlFinding()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ VP-BHA-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>Ngày phát hiện – KPI vi phạm tính theo ngày này.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Mức.</summary>
    public FindingSeverity Severity { get; private set; }

    /// <summary>Loại dấu hiệu bất thường gốc (mã <c>AnomalyCodes</c>); null = KSNB tự phát hiện.</summary>
    public string? AnomalyCode { get; private set; }

    /// <summary>Tiêu đề ngắn.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Mô tả vi phạm, bằng chứng.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Màn hình của chứng từ liên quan (PO, GRN, SUPAPR…).</summary>
    public string? RefScreen { get; private set; }

    /// <summary>Chứng từ liên quan.</summary>
    public Guid? RefId { get; private set; }

    /// <summary>Số chứng từ liên quan.</summary>
    public string? RefNo { get; private set; }

    /// <summary>NCC liên quan.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Trạng thái.</summary>
    public FindingStatus Status { get; private set; } = FindingStatus.Open;

    /// <summary>Kết luận, hướng xử lý.</summary>
    public string? Conclusion { get; private set; }

    /// <summary>Thời điểm kết luận (UTC).</summary>
    public DateTimeOffset? ConcludedAt { get; private set; }

    /// <summary>Người kết luận.</summary>
    public Guid? ConcludedBy { get; private set; }

    /// <summary>
    /// Ghi nhận vi phạm.
    /// </summary>
    /// <param name="companyId">Công ty phát sinh.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="data">Nội dung.</param>
    /// <returns>Vi phạm đang mở.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số, tiêu đề, mô tả hoặc mức sai.</exception>
    public static ControlFinding Create(Guid companyId, string no, ControlFindingData data)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Vi phạm phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Thiếu số phiếu vi phạm.");
        var finding = new ControlFinding
        {
            CompanyId = companyId, No = no.Trim(),
            AnomalyCode = string.IsNullOrWhiteSpace(data.AnomalyCode) ? null : data.AnomalyCode.Trim(),
            RefScreen = string.IsNullOrWhiteSpace(data.RefScreen) ? null : data.RefScreen.Trim(), RefId = data.RefId,
            RefNo = string.IsNullOrWhiteSpace(data.RefNo) ? null : data.RefNo.Trim(),
        };
        finding.Apply(data);
        return finding;
    }

    /// <summary>
    /// Sửa ngày, mức, tiêu đề, mô tả, NCC khi còn mở. Chứng từ gốc, loại dấu hiệu giữ nguyên.
    /// </summary>
    /// <param name="data">Nội dung mới.</param>
    /// <exception cref="BusinessRuleException">Khi đã kết luận hoặc dữ liệu sai.</exception>
    public void Update(ControlFindingData data)
    {
        if (Status != FindingStatus.Open) throw new BusinessRuleException("Vi phạm đã kết luận, không sửa được.");
        Apply(data);
    }

    /// <summary>
    /// Kết luận vi phạm (đã xác minh, hướng xử lý hoặc "không vi phạm"). Sau đó không sửa được.
    /// </summary>
    /// <param name="conclusion">Kết luận, bắt buộc.</param>
    /// <param name="severity">Mức sau xác minh – ghi nhận nhầm thì hạ về Thường.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người kết luận.</param>
    /// <exception cref="BusinessRuleException">Khi đã kết luận hoặc thiếu kết luận.</exception>
    public void Conclude(string? conclusion, FindingSeverity severity, DateTimeOffset at, Guid by)
    {
        if (Status != FindingStatus.Open) throw new BusinessRuleException("Vi phạm đã kết luận.");
        if (string.IsNullOrWhiteSpace(conclusion)) throw new BusinessRuleException("Hãy nhập kết luận.");
        if (conclusion.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Kết luận tối đa {TextMaxLength} ký tự.");
        if (!Enum.IsDefined(severity)) throw new BusinessRuleException("Mức vi phạm không hợp lệ.");
        Severity = severity;
        Conclusion = conclusion.Trim();
        Status = FindingStatus.Concluded;
        ConcludedAt = at;
        ConcludedBy = by;
    }

    /// <summary>Kiểm tra và gán dữ liệu phát hiện kiểm soát từ form (mức vi phạm, tiêu đề, mô tả).</summary>
    private void Apply(ControlFindingData data)
    {
        if (!Enum.IsDefined(data.Severity)) throw new BusinessRuleException("Mức vi phạm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(data.Title)) throw new BusinessRuleException("Hãy nhập tiêu đề vi phạm.");
        if (string.IsNullOrWhiteSpace(data.Description)) throw new BusinessRuleException("Hãy mô tả vi phạm.");
        if (data.Title.Trim().Length > TitleMaxLength) throw new BusinessRuleException($"Tiêu đề tối đa {TitleMaxLength} ký tự.");
        if (data.Description.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Mô tả tối đa {TextMaxLength} ký tự.");
        Date = data.Date;
        Severity = data.Severity;
        Title = data.Title.Trim();
        Description = data.Description.Trim();
        SupplierId = data.SupplierId;
    }
}

/// <summary>Nội dung vi phạm khi ghi nhận/sửa.</summary>
/// <param name="Date">Ngày phát hiện.</param>
/// <param name="Severity">Mức.</param>
/// <param name="Title">Tiêu đề.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="SupplierId">NCC liên quan.</param>
/// <param name="AnomalyCode">Loại dấu hiệu gốc (chỉ dùng khi ghi nhận).</param>
/// <param name="RefScreen">Màn hình chứng từ liên quan (chỉ dùng khi ghi nhận).</param>
/// <param name="RefId">Chứng từ liên quan (chỉ dùng khi ghi nhận).</param>
/// <param name="RefNo">Số chứng từ liên quan (chỉ dùng khi ghi nhận).</param>
public sealed record ControlFindingData(
    DateOnly Date, FindingSeverity Severity, string Title, string Description, Guid? SupplierId,
    string? AnomalyCode = null, string? RefScreen = null, Guid? RefId = null, string? RefNo = null);
