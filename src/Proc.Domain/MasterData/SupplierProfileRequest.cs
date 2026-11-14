using System.Text.Json;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Trạng thái đề nghị cập nhật hồ sơ NCC gửi từ Portal (lát P2a, design.md 19.5).</summary>
public enum SupplierProfileRequestStatus
{
    /// <summary>NCC đã gửi, chờ Phòng CUVT xử lý.</summary>
    Submitted = 1,

    /// <summary>Phòng CUVT chấp nhận – thông tin chung, người liên hệ đã áp vào NCC; tài khoản ngân hàng chuyển sang yêu cầu duyệt NCC.</summary>
    Accepted = 2,

    /// <summary>Phòng CUVT từ chối (có lý do).</summary>
    Rejected = 3,

    /// <summary>NCC rút lại khi còn chờ.</summary>
    Withdrawn = 4,
}

/// <summary>
/// Thông tin chung NCC đề nghị trên Portal – chỉ các trường NCC tự khai (lát P2a P2-2). **Mã số thuế** (người dùng chốt 14/11/2026 – chỉ
/// Phòng CUVT sửa trong phần mềm), nhóm NCC, điều khoản thanh toán, trạng thái, xếp hạng, NCC nước ngoài do Phòng CUVT quản lý nên không có ở đây.
/// </summary>
/// <param name="Name">Tên NCC.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="Website">Website.</param>
/// <param name="SupplyScope">Phạm vi cung cấp.</param>
public sealed record SupplierProfileData(
    string Name, string? ShortName, string? Address, string? Phone, string? Email, string? Website, string? SupplyScope);

/// <summary>Một trường thông tin chung NCC đề nghị đổi: giá trị lúc gửi và giá trị đề nghị.</summary>
/// <param name="Field">Tên trường (tên thuộc tính của <see cref="Supplier"/>, ví dụ "Address").</param>
/// <param name="Label">Nhãn tiếng Việt.</param>
/// <param name="Old">Giá trị trên NCC lúc gửi.</param>
/// <param name="New">Giá trị đề nghị (null = xóa trắng).</param>
public sealed record SupplierProfileFieldChange(string Field, string Label, string? Old, string? New);

/// <summary>
/// Đề nghị cập nhật hồ sơ NCC do quản trị NCC gửi trên Portal (lát P2a, design.md 19.5 P2-1…P2-6). Không thuộc công ty nào (NCC là danh mục
/// dùng chung); Phòng CUVT ở tổng công ty xem so sánh rồi chấp nhận hoặc từ chối. Chỉ lưu phần NCC đổi so với lúc gửi: khi chấp nhận chỉ các
/// trường đó được ghi vào NCC – thay đổi Phòng CUVT tự làm trên trường khác trong lúc chờ không bị ghi đè.
/// </summary>
[Auditable("Đề nghị cập nhật hồ sơ NCC", LabelProperty = nameof(No))]
public sealed class SupplierProfileRequest : AuditableEntity, IVersioned, IHasDocumentNumber
{
    /// <summary>Độ dài tối đa của ghi chú, lý do.</summary>
    public const int NoteMaxLength = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SupplierProfileRequest()
    {
    }

    /// <summary>Phiên bản dòng (cột hệ thống xmin của PostgreSQL) – chống hai người cùng xử lý (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    /// <summary>Số đề nghị: mã NCC + "-HS" + số thứ tự, ví dụ NCC0012-HS01.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SupplierProfileRequestStatus Status { get; private set; } = SupplierProfileRequestStatus.Submitted;

    /// <summary>Thông tin chung đề nghị đổi (JSON danh sách <see cref="SupplierProfileFieldChange"/>; rỗng = không đổi thông tin chung).</summary>
    public string FieldChangesJson { get; private set; } = "[]";

    /// <summary>Danh sách người liên hệ đề nghị (JSON); null = không đổi người liên hệ.</summary>
    public string? ContactsJson { get; private set; }

    /// <summary>Danh sách tài khoản ngân hàng đề nghị (JSON); null = không đổi tài khoản.</summary>
    public string? BankAccountsJson { get; private set; }

    /// <summary>Ghi chú của NCC.</summary>
    public string? Note { get; private set; }

    /// <summary>Thời điểm NCC gửi (UTC).</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Tài khoản Portal gửi.</summary>
    public Guid PortalUserId { get; private set; }

    /// <summary>Email tài khoản Portal gửi (giữ lại dù tài khoản đổi sau này).</summary>
    public string SubmittedByEmail { get; private set; } = string.Empty;

