using Proc.Domain.Common;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái công trình (lát Y2, design.md mục 19.2).</summary>
public enum ConstructionProjectStatus
{
    /// <summary>Đang thực hiện – chọn được trên đề nghị mua loại A, điều chỉnh dự toán được.</summary>
    InProgress = 1,

    /// <summary>Đã hoàn thành – không lập đề nghị mua mới, không điều chỉnh dự toán; số liệu lũy kế giữ để tra cứu.</summary>
    Completed = 2,

    /// <summary>Tạm ngừng – như hoàn thành nhưng mở lại được (đổi về Đang thực hiện).</summary>
    Suspended = 3,
}

/// <summary>
/// Công trình / dự án có tổng dự toán (lát Y2, design.md mục 19.2 – loại mua A): vật tư công trình mua theo từng công đoạn, hạng mục đến khi
/// hoàn thiện; khung chi phí là tổng dự toán – lũy kế đã mua – còn lại (biểu mẫu 1A). Thuộc một công ty (mỗi công ty là pháp nhân độc lập).
/// Tổng dự toán **chưa VAT** để so với lũy kế tính theo PO đã duyệt giá chưa VAT (Q4). Dự toán chỉ đổi qua <see cref="ReviseEstimate"/> – mỗi
/// lần là một dòng lịch sử có lý do, căn cứ (quyết định điều chỉnh), không sửa/xóa dòng cũ (cùng tinh thần D2 mục 20.2).
/// </summary>
[Auditable("Công trình", LabelProperty = nameof(Code))]
public sealed class ConstructionProject : AuditableEntity, ICompanyOwned
{
    /// <summary>Độ dài mã tối đa.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Độ dài tên, địa điểm tối đa.</summary>
    public const int NameMaxLength = 300;

    /// <summary>Độ dài ghi chú, lý do, căn cứ tối đa.</summary>
    public const int TextMaxLength = 500;

    private readonly List<ConstructionProjectEstimateRevision> _revisions = [];

    private ConstructionProject()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã công trình, duy nhất trong công ty, không đổi sau khi tạo (ví dụ CT-2026-01).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên công trình, ví dụ "Thi công Vua Trời Phạm Thiên".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Phòng ban đề xuất / quản lý công trình (in trên biểu mẫu 1A, điền sẵn khi lập đề nghị mua).</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>Địa điểm thi công / tập kết.</summary>
    public string? Location { get; private set; }

    /// <summary>Ngày khởi công (tùy chọn).</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>Ngày dự kiến hoàn thành (tùy chọn).</summary>
    public DateOnly? ExpectedEndDate { get; private set; }

    /// <summary>Tổng dự toán hiện hành, VND chưa VAT – bằng <c>NewAmount</c> của lần điều chỉnh gần nhất.</summary>
    public decimal EstimateAmount { get; private set; }

    /// <summary>Trạng thái.</summary>
    public ConstructionProjectStatus Status { get; private set; } = ConstructionProjectStatus.InProgress;

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Lịch sử dự toán: lần 0 là dự toán ban đầu, các lần sau là điều chỉnh.</summary>
    public IReadOnlyCollection<ConstructionProjectEstimateRevision> Revisions => _revisions;

