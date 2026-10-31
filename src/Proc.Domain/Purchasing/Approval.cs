using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Purchasing;

/// <summary>Trạng thái chứng từ mua hàng có duyệt (design.md mục 3: Nháp → Chờ duyệt → Đã duyệt …).</summary>
public enum ApprovalDocumentStatus
{
    /// <summary>Nháp: sửa được; bị từ chối cũng về đây (kèm lý do).</summary>
    Draft = 1,

    /// <summary>Đã gửi, đang chờ các cấp duyệt – không sửa được.</summary>
    Submitted = 2,

    /// <summary>Đã duyệt đủ các cấp.</summary>
    Approved = 3,

    /// <summary>PO đã nhận đủ hàng (đóng). PR không dùng.</summary>
    Closed = 4,

    /// <summary>Đã hủy: giữ số và lịch sử.</summary>
    Cancelled = 5,
}

/// <summary>Kết quả một bước duyệt.</summary>
public enum ApprovalStepStatus
{
    /// <summary>Chờ duyệt.</summary>
    Pending = 1,

    /// <summary>Đã duyệt.</summary>
    Approved = 2,

    /// <summary>Từ chối (cả lượt gửi dừng lại).</summary>
    Rejected = 3,

    /// <summary>Không cần duyệt nữa (phiếu bị từ chối ở cấp thấp hơn hoặc hủy).</summary>
    Skipped = 4,
}

/// <summary>
/// Một cấp trong quy trình duyệt của công ty (design.md mục 5 – <c>ApprovalRule</c>, mục 6.2): loại chứng từ, thứ tự cấp, ngưỡng giá
/// trị và vai trò được duyệt. Phiếu có tổng giá trị ≥ ngưỡng của cấp nào thì phải qua cấp đó; duyệt lần lượt cấp thấp → cao.
/// Ví dụ PR: cấp 1 ≥ 0 đ – Trưởng bộ phận; cấp 2 ≥ 50.000.000 đ – Giám đốc → PR 80 triệu qua 2 cấp, PR 10 triệu qua 1 cấp.
/// </summary>
[Auditable("Quy trình duyệt", LabelProperty = nameof(Label))]
public sealed class ApprovalRule : AuditableEntity, ICompanyOwned
{
    /// <summary>Số cấp tối đa.</summary>
    public const int MaxLevel = 9;

    /// <summary>Loại chứng từ có duyệt.</summary>
    public static readonly IReadOnlyList<string> DocTypes = [DocumentTypes.PurchaseRequest, DocumentTypes.PurchaseOrder];

    private ApprovalRule()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Loại chứng từ (PR, PO).</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Thứ tự cấp, từ 1.</summary>
    public int Level { get; private set; }

    /// <summary>Ngưỡng: phiếu có tổng giá trị ≥ số này mới phải qua cấp này – numeric(19,2).</summary>
    public decimal MinAmount { get; private set; }

    /// <summary>Vai trò được duyệt ở cấp này (người có vai trò đó tại công ty và có quyền duyệt loại phiếu).</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Nhãn trong nhật ký.</summary>
    public string Label => $"{DocType} cấp {Level}";

    /// <summary>Tạo một cấp duyệt.</summary>
    /// <exception cref="BusinessRuleException">Khi loại chứng từ, cấp, ngưỡng không hợp lệ.</exception>
    public static ApprovalRule Create(Guid companyId, string docType, int level, decimal minAmount, Guid roleId)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Quy trình duyệt phải thuộc một công ty.");
        var type = (docType ?? string.Empty).Trim().ToUpperInvariant();
        if (!DocTypes.Contains(type)) throw new BusinessRuleException($"Loại chứng từ duyệt phải là {string.Join(", ", DocTypes)}.");
        var rule = new ApprovalRule { CompanyId = companyId, DocType = type };
        rule.Update(level, minAmount, roleId);
        return rule;
    }

    /// <summary>Sửa cấp, ngưỡng, vai trò.</summary>
    /// <exception cref="BusinessRuleException">Khi cấp ngoài 1–9, ngưỡng âm hoặc chưa chọn vai trò.</exception>
    public void Update(int level, decimal minAmount, Guid roleId)
    {
        if (level is < 1 or > MaxLevel) throw new BusinessRuleException($"Cấp duyệt phải từ 1 đến {MaxLevel}.");
        if (minAmount < 0) throw new BusinessRuleException("Ngưỡng giá trị không được âm.");
        if (roleId == Guid.Empty) throw new BusinessRuleException("Hãy chọn vai trò duyệt.");
        Level = level;
        MinAmount = Math.Round(minAmount, 2, MidpointRounding.AwayFromZero);
        RoleId = roleId;
    }
}