    /// <summary>Người nội bộ xử lý (chấp nhận / từ chối).</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>Thời điểm xử lý hoặc NCC rút (UTC).</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Ý kiến Phòng CUVT khi chấp nhận, lý do khi từ chối, hoặc lý do NCC rút.</summary>
    public string? ReviewComment { get; private set; }

    /// <summary>Số yêu cầu duyệt NCC (đổi tài khoản ngân hàng) lập khi chấp nhận – phần tài khoản theo quy tắc Q7, mục 18.6.</summary>
    public string? BankChangeRequestNo { get; private set; }

    /// <summary>Thông tin chung đề nghị đổi.</summary>
    public IReadOnlyList<SupplierProfileFieldChange> FieldChanges =>
        JsonSerializer.Deserialize<List<SupplierProfileFieldChange>>(FieldChangesJson, Json) ?? [];

    /// <summary>Người liên hệ đề nghị; null = không đổi.</summary>
    public IReadOnlyList<SupplierContactData>? ProposedContacts =>
        ContactsJson is null ? null : JsonSerializer.Deserialize<List<SupplierContactData>>(ContactsJson, Json) ?? [];

    /// <summary>Tài khoản ngân hàng đề nghị; null = không đổi.</summary>
    public IReadOnlyList<SupplierBankAccountData>? ProposedBankAccounts =>
        BankAccountsJson is null ? null : JsonSerializer.Deserialize<List<SupplierBankAccountData>>(BankAccountsJson, Json) ?? [];

    /// <summary>
    /// NCC gửi đề nghị cập nhật hồ sơ (P2-2, P2-3): chỉ lưu phần khác hồ sơ hiện tại; không có gì khác thì báo lỗi.
    /// </summary>
    /// <param name="supplier">NCC của phiên Portal.</param>
    /// <param name="no">Số đề nghị.</param>
    /// <param name="portalUserId">Tài khoản Portal gửi.</param>
    /// <param name="email">Email tài khoản gửi.</param>
    /// <param name="data">Thông tin chung đề nghị.</param>
    /// <param name="contacts">Người liên hệ đề nghị (đủ danh sách).</param>
    /// <param name="bankAccounts">Tài khoản ngân hàng đề nghị (đủ danh sách).</param>
    /// <param name="note">Ghi chú.</param>
    /// <param name="at">Thời điểm gửi (UTC).</param>
    /// <returns>Đề nghị mới ở trạng thái chờ xử lý.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu không hợp lệ hoặc không có thay đổi.</exception>
    public static SupplierProfileRequest Create(
        Supplier supplier, string no, Guid portalUserId, string email, SupplierProfileData data, IEnumerable<SupplierContactData> contacts,
        IEnumerable<SupplierBankAccountData> bankAccounts, string? note, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Đề nghị phải có số.");
        var name = Limit(data.Name, 300, "Tên công ty") ?? throw new BusinessRuleException("Tên công ty không được để trống.");
        var proposed = new (string Field, string Label, string? Old, string? New)[]
        {
            (nameof(Supplier.Name), "Tên công ty", supplier.Name, name),
            (nameof(Supplier.ShortName), "Tên viết tắt", supplier.ShortName, Limit(data.ShortName, 100, "Tên viết tắt")),
            (nameof(Supplier.Address), "Địa chỉ", supplier.Address, Limit(data.Address, 500, "Địa chỉ")),
            (nameof(Supplier.Phone), "Điện thoại", supplier.Phone, Limit(data.Phone, 50, "Điện thoại")),
            (nameof(Supplier.Email), "Email", supplier.Email, Limit(data.Email, 256, "Email")),
            (nameof(Supplier.Website), "Website", supplier.Website, Limit(data.Website, 200, "Website")),
            (nameof(Supplier.SupplyScope), "Phạm vi cung cấp", supplier.SupplyScope, Limit(data.SupplyScope, 500, "Phạm vi cung cấp")),
        };
        var changes = proposed.Where(p => !string.Equals(p.Old, p.New, StringComparison.Ordinal))
            .Select(p => new SupplierProfileFieldChange(p.Field, p.Label, p.Old, p.New))
            .ToList();
        var contactList = Supplier.NormalizeContacts(contacts ?? []);
        var bankList = Supplier.NormalizeBankAccounts(bankAccounts ?? []);
        var contactsChanged = supplier.ContactsDifferFrom(contactList);
        var banksChanged = supplier.BankAccountsDifferFrom(bankList);
        if (changes.Count == 0 && !contactsChanged && !banksChanged)
            throw new BusinessRuleException("Hồ sơ đề nghị không khác hồ sơ hiện tại – sửa thông tin cần cập nhật rồi gửi lại.");
        return new SupplierProfileRequest
        {
            No = no,
            SupplierId = supplier.Id,
            FieldChangesJson = JsonSerializer.Serialize(changes, Json),
            ContactsJson = contactsChanged ? JsonSerializer.Serialize(contactList, Json) : null,
            BankAccountsJson = banksChanged ? JsonSerializer.Serialize(bankList, Json) : null,
            Note = Limit(note, NoteMaxLength, "Ghi chú"),
            SubmittedAt = at,
            PortalUserId = portalUserId,
            SubmittedByEmail = email,
        };
    }