    /// <summary>Tạo công trình kèm dòng dự toán ban đầu (lần 0).</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="code">Mã 2–30 ký tự: chữ không dấu, số và . _ / - (tự viết hoa).</param>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="departmentId">Phòng ban đề xuất, bắt buộc.</param>
    /// <param name="location">Địa điểm.</param>
    /// <param name="startDate">Ngày khởi công.</param>
    /// <param name="expectedEndDate">Ngày dự kiến hoàn thành – không trước ngày khởi công.</param>
    /// <param name="estimateAmount">Tổng dự toán chưa VAT, &gt; 0.</param>
    /// <param name="basis">Căn cứ (số quyết định phê duyệt dự toán…).</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="today">Hôm nay (ngày Việt Nam) – ngày hiệu lực của dự toán ban đầu.</param>
    /// <returns>Công trình đang thực hiện.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    public static ConstructionProject Create(
        Guid companyId, string code, string name, Guid departmentId, string? location, DateOnly? startDate, DateOnly? expectedEndDate,
        decimal estimateAmount, string? basis, string? note, DateOnly today)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Công trình phải thuộc một công ty.");
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsExtendedCode(normalized, 2, CodeMaxLength))
            throw new BusinessRuleException($"Mã công trình phải gồm 2–{CodeMaxLength} ký tự chữ không dấu, số hoặc . _ / -, không có khoảng trắng.");
        ValidateAmount(estimateAmount);
        var project = new ConstructionProject { CompanyId = companyId, Code = normalized, EstimateAmount = estimateAmount };
        project.Update(name, departmentId, location, startDate, expectedEndDate, ConstructionProjectStatus.InProgress, note);
        project._revisions.Add(ConstructionProjectEstimateRevision.Create(project.Id, 0, today, 0, estimateAmount, basis, null));
        return project;
    }

    /// <summary>Sửa thông tin chung, đổi trạng thái. Mã và dự toán không đổi ở đây (dự toán đổi qua <see cref="ReviseEstimate"/>).</summary>
    /// <param name="name">Tên, bắt buộc.</param>
    /// <param name="departmentId">Phòng ban đề xuất.</param>
    /// <param name="location">Địa điểm.</param>
    /// <param name="startDate">Ngày khởi công.</param>
    /// <param name="expectedEndDate">Ngày dự kiến hoàn thành.</param>
    /// <param name="status">Trạng thái.</param>
    /// <param name="note">Ghi chú.</param>
    /// <exception cref="BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    public void Update(
        string name, Guid departmentId, string? location, DateOnly? startDate, DateOnly? expectedEndDate, ConstructionProjectStatus status,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên công trình không được để trống.");
        if (name.Trim().Length > NameMaxLength) throw new BusinessRuleException($"Tên công trình tối đa {NameMaxLength} ký tự.");
        if (departmentId == Guid.Empty) throw new BusinessRuleException("Hãy chọn phòng ban đề xuất của công trình.");
        if (location?.Trim().Length > NameMaxLength) throw new BusinessRuleException($"Địa điểm tối đa {NameMaxLength} ký tự.");
        if (note?.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        if (startDate is not null && expectedEndDate is not null && expectedEndDate < startDate)
            throw new BusinessRuleException("Ngày dự kiến hoàn thành không được trước ngày khởi công.");
        if (!Enum.IsDefined(status)) throw new BusinessRuleException("Trạng thái công trình không hợp lệ.");
        Name = name.Trim();
        DepartmentId = departmentId;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        StartDate = startDate;
        ExpectedEndDate = expectedEndDate;
        Status = status;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// Điều chỉnh tổng dự toán (tăng hoặc giảm) – ghi một dòng lịch sử, không sửa dòng cũ.
    /// Ví dụ dự toán 64.000.000 điều chỉnh lên 70.000.000 theo QĐ 15/QĐ-CT: dòng lần 1, trước 64.000.000, sau 70.000.000, chênh +6.000.000.
    /// </summary>
    /// <param name="newAmount">Tổng dự toán mới chưa VAT, &gt; 0, khác dự toán hiện hành.</param>
    /// <param name="effectiveDate">Ngày hiệu lực của điều chỉnh (ngày quyết định).</param>
    /// <param name="basis">Căn cứ (số quyết định điều chỉnh…).</param>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <returns>Dòng lịch sử vừa thêm.</returns>
    /// <exception cref="BusinessRuleException">Khi công trình không còn thực hiện, số tiền sai hoặc thiếu lý do.</exception>
    public ConstructionProjectEstimateRevision ReviseEstimate(decimal newAmount, DateOnly effectiveDate, string? basis, string reason)
    {
        // Công trình đã hoàn thành / tạm ngừng không còn mua thêm – điều chỉnh dự toán lúc đó dễ che lũy kế vượt; mở lại trước rồi điều chỉnh.
        if (Status != ConstructionProjectStatus.InProgress)
            throw new BusinessRuleException("Chỉ điều chỉnh dự toán khi công trình đang thực hiện.");
        ValidateAmount(newAmount);
        if (newAmount == EstimateAmount) throw new BusinessRuleException("Dự toán mới phải khác dự toán hiện hành.");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do điều chỉnh dự toán.");
        var revisionNo = _revisions.Count == 0 ? 1 : _revisions.Max(r => r.RevisionNo) + 1;
        var revision = ConstructionProjectEstimateRevision.Create(Id, revisionNo, effectiveDate, EstimateAmount, newAmount, basis, reason);
        _revisions.Add(revision);
        EstimateAmount = newAmount;
        return revision;
    }

    /// <summary>Dự toán dương, tối đa 13 chữ số phần nguyên (numeric(19,2) còn chỗ cho lũy kế cộng dồn).</summary>
    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0) throw new BusinessRuleException("Tổng dự toán phải lớn hơn 0.");
        if (amount >= 10_000_000_000_000m) throw new BusinessRuleException("Tổng dự toán quá lớn.");
        if (decimal.Round(amount, 2) != amount) throw new BusinessRuleException("Tổng dự toán tối đa 2 chữ số thập phân.");
    }
}

