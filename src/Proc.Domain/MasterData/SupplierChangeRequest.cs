using System.Text.Json;
using Proc.Domain.Common;
using Proc.Domain.Purchasing;

namespace Proc.Domain.MasterData;

/// <summary>Loại thay đổi hồ sơ NCC phải qua duyệt (design.md mục 18.6, quyết định Q7).</summary>
public enum SupplierChangeKind
{
    /// <summary>Đưa NCC vào Đã duyệt hoặc Danh sách đen.</summary>
    Status = 1,

    /// <summary>Thêm, sửa, bỏ tài khoản ngân hàng.</summary>
    BankAccounts = 2,
}

/// <summary>Trạng thái yêu cầu duyệt NCC.</summary>
public enum SupplierChangeRequestStatus
{
    /// <summary>Đang chờ duyệt (công ty liên quan, rồi Tổng công ty).</summary>
    Submitted = 2,

    /// <summary>Đã duyệt đủ – thay đổi đã áp vào NCC.</summary>
    Approved = 3,

    /// <summary>Bị từ chối ở một công ty bất kỳ – kết thúc, muốn đổi thì lập yêu cầu mới.</summary>
    Rejected = 4,

    /// <summary>Người lập rút lại khi còn chờ duyệt.</summary>
    Cancelled = 5,
}

/// <summary>
/// Yêu cầu thay đổi hồ sơ NCC chờ duyệt (design.md mục 18.6): đưa vào Đã duyệt/Danh sách đen, đổi tài khoản ngân hàng. NCC là danh mục
/// dùng chung nên yêu cầu không thuộc riêng công ty nào (không lọc theo công ty) – ai có quyền xem NCC đều xem được; việc duyệt đi hai chặng:
/// các công ty liên quan (đang mua của NCC) duyệt xong, rồi Tổng công ty duyệt lần cuối. Duyệt xong mới áp thay đổi vào NCC.
/// </summary>
[Auditable("Yêu cầu duyệt NCC", LabelProperty = nameof(No))]
public sealed class SupplierChangeRequest : AuditableEntity, IApprovalSubject, IVersioned
{
    /// <summary>Phiên bản dòng (cột hệ thống xmin của PostgreSQL) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SupplierChangeRequest()
    {
    }

    /// <summary>Công ty người lập đang làm việc khi lập yêu cầu (để ghi nhận, không dùng lọc).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số yêu cầu: mã NCC + số thứ tự, ví dụ NCC0012-03.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Loại thay đổi.</summary>
    public SupplierChangeKind Kind { get; private set; }

    /// <summary>Trạng thái NCC đề nghị (loại <see cref="SupplierChangeKind.Status"/>).</summary>
    public SupplierStatus? NewStatus { get; private set; }

    /// <summary>Lý do / ghi chú trạng thái đề nghị.</summary>
    public string? StatusNote { get; private set; }

    /// <summary>Danh sách tài khoản ngân hàng đề nghị (JSON, loại <see cref="SupplierChangeKind.BankAccounts"/>).</summary>
    public string? BankAccountsJson { get; private set; }

    /// <summary>Tóm tắt giá trị hiện tại lúc lập, ví dụ "Tiềm năng" hoặc "VCB 0011…" – để người duyệt so sánh.</summary>
    public string CurrentSummary { get; private set; } = string.Empty;

    /// <summary>Tóm tắt giá trị đề nghị.</summary>
    public string ProposedSummary { get; private set; } = string.Empty;

    /// <summary>Trạng thái yêu cầu.</summary>
    public SupplierChangeRequestStatus Status { get; private set; } = SupplierChangeRequestStatus.Submitted;

    /// <inheritdoc />
    public int ApprovalRound { get; private set; }

    /// <summary>Lý do từ chối.</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Thời điểm duyệt đủ (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <inheritdoc />
    public string DocType => ApprovalTypes.Supplier;

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> RelatedSupplierIds => [SupplierId];

    /// <inheritdoc />
    public decimal ApprovalAmount => 0;

    /// <inheritdoc />
    public bool IsAwaitingApproval => Status == SupplierChangeRequestStatus.Submitted;

    /// <summary>Tài khoản đề nghị.</summary>
    public IReadOnlyList<SupplierBankAccountData> ProposedBankAccounts =>
        BankAccountsJson is null ? [] : JsonSerializer.Deserialize<List<SupplierBankAccountData>>(BankAccountsJson, Json) ?? [];

    /// <summary>Lập yêu cầu đổi trạng thái (Đã duyệt / Danh sách đen).</summary>
    /// <exception cref="BusinessRuleException">Khi trạng thái không cần duyệt hoặc thiếu lý do danh sách đen.</exception>
    public static SupplierChangeRequest ForStatus(Supplier supplier, Guid companyId, string no, SupplierStatus status, string? note)
    {
        if (!supplier.StatusChangeNeedsApproval(status))
            throw new BusinessRuleException("Chỉ đưa NCC vào Đã duyệt hoặc Danh sách đen mới cần lập yêu cầu duyệt.");
        var cleaned = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (status == SupplierStatus.Blacklisted && cleaned is null) throw new BusinessRuleException("Đưa NCC vào danh sách đen phải ghi lý do.");
        if (cleaned?.Length > 500) throw new BusinessRuleException("Lý do tối đa 500 ký tự.");
        return new SupplierChangeRequest
        {
            CompanyId = companyId, No = no, SupplierId = supplier.Id, Kind = SupplierChangeKind.Status, NewStatus = status, StatusNote = cleaned,
            CurrentSummary = StatusName(supplier.Status), ProposedSummary = StatusName(status) + (cleaned is null ? string.Empty : $" – {cleaned}"),
        };
    }

