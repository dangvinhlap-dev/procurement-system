using System.Text.RegularExpressions;
using Proc.Domain.Common;

namespace Proc.Domain.Organization;

/// <summary>
/// Công ty – một pháp nhân độc lập (CLAUDE.md mục 4). Tổng công ty cũng là một bản ghi với IsHeadOffice = true
/// (design.md mục 5.1). Mọi bảng giao dịch đều có CompanyId trỏ về đây.
/// </summary>
[Auditable("Công ty", LabelProperty = nameof(Code))]
public sealed partial class Company : AuditableEntity
{
    /// <summary>Độ dài tối thiểu của mã công ty.</summary>
    public const int CodeMinLength = 2;

    /// <summary>Độ dài tối đa của mã công ty – giữ số chứng từ ngắn gọn.</summary>
    public const int CodeMaxLength = 10;

    private Company()
    {
    }

    /// <summary>Mã công ty, duy nhất, dùng trong số chứng từ (PO-{CTY}-{YYMM}-{0000}).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Tên đầy đủ.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Tên viết tắt hiển thị trên thanh menu, báo cáo.</summary>
    public string? ShortName { get; private set; }

    /// <summary>Mã số thuế.</summary>
    public string? TaxCode { get; private set; }

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; private set; }

    /// <summary>Người đại diện theo pháp luật.</summary>
    public string? Representative { get; private set; }

    /// <summary>true nếu là tổng công ty. Chỉ có một tổng công ty (ràng buộc duy nhất trong CSDL).</summary>
    public bool IsHeadOffice { get; private set; }

    /// <summary>Tiền tệ hạch toán, mặc định VND (design.md mục 1.3, câu 4).</summary>
    public string AccountingCurrency { get; private set; } = "VND";

    /// <summary>Ngày bắt đầu dùng hệ thống – mốc nhập số dư đầu kỳ.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>Còn sử dụng. Danh mục không xóa cứng khi đã phát sinh giao dịch (design.md mục 5).</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tạo công ty mới.
    /// </summary>
    /// <param name="code">Mã công ty; được chuẩn hóa về chữ hoa.</param>
    /// <param name="name">Tên đầy đủ.</param>
    /// <param name="isHeadOffice">Là tổng công ty.</param>
    /// <param name="shortName">Tên viết tắt (tùy chọn).</param>
    /// <returns>Công ty mới, đang hoạt động, tiền hạch toán VND.</returns>
    /// <exception cref="BusinessRuleException">Khi mã hoặc tên không hợp lệ.</exception>
    public static Company Create(string code, string name, bool isHeadOffice, string? shortName = null)
    {
        var company = new Company { IsHeadOffice = isHeadOffice };
        company.SetCode(code);
        company.Rename(name, shortName);
        return company;
    }

    /// <summary>
    /// Đổi tên và tên viết tắt.
    /// </summary>
    /// <param name="name">Tên đầy đủ, bắt buộc.</param>
    /// <param name="shortName">Tên viết tắt, có thể trống.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng.</exception>
    public void Rename(string name, string? shortName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Tên công ty không được để trống.");
        Name = name.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
    }

    /// <summary>
    /// Sửa thông tin công ty (mọi trường trừ mã – mã nằm trong số chứng từ đã cấp nên không đổi).
    /// </summary>
    /// <param name="name">Tên đầy đủ, bắt buộc.</param>
    /// <param name="shortName">Tên viết tắt.</param>
    /// <param name="taxCode">Mã số thuế: 10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số (đơn vị phụ thuộc); trống = chưa có.</param>
    /// <param name="address">Địa chỉ.</param>
    /// <param name="representative">Người đại diện theo pháp luật.</param>
    /// <param name="startDate">Ngày bắt đầu dùng hệ thống.</param>
    /// <exception cref="BusinessRuleException">Khi tên rỗng hoặc mã số thuế sai định dạng.</exception>
    public void UpdateInfo(
        string name, string? shortName, string? taxCode, string? address, string? representative, DateOnly? startDate)
    {
        var normalizedTaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim();
        if (normalizedTaxCode is not null && !TaxCodeRegex().IsMatch(normalizedTaxCode))
            throw new BusinessRuleException("Mã số thuế gồm 10 chữ số, hoặc 10 chữ số + \"-\" + 3 chữ số (ví dụ 0101234567-001).");

        Rename(name, shortName);
        TaxCode = normalizedTaxCode;
        Address = Clean(address);
        Representative = Clean(representative);
        StartDate = startDate;
    }

    /// <summary>Ngừng sử dụng công ty: người dùng không chọn được công ty này để làm việc nữa.</summary>
    /// <exception cref="BusinessRuleException">Khi là tổng công ty.</exception>
    public void Deactivate()
    {
        // Tổng công ty quản lý danh mục dùng chung và tài khoản quản trị (design.md mục 5) – ngừng thì không ai vào quản trị được.
        if (IsHeadOffice) throw new BusinessRuleException("Không ngừng dùng được tổng công ty.");
        IsActive = false;
    }

    /// <summary>Dùng lại công ty đã ngừng.</summary>
    public void Activate() => IsActive = true;

    private void SetCode(string code)
    {
        var normalized = CodeRules.Normalize(code);
        // Mã công ty nằm trong số chứng từ PO-{CTY}-{YYMM}-{0000} (design.md mục 5.4)
        // nên chỉ cho chữ không dấu và số, không có dấu gạch để tách được các phần của số chứng từ.
        if (!CodeRules.IsAlphanumeric(normalized, CodeMinLength, CodeMaxLength))
        {
            throw new BusinessRuleException(
                $"Mã công ty phải gồm {CodeMinLength}–{CodeMaxLength} ký tự chữ không dấu (A–Z) hoặc số.");
        }

        Code = normalized;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\d{10}(-\d{3})?$")]
    private static partial Regex TaxCodeRegex();
}