/// <summary>
/// Một bước duyệt của một lượt gửi duyệt (design.md mục 12.1 – <c>ApprovalRequest</c>, <c>ApprovalStep</c>: gộp làm một bảng, lượt
/// gửi phân biệt bằng <see cref="Round"/>). Chỉ thêm, sửa kết quả; không xóa – là lịch sử duyệt của phiếu.
/// </summary>
[Auditable("Bước duyệt")]
public sealed class ApprovalStep : Entity, ICompanyOwned
{
    private ApprovalStep()
    {
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Loại chứng từ.</summary>
    public string DocType { get; private set; } = string.Empty;

    /// <summary>Chứng từ.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Lượt gửi duyệt (1, 2… – mỗi lần gửi lại sau khi bị từ chối tăng 1).</summary>
    public int Round { get; private set; }

    /// <summary>Cấp.</summary>
    public int Level { get; private set; }

    /// <summary>Vai trò được duyệt; null = bất kỳ ai có quyền duyệt (công ty chưa cấu hình quy trình).</summary>
    public Guid? RoleId { get; private set; }

    /// <summary>Kết quả.</summary>
    public ApprovalStepStatus Status { get; private set; } = ApprovalStepStatus.Pending;

    /// <summary>Người duyệt/từ chối.</summary>
    public Guid? ActedBy { get; private set; }

    /// <summary>Thời điểm duyệt/từ chối (UTC).</summary>
    public DateTimeOffset? ActedAt { get; private set; }

    /// <summary>Ý kiến (bắt buộc khi từ chối).</summary>
    public string? Comment { get; private set; }

    /// <summary>Tạo bước chờ duyệt.</summary>
    /// <returns>Bước duyệt.</returns>
    public static ApprovalStep Create(Guid companyId, string docType, Guid documentId, int round, int level, Guid? roleId) =>
        new() { CompanyId = companyId, DocType = docType, DocumentId = documentId, Round = round, Level = level, RoleId = roleId };

    /// <summary>Ghi kết quả duyệt.</summary>
    /// <exception cref="BusinessRuleException">Khi bước không còn chờ duyệt.</exception>
    public void Approve(Guid userId, DateTimeOffset at, string? comment) => Act(ApprovalStepStatus.Approved, userId, at, comment);

    /// <summary>Ghi từ chối – bắt buộc lý do.</summary>
    /// <exception cref="BusinessRuleException">Khi thiếu lý do hoặc bước không còn chờ duyệt.</exception>
    public void Reject(Guid userId, DateTimeOffset at, string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) throw new BusinessRuleException("Phải nhập lý do từ chối.");
        Act(ApprovalStepStatus.Rejected, userId, at, comment);
    }

    /// <summary>Bước không cần duyệt nữa (cấp dưới đã từ chối hoặc phiếu bị rút/hủy).</summary>
    public void Skip()
    {
        if (Status == ApprovalStepStatus.Pending) Status = ApprovalStepStatus.Skipped;
    }

    private void Act(ApprovalStepStatus status, Guid userId, DateTimeOffset at, string? comment)
    {
        if (Status != ApprovalStepStatus.Pending) throw new BusinessRuleException("Bước duyệt này đã xử lý xong.");
        if (comment?.Trim().Length > 500) throw new BusinessRuleException("Ý kiến duyệt tối đa 500 ký tự.");
        Status = status;
        ActedBy = userId;
        ActedAt = at;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}

/// <summary>Chọn các cấp phải duyệt cho một phiếu (design.md mục 6.2: duyệt theo ngưỡng giá trị).</summary>
public static class ApprovalPlan
{
    /// <summary>
    /// Các cấp phải qua theo tổng giá trị: mọi cấp có ngưỡng ≤ tổng, theo thứ tự cấp. Công ty chưa cấu hình quy trình cho loại
    /// phiếu → một cấp, ai có quyền duyệt cũng được (RoleId null). Có cấu hình mà tổng dưới mọi ngưỡng → không cần duyệt (danh
    /// sách rỗng, phiếu được duyệt ngay khi gửi).
    /// </summary>
    /// <param name="rules">Các cấp đã cấu hình cho công ty × loại phiếu.</param>
    /// <param name="total">Tổng giá trị phiếu.</param>
    /// <returns>(Cấp, vai trò) theo thứ tự duyệt.</returns>
    public static IReadOnlyList<(int Level, Guid? RoleId)> Levels(IReadOnlyList<ApprovalRule> rules, decimal total) =>
        rules.Count == 0
            ? [(1, null)]
            : rules.Where(r => r.MinAmount <= total).OrderBy(r => r.Level).Select(r => (r.Level, (Guid?)r.RoleId)).ToList();
}
