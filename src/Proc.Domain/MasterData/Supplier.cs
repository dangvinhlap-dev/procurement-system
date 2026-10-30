using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// Nhóm nhà cung cấp dùng chung: trong nước, nhập khẩu, nội bộ… (design.md mục 5.3).
/// </summary>
[Auditable("Nhóm NCC", LabelProperty = nameof(Code))]
public sealed class SupplierGroup : AuditableEntity
{
    private SupplierGroup()
    {
    }

    /// <summary>Mã nhóm, duy nhất.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên nhóm.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Còn sử dụng.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo nhóm NCC.
    /// </summary>
    /// <param name="code">Mã (2–20 chữ không dấu hoặc số).</param>
    /// <param name="name">Tên.</param>
    /// <returns>Nhóm mới.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static SupplierGroup Create(string code, string name)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsAlphanumeric(normalized, 2, 20))
            throw new BusinessRuleException("Mã nhóm NCC gồm 2–20 ký tự chữ không dấu (A–Z) hoặc số.");
        var group = new SupplierGroup { Code = normalized };
        group.Rename(name);
        return group;
    }

    /// <summary>
    /// Đổi tên.
    /// </summary>
    /// <param name="name">Tên mới.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Tên nhóm NCC không được để trống.");
        Name = name.Trim();
    }

    /// <summary>Ngừng dùng (kiểm tra còn NCC trong nhóm do tầng Infrastructure làm).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Dùng lại.</summary>
    public void Activate() => IsActive = true;
}

/// <summary>Trạng thái nhà cung cấp (design.md mục 5.3).</summary>
public enum SupplierStatus
{
    /// <summary>Tiềm năng: mới tìm hiểu, được mời báo giá nhưng chưa được đặt hàng.</summary>
    Potential = 1,

    /// <summary>Đã duyệt: được mua hàng.</summary>
    Approved = 2,

    /// <summary>Tạm ngưng: tạm không giao dịch (ví dụ đang xử lý khiếu nại chất lượng).</summary>
    Suspended = 3,

    /// <summary>Danh sách đen: không bao giờ giao dịch nữa.</summary>
    Blacklisted = 4,
}

/// <summary>Thông tin chung của NCC, gom lại để hàm sửa không có quá nhiều tham số.</summary>
/// <param name="Name">Tên đầy đủ, bắt buộc.</param>
/// <param name="ShortName">Tên viết tắt.</param>
/// <param name="IsForeign">NCC nước ngoài (không có mã số thuế Việt Nam).</param>
/// <param name="TaxCode">Mã số thuế (trong nước: 10 hoặc 10-3 chữ số; nước ngoài: mã thuế nước sở tại).</param>
/// <param name="Address">Địa chỉ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email chung.</param>
/// <param name="Website">Website.</param>
/// <param name="PaymentTerms">Điều khoản thanh toán, ví dụ "Chuyển khoản 30 ngày sau khi nhận hàng".</param>
/// <param name="SupplyScope">Ngành hàng cung cấp.</param>
/// <param name="Rating">Xếp hạng 1–5 sao; trống = chưa đánh giá.</param>
public sealed record SupplierInfo(
    string Name, string? ShortName, bool IsForeign, string? TaxCode, string? Address, string? Phone, string? Email,
    string? Website, string? PaymentTerms, string? SupplyScope, int? Rating);

/// <summary>Người liên hệ gửi vào khi lưu; Id trống = người mới.</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="Name">Họ tên, bắt buộc.</param>
/// <param name="Position">Chức vụ.</param>
/// <param name="Phone">Điện thoại.</param>
/// <param name="Email">Email.</param>
/// <param name="IsPrimary">Người liên hệ chính (tối đa một).</param>
public sealed record SupplierContactData(Guid? Id, string Name, string? Position, string? Phone, string? Email, bool IsPrimary);

/// <summary>Tài khoản ngân hàng gửi vào khi lưu; Id trống = tài khoản mới.</summary>
/// <param name="Id">Id dòng đã có.</param>
/// <param name="BankName">Tên ngân hàng, bắt buộc.</param>
/// <param name="Branch">Chi nhánh.</param>
/// <param name="AccountNumber">Số tài khoản, bắt buộc.</param>
/// <param name="AccountName">Chủ tài khoản.</param>
/// <param name="IsDefault">Tài khoản mặc định khi thanh toán (tối đa một).</param>
public sealed record SupplierBankAccountData(
    Guid? Id, string BankName, string? Branch, string AccountNumber, string? AccountName, bool IsDefault);