    /// <summary>Lập yêu cầu đổi tài khoản ngân hàng.</summary>
    /// <exception cref="BusinessRuleException">Khi tài khoản không hợp lệ hoặc không có gì thay đổi.</exception>
    public static SupplierChangeRequest ForBankAccounts(Supplier supplier, Guid companyId, string no, IEnumerable<SupplierBankAccountData> accounts)
    {
        var list = Supplier.NormalizeBankAccounts(accounts);
        if (!supplier.BankAccountsDifferFrom(list)) throw new BusinessRuleException("Tài khoản ngân hàng không thay đổi.");
        return new SupplierChangeRequest
        {
            CompanyId = companyId, No = no, SupplierId = supplier.Id, Kind = SupplierChangeKind.BankAccounts,
            BankAccountsJson = JsonSerializer.Serialize(list, Json),
            CurrentSummary = BankSummary(supplier.BankAccounts.Select(b => (b.BankName, b.AccountNumber, b.IsDefault))),
            ProposedSummary = BankSummary(list.Select(b => (b.BankName, b.AccountNumber, b.IsDefault))),
        };
    }

    /// <inheritdoc />
    /// <remarks>Yêu cầu được gửi duyệt ngay khi lập, chỉ một lượt (từ chối là kết thúc).</remarks>
    public int Submit()
    {
        if (ApprovalRound > 0) throw new BusinessRuleException($"Yêu cầu {No} đã gửi duyệt.");
        Status = SupplierChangeRequestStatus.Submitted;
        return ++ApprovalRound;
    }

    /// <inheritdoc />
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureSubmitted();
        Status = SupplierChangeRequestStatus.Approved;
        ApprovedAt = at;
    }

    /// <inheritdoc />
    /// <remarks>Yêu cầu NCC không về nháp: bị từ chối là kết thúc (design.md mục 18.6).</remarks>
    public void ReturnToDraft(string? reason)
    {
        EnsureSubmitted();
        Status = SupplierChangeRequestStatus.Rejected;
        RejectReason = reason;
    }

    /// <summary>Người lập rút yêu cầu còn chờ duyệt.</summary>
    /// <exception cref="BusinessRuleException">Khi yêu cầu không còn chờ duyệt.</exception>
    public void Cancel()
    {
        EnsureSubmitted();
        Status = SupplierChangeRequestStatus.Cancelled;
    }

    /// <summary>Áp thay đổi đã duyệt vào NCC (gọi cùng lúc với <see cref="MarkApproved"/>).</summary>
    /// <exception cref="BusinessRuleException">Khi NCC không khớp hoặc yêu cầu chưa duyệt.</exception>
    public void ApplyTo(Supplier supplier)
    {
        if (supplier.Id != SupplierId) throw new BusinessRuleException("Yêu cầu không thuộc NCC này.");
        if (Status != SupplierChangeRequestStatus.Approved) throw new BusinessRuleException($"Yêu cầu {No} chưa được duyệt.");
        if (Kind == SupplierChangeKind.Status) supplier.SetStatus(NewStatus!.Value, StatusNote);
        else supplier.ReplaceBankAccounts(ProposedBankAccounts);
    }

    /// <summary>Chặn thao tác khi yêu cầu sửa NCC không ở trạng thái Chờ duyệt.</summary>
    private void EnsureSubmitted()
    {
        if (Status != SupplierChangeRequestStatus.Submitted) throw new BusinessRuleException($"Yêu cầu {No} không ở trạng thái chờ duyệt.");
    }

    /// <summary>Tên trạng thái NCC tiếng Việt.</summary>
    public static string StatusName(SupplierStatus status) => status switch
    {
        SupplierStatus.Potential => "Tiềm năng",
        SupplierStatus.Approved => "Đã duyệt",
        SupplierStatus.Suspended => "Tạm ngưng",
        _ => "Danh sách đen",
    };

    /// <summary>Tóm tắt danh sách tài khoản ngân hàng thành một dòng chữ để so sánh trước/sau trong yêu cầu sửa.</summary>
    private static string BankSummary(IEnumerable<(string Bank, string Number, bool IsDefault)> accounts)
    {
        var text = string.Join("; ", accounts.Select(a => $"{a.Bank} {a.Number}{(a.IsDefault ? " (mặc định)" : string.Empty)}"));
        return text.Length == 0 ? "(không có tài khoản)" : text.Length > 1000 ? text[..1000] : text;
    }
}