    /// <summary>
    /// Phòng CUVT chấp nhận (P2-4): ghi các trường thông tin chung NCC đề nghị đổi và người liên hệ vào NCC. Tài khoản ngân hàng không áp ở
    /// đây – người gọi lập yêu cầu duyệt NCC rồi ghi số qua <see cref="LinkBankChangeRequest"/>.
    /// </summary>
    /// <param name="supplier">NCC (đang theo dõi thay đổi).</param>
    /// <param name="userId">Người xử lý.</param>
    /// <param name="comment">Ý kiến (không bắt buộc).</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi đề nghị không còn chờ, NCC không khớp hoặc dữ liệu không còn hợp lệ.</exception>
    public void Accept(Supplier supplier, Guid userId, string? comment, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        EnsureSubmitted();
        if (supplier.Id != SupplierId) throw new BusinessRuleException("Đề nghị không thuộc NCC này.");
        var changes = FieldChanges.ToDictionary(c => c.Field, c => c.New, StringComparer.Ordinal);
        string? Pick(string field, string? current) => changes.TryGetValue(field, out var value) ? value : current;
        if (changes.Count > 0)
        {
            supplier.Update(new SupplierInfo(
                Pick(nameof(Supplier.Name), supplier.Name) ?? supplier.Name, Pick(nameof(Supplier.ShortName), supplier.ShortName), supplier.IsForeign,
                supplier.TaxCode, Pick(nameof(Supplier.Address), supplier.Address),
                Pick(nameof(Supplier.Phone), supplier.Phone), Pick(nameof(Supplier.Email), supplier.Email),
                Pick(nameof(Supplier.Website), supplier.Website), supplier.PaymentTerms, Pick(nameof(Supplier.SupplyScope), supplier.SupplyScope),
                supplier.Rating));
        }

        if (ProposedContacts is { } contacts) supplier.ReplaceContacts(contacts);
        Status = SupplierProfileRequestStatus.Accepted;
        ReviewedBy = userId;
        ClosedAt = at;
        ReviewComment = Limit(comment, NoteMaxLength, "Ý kiến");
    }

    /// <summary>Ghi số yêu cầu duyệt NCC (đổi tài khoản ngân hàng) lập khi chấp nhận.</summary>
    /// <param name="no">Số yêu cầu.</param>
    public void LinkBankChangeRequest(string no) => BankChangeRequestNo = no;

    /// <summary>Phòng CUVT từ chối (P2-4): bắt buộc lý do để NCC biết sửa gì.</summary>
    /// <param name="userId">Người xử lý.</param>
    /// <param name="reason">Lý do.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi đề nghị không còn chờ hoặc thiếu lý do.</exception>
    public void Reject(Guid userId, string? reason, DateTimeOffset at)
    {
        EnsureSubmitted();
        ReviewComment = Limit(reason, NoteMaxLength, "Lý do từ chối")
            ?? throw new BusinessRuleException("Nhập lý do từ chối để NCC biết cần sửa gì.");
        Status = SupplierProfileRequestStatus.Rejected;
        ReviewedBy = userId;
        ClosedAt = at;
    }

    /// <summary>NCC rút đề nghị còn chờ (P2-3).</summary>
    /// <param name="reason">Lý do (không bắt buộc).</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi đề nghị không còn chờ.</exception>
    public void Withdraw(string? reason, DateTimeOffset at)
    {
        EnsureSubmitted();
        ReviewComment = Limit(reason, NoteMaxLength, "Lý do rút");
        Status = SupplierProfileRequestStatus.Withdrawn;
        ClosedAt = at;
    }

    /// <summary>Chặn thao tác khi đề nghị đã xử lý hoặc đã rút – đề nghị đóng là khóa (design.md mục 20 R1).</summary>
    private void EnsureSubmitted()
    {
        if (Status != SupplierProfileRequestStatus.Submitted)
            throw new BusinessRuleException($"Đề nghị {No} đã được xử lý hoặc đã rút – không thao tác được nữa.");
    }

    /// <summary>Cắt khoảng trắng; chuỗi rỗng thành null; quá dài thì báo lỗi theo tên ô.</summary>
    private static string? Limit(string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > max) throw new BusinessRuleException($"{label} tối đa {max} ký tự.");
        return text;
    }
}