/// <summary>
/// Nhà cung cấp dùng chung toàn tập đoàn (design.md mục 5.3): một NCC một mã để so sánh giá giữa các công ty.
/// Công ty thành viên cũng là NCC nội bộ, liên kết qua <see cref="InternalCompanyId"/>.
/// </summary>
[Auditable("Nhà cung cấp", LabelProperty = nameof(Code), Ignore = [nameof(SearchText)])]
public sealed partial class Supplier : AuditableEntity
{
    /// <summary>Độ dài tối đa của mã NCC.</summary>
    public const int CodeMaxLength = 30;

    private readonly List<SupplierContact> _contacts = [];
    private readonly List<SupplierBankAccount> _bankAccounts = [];

    private Supplier()
    {
    }

    /// <summary>Mã NCC, duy nhất, không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên đầy đủ.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Tên viết tắt.</summary>
    public string? ShortName { get; private set; }

    /// <summary>Nhóm NCC.</summary>
    public Guid GroupId { get; private set; }

    /// <summary>NCC nước ngoài.</summary>
    public bool IsForeign { get; private set; }

    /// <summary>Mã số thuế, duy nhất (design.md mục 5.3).</summary>
    public string? TaxCode { get; private set; }

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; private set; }

    /// <summary>Điện thoại.</summary>
    public string? Phone { get; private set; }

    /// <summary>Email chung.</summary>
    public string? Email { get; private set; }

    /// <summary>Website.</summary>
    public string? Website { get; private set; }

    /// <summary>Điều khoản thanh toán (chữ tự do cho tới khi có danh mục điều khoản thanh toán – mục 5.4).</summary>
    public string? PaymentTerms { get; private set; }

    /// <summary>Ngành hàng cung cấp.</summary>
    public string? SupplyScope { get; private set; }

    /// <summary>Xếp hạng 1–5 sao.</summary>
    public int? Rating { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SupplierStatus Status { get; private set; } = SupplierStatus.Potential;

    /// <summary>Lý do tạm ngưng / đưa vào danh sách đen.</summary>
    public string? StatusNote { get; private set; }

    /// <summary>Công ty thành viên mà NCC này đại diện (NCC nội bộ, dùng cho mua bán liên công ty ICS/ICR); mỗi công ty tối đa một.</summary>
    public Guid? InternalCompanyId { get; private set; }

    /// <summary>Chuỗi tìm kiếm không dấu từ mã, tên, tên viết tắt, mã số thuế, điện thoại.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>Người liên hệ.</summary>
    public IReadOnlyList<SupplierContact> Contacts => _contacts;

    /// <summary>Tài khoản ngân hàng.</summary>
    public IReadOnlyList<SupplierBankAccount> BankAccounts => _bankAccounts;

    /// <summary>
    /// Tạo NCC ở trạng thái Tiềm năng.
    /// </summary>
    /// <param name="code">Mã: 2–30 ký tự chữ không dấu, số và - . _ /.</param>
    /// <param name="group">Nhóm NCC đang dùng.</param>
    /// <param name="info">Thông tin chung.</param>
    /// <returns>NCC mới.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu không hợp lệ.</exception>
    public static Supplier Create(string code, SupplierGroup group, SupplierInfo info)
    {
        var normalized = CodeRules.Normalize(code);
        if (!CodeRules.IsExtendedCode(normalized, 2, CodeMaxLength))
            throw new BusinessRuleException($"Mã NCC gồm 2–{CodeMaxLength} ký tự chữ không dấu (A–Z), số hoặc - . _ /.");
        var supplier = new Supplier { Code = normalized };
        supplier.ChangeGroup(group);
        supplier.Update(info);
        return supplier;
    }

    /// <summary>
    /// Sửa thông tin chung.
    /// </summary>
    /// <param name="info">Thông tin mới.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng, mã số thuế sai định dạng hoặc xếp hạng ngoài 1–5.</exception>
    public void Update(SupplierInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (string.IsNullOrWhiteSpace(info.Name)) throw new BusinessRuleException("Tên NCC không được để trống.");
        if (info.Rating is < 1 or > 5) throw new BusinessRuleException("Xếp hạng NCC từ 1 đến 5 sao.");

        var taxCode = Clean(info.TaxCode)?.ToUpperInvariant();
        // NCC trong nước theo định dạng MST Việt Nam; NCC nước ngoài dùng mã thuế nước sở tại nên chỉ giới hạn ký tự.
        if (taxCode is not null && !info.IsForeign && !VietnamTaxCodeRegex().IsMatch(taxCode))
            throw new BusinessRuleException("Mã số thuế gồm 10 chữ số, hoặc 10 chữ số + \"-\" + 3 chữ số (ví dụ 0101234567-001).");
        if (taxCode is not null && info.IsForeign && !CodeRules.IsExtendedCode(taxCode, 3, 20))
            throw new BusinessRuleException("Mã thuế NCC nước ngoài gồm 3–20 ký tự chữ không dấu, số hoặc - . _ /.");

        Name = info.Name.Trim();
        ShortName = Clean(info.ShortName);
        IsForeign = info.IsForeign;
        TaxCode = taxCode;
        Address = Clean(info.Address);
        Phone = Clean(info.Phone);
        Email = Clean(info.Email);
        Website = Clean(info.Website);
        PaymentTerms = Clean(info.PaymentTerms);
        SupplyScope = Clean(info.SupplyScope);
        Rating = info.Rating;
        SearchText = Common.SearchText.Combine(Code, Name, ShortName, TaxCode, Phone);
    }

    /// <summary>
    /// Đổi nhóm NCC.
    /// </summary>
    /// <param name="group">Nhóm mới, đang dùng.</param>
    /// <exception cref="BusinessRuleException">Khi nhóm đã ngừng dùng.</exception>
    public void ChangeGroup(SupplierGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!group.IsActive && group.Id != GroupId) throw new BusinessRuleException($"Nhóm NCC {group.Code} đã ngừng dùng.");
        GroupId = group.Id;
    }

    /// <summary>
    /// Đổi trạng thái. Tạm ngưng hay đưa vào danh sách đen phải ghi lý do – để người mua sau biết vì sao không đặt hàng được.
    /// </summary>
    /// <param name="status">Trạng thái mới.</param>
    /// <param name="note">Lý do (bắt buộc với Tạm ngưng, Danh sách đen).</param>
    /// <exception cref="BusinessRuleException">Khi trạng thái không hợp lệ hoặc thiếu lý do.</exception>
    public void SetStatus(SupplierStatus status, string? note)
    {
        if (!Enum.IsDefined(status)) throw new BusinessRuleException("Trạng thái NCC không hợp lệ.");
        var cleaned = Clean(note);
        if (status is SupplierStatus.Suspended or SupplierStatus.Blacklisted && cleaned is null)
            throw new BusinessRuleException("Tạm ngưng hoặc đưa NCC vào danh sách đen phải ghi lý do.");
        Status = status;
        StatusNote = cleaned;
    }

    /// <summary>
    /// Gắn NCC với công ty thành viên (NCC nội bộ) hoặc bỏ gắn.
    /// </summary>
    /// <param name="companyId">Công ty; null = NCC bên ngoài.</param>
    public void LinkInternalCompany(Guid? companyId) => InternalCompanyId = companyId;

    /// <summary>
    /// Thay danh sách người liên hệ: dòng có Id thì sửa, không có Id thì thêm, dòng cũ không còn trong danh sách thì xóa.
    /// Sửa theo Id (không xóa hết rồi thêm lại) để nhật ký thao tác chỉ ghi dòng thật sự thay đổi.
    /// </summary>
    /// <param name="contacts">Danh sách mới.</param>
    /// <exception cref="BusinessRuleException">Khi thiếu họ tên, có hơn một người liên hệ chính, hoặc Id không thuộc NCC này.</exception>
    public void SetContacts(IEnumerable<SupplierContactData> contacts)
    {
        var list = contacts.ToList();
        if (list.Any(c => string.IsNullOrWhiteSpace(c.Name))) throw new BusinessRuleException("Người liên hệ phải có họ tên.");
        if (list.Count(c => c.IsPrimary) > 1) throw new BusinessRuleException("Chỉ chọn một người liên hệ chính.");
        Merge(_contacts, list, c => c.Id, d => d.Id, d => new SupplierContact(Id), (row, d) => row.Set(d), "người liên hệ");
    }

    /// <summary>
    /// Thay danh sách tài khoản ngân hàng (cùng cách sửa theo Id như người liên hệ).
    /// </summary>
    /// <param name="accounts">Danh sách mới.</param>
    /// <exception cref="BusinessRuleException">Khi thiếu ngân hàng/số tài khoản, trùng số tài khoản, hơn một tài khoản mặc định.</exception>
    public void SetBankAccounts(IEnumerable<SupplierBankAccountData> accounts)
    {
        var list = accounts
            .Select(a => a with { AccountNumber = (a.AccountNumber ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant() })
            .ToList();
        if (list.Any(a => string.IsNullOrWhiteSpace(a.BankName))) throw new BusinessRuleException("Tài khoản ngân hàng phải có tên ngân hàng.");
        if (list.Any(a => !BankAccountRegex().IsMatch(a.AccountNumber)))
            throw new BusinessRuleException("Số tài khoản gồm 4–30 chữ số hoặc chữ không dấu.");
        var duplicate = list.GroupBy(a => (Bank: a.BankName.Trim().ToUpperInvariant(), a.AccountNumber)).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessRuleException($"Số tài khoản {duplicate.Key.AccountNumber} bị khai báo hai lần.");
        if (list.Count(a => a.IsDefault) > 1) throw new BusinessRuleException("Chỉ chọn một tài khoản mặc định.");
        Merge(_bankAccounts, list, a => a.Id, d => d.Id, d => new SupplierBankAccount(Id), (row, d) => row.Set(d), "tài khoản ngân hàng");
    }

    private static void Merge<TRow, TData>(
        List<TRow> rows, List<TData> data, Func<TRow, Guid> rowId, Func<TData, Guid?> dataId,
        Func<TData, TRow> create, Action<TRow, TData> apply, string label)
    {
        // Giữ lại đúng các dòng được sửa hoặc vừa thêm; dòng cũ không còn trong danh sách gửi lên thì xóa.
        var keep = new List<TRow>();
        foreach (var d in data)
        {
            TRow row;
            if (dataId(d) is { } id)
            {
                row = rows.FirstOrDefault(r => rowId(r) == id)
                    ?? throw new BusinessRuleException($"Không tìm thấy {label} cần sửa – hãy tải lại NCC.");
            }
            else
            {
                row = create(d);
                rows.Add(row);
            }

            apply(row, d);
            keep.Add(row);
        }

        rows.RemoveAll(r => !keep.Contains(r));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex VietnamTaxCodeRegex();

    [GeneratedRegex("^[A-Z0-9]{4,30}$")]
    private static partial Regex BankAccountRegex();
}

/// <summary>Người liên hệ của NCC.</summary>
[Auditable("Người liên hệ NCC", LabelProperty = nameof(Name))]
public sealed class SupplierContact : Entity
{
    private SupplierContact()
    {
    }

    internal SupplierContact(Guid supplierId) => SupplierId = supplierId;

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Họ tên.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Chức vụ.</summary>
    public string? Position { get; private set; }

    /// <summary>Điện thoại.</summary>
    public string? Phone { get; private set; }

    /// <summary>Email.</summary>
    public string? Email { get; private set; }

    /// <summary>Người liên hệ chính.</summary>
    public bool IsPrimary { get; private set; }

    internal void Set(SupplierContactData d)
    {
        Name = d.Name.Trim();
        Position = string.IsNullOrWhiteSpace(d.Position) ? null : d.Position.Trim();
        Phone = string.IsNullOrWhiteSpace(d.Phone) ? null : d.Phone.Trim();
        Email = string.IsNullOrWhiteSpace(d.Email) ? null : d.Email.Trim();
        IsPrimary = d.IsPrimary;
    }
}

/// <summary>Tài khoản ngân hàng của NCC.</summary>
[Auditable("Tài khoản ngân hàng NCC", LabelProperty = nameof(AccountNumber))]
public sealed class SupplierBankAccount : Entity
{
    private SupplierBankAccount()
    {
    }

    internal SupplierBankAccount(Guid supplierId) => SupplierId = supplierId;

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Tên ngân hàng.</summary>
    public string BankName { get; private set; } = string.Empty;

    /// <summary>Chi nhánh.</summary>
    public string? Branch { get; private set; }

    /// <summary>Số tài khoản (đã bỏ khoảng trắng, chữ hoa).</summary>
    public string AccountNumber { get; private set; } = string.Empty;

    /// <summary>Chủ tài khoản.</summary>
    public string? AccountName { get; private set; }

    /// <summary>Tài khoản mặc định khi thanh toán.</summary>
    public bool IsDefault { get; private set; }

    internal void Set(SupplierBankAccountData d)
    {
        BankName = d.BankName.Trim();
        Branch = string.IsNullOrWhiteSpace(d.Branch) ? null : d.Branch.Trim();
        AccountNumber = d.AccountNumber;
        AccountName = string.IsNullOrWhiteSpace(d.AccountName) ? null : d.AccountName.Trim();
        IsDefault = d.IsDefault;
    }
}