/// <summary>
/// Một lần ghi dự toán của công trình (lát Y2): lần 0 = dự toán ban đầu, lần 1… = điều chỉnh. Chỉ thêm, không sửa/xóa – giữ dấu vết dự toán
/// tại từng thời điểm làm căn cứ cho các đề nghị mua đã duyệt.
/// </summary>
[Auditable("Điều chỉnh dự toán công trình")]
public sealed class ConstructionProjectEstimateRevision : AuditableEntity
{
    private ConstructionProjectEstimateRevision()
    {
    }

    /// <summary>Công trình.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>Lần: 0 = dự toán ban đầu, 1, 2… = điều chỉnh.</summary>
    public int RevisionNo { get; private set; }

    /// <summary>Ngày hiệu lực.</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>Dự toán trước (lần 0 = 0).</summary>
    public decimal PreviousAmount { get; private set; }

    /// <summary>Dự toán sau.</summary>
    public decimal NewAmount { get; private set; }

    /// <summary>Căn cứ (số quyết định…).</summary>
    public string? Basis { get; private set; }

    /// <summary>Lý do (bắt buộc với điều chỉnh).</summary>
    public string? Reason { get; private set; }

    /// <summary>Tạo dòng lịch sử dự toán.</summary>
    /// <param name="projectId">Công trình.</param>
    /// <param name="revisionNo">Lần.</param>
    /// <param name="effectiveDate">Ngày hiệu lực.</param>
    /// <param name="previousAmount">Dự toán trước.</param>
    /// <param name="newAmount">Dự toán sau.</param>
    /// <param name="basis">Căn cứ.</param>
    /// <param name="reason">Lý do.</param>
    /// <returns>Dòng lịch sử.</returns>
    /// <exception cref="BusinessRuleException">Khi căn cứ, lý do quá dài.</exception>
    internal static ConstructionProjectEstimateRevision Create(
        Guid projectId, int revisionNo, DateOnly effectiveDate, decimal previousAmount, decimal newAmount, string? basis, string? reason)
    {
        if (basis?.Trim().Length > ConstructionProject.TextMaxLength)
            throw new BusinessRuleException($"Căn cứ tối đa {ConstructionProject.TextMaxLength} ký tự.");
        if (reason?.Trim().Length > ConstructionProject.TextMaxLength)
            throw new BusinessRuleException($"Lý do tối đa {ConstructionProject.TextMaxLength} ký tự.");
        return new ConstructionProjectEstimateRevision
        {
            ProjectId = projectId, RevisionNo = revisionNo, EffectiveDate = effectiveDate, PreviousAmount = previousAmount, NewAmount = newAmount,
            Basis = string.IsNullOrWhiteSpace(basis) ? null : basis.Trim(), Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
    }
}
