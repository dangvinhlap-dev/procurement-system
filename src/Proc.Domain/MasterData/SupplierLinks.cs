using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>
/// NCC áp dụng cho một công ty (design.md mục 5.3, 5.9): trả lời "NCC nào đang bán cho công ty nào".
/// Thuộc công ty (CompanyId) nên tự lọc theo công ty đang làm việc; mỗi công ty tự quản lý danh sách NCC mình mua.
/// </summary>
[Auditable("NCC áp dụng cho công ty", LabelProperty = nameof(LocalCode))]
public sealed class SupplierCompany : AuditableEntity, ICompanyOwned
{
    private SupplierCompany()
    {
    }

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Mã NCC tại công ty – để khớp với mã đối tượng trong phần mềm kế toán của công ty; duy nhất trong công ty.</summary>
    public string? LocalCode { get; private set; }

    /// <summary>Ngày bắt đầu giao dịch.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>Đang giao dịch (false = công ty đã ngừng mua của NCC này).</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Áp dụng NCC cho công ty.
    /// </summary>
    /// <param name="supplier">NCC (không được ở danh sách đen).</param>
    /// <param name="companyId">Công ty.</param>
    /// <param name="localCode">Mã NCC tại công ty.</param>
    /// <param name="startDate">Ngày bắt đầu giao dịch.</param>
    /// <returns>Dòng áp dụng, đang giao dịch.</returns>
    /// <exception cref="BusinessRuleException">Khi NCC ở danh sách đen hoặc mã sai định dạng.</exception>
    public static SupplierCompany Apply(Supplier supplier, Guid companyId, string? localCode, DateOnly? startDate)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        if (companyId == Guid.Empty) throw new BusinessRuleException("Phải chọn công ty áp dụng.");
        var link = new SupplierCompany { SupplierId = supplier.Id, CompanyId = companyId };
        link.Update(supplier, localCode, startDate, isActive: true);
        return link;
    }

    /// <summary>
    /// Sửa mã tại công ty, ngày bắt đầu, trạng thái giao dịch.
    /// </summary>
    /// <param name="supplier">NCC của dòng này.</param>
    /// <param name="localCode">Mã NCC tại công ty (chữ không dấu, số, - . _ /; tối đa 30); trống = dùng mã chung.</param>
    /// <param name="startDate">Ngày bắt đầu giao dịch.</param>
    /// <param name="isActive">Đang giao dịch.</param>
    /// <exception cref="BusinessRuleException">Khi bật giao dịch với NCC ở danh sách đen hoặc mã sai định dạng.</exception>
    public void Update(Supplier supplier, string? localCode, DateOnly? startDate, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        // NCC trong danh sách đen thì không công ty nào được giao dịch nữa (design.md mục 5.8).
        if (isActive && supplier.Status == SupplierStatus.Blacklisted)
            throw new BusinessRuleException($"NCC {supplier.Code} đang ở danh sách đen, không áp dụng cho công ty được.");
        var code = string.IsNullOrWhiteSpace(localCode) ? null : CodeRules.Normalize(localCode);
        if (code is not null && !CodeRules.IsExtendedCode(code, 1, 30))
            throw new BusinessRuleException("Mã NCC tại công ty gồm tối đa 30 ký tự chữ không dấu, số hoặc - . _ /.");
        LocalCode = code;
        StartDate = startDate;
        IsActive = isActive;
    }
}

/// <summary>Thông tin NCC – sản phẩm gửi vào khi lưu.</summary>
/// <param name="SupplierItemCode">Mã hàng của NCC (trên báo giá, hóa đơn của họ).</param>
/// <param name="PurchaseUomId">ĐVT mua: ĐVT cơ sở hoặc một ĐVT quy đổi của mặt hàng.</param>
/// <param name="Moq">Số lượng đặt tối thiểu theo ĐVT mua.</param>
/// <param name="LeadTimeDays">Số ngày từ khi đặt tới khi giao (0–365).</param>
/// <param name="IsPreferred">NCC ưu tiên của mặt hàng.</param>
public sealed record SupplierItemData(string? SupplierItemCode, Guid PurchaseUomId, decimal? Moq, int? LeadTimeDays, bool IsPreferred);

/// <summary>
/// NCC – sản phẩm/dịch vụ (design.md mục 5.3, 5.9): trả lời "NCC nào đang bán sản phẩm gì". Dùng chung toàn tập đoàn;
/// lọc theo phạm vi nhóm hàng của mặt hàng.
/// </summary>
[Auditable("NCC – sản phẩm", LabelProperty = nameof(SupplierItemCode))]
public sealed class SupplierItem : AuditableEntity
{
    private SupplierItem()
    {
    }

    /// <summary>NCC.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Mã hàng của NCC.</summary>
    public string? SupplierItemCode { get; private set; }

    /// <summary>ĐVT mua.</summary>
    public Guid PurchaseUomId { get; private set; }

    /// <summary>Số lượng đặt tối thiểu theo ĐVT mua – numeric(18,4).</summary>
    public decimal? Moq { get; private set; }

    /// <summary>Thời gian giao hàng (ngày).</summary>
    public int? LeadTimeDays { get; private set; }

    /// <summary>NCC ưu tiên của mặt hàng – mỗi mặt hàng tối đa một (chỉ mục duy nhất có điều kiện trong CSDL).</summary>
    public bool IsPreferred { get; private set; }

    /// <summary>
    /// Ghi nhận NCC bán mặt hàng.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="item">Mặt hàng (đang dùng).</param>
    /// <param name="data">Thông tin.</param>
    /// <returns>Dòng mới.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu không hợp lệ.</exception>
    public static SupplierItem Create(Guid supplierId, Item item, SupplierItemData data)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsActive) throw new BusinessRuleException($"Mặt hàng {item.Code} đã ngừng dùng.");
        var link = new SupplierItem { SupplierId = supplierId, ItemId = item.Id };
        link.Update(item, data);
        return link;
    }

    /// <summary>
    /// Sửa thông tin.
    /// </summary>
    /// <param name="item">Mặt hàng của dòng này (để kiểm tra ĐVT mua).</param>
    /// <param name="data">Thông tin mới.</param>
    /// <exception cref="BusinessRuleException">Khi ĐVT mua không thuộc mặt hàng, MOQ hay lead time không hợp lệ.</exception>
    public void Update(Item item, SupplierItemData data)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(data);
        if (item.Id != ItemId) throw new BusinessRuleException("Không đổi được mặt hàng của dòng NCC – sản phẩm; xóa và thêm dòng mới.");
        // ĐVT mua phải quy đổi được về ĐVT cơ sở, nếu không lúc nhập kho không tính được số tồn (design.md mục 5.2).
        if (data.PurchaseUomId != item.BaseUomId && item.Conversions.All(c => c.UomId != data.PurchaseUomId))
            throw new BusinessRuleException($"ĐVT mua phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng {item.Code}.");
        if (data.Moq is { } moq && (moq <= 0 || decimal.Round(moq, 4) != moq || moq >= 100_000_000_000_000m))
            throw new BusinessRuleException("Số lượng đặt tối thiểu phải lớn hơn 0, tối đa 4 chữ số thập phân.");
        if (data.LeadTimeDays is < 0 or > 365) throw new BusinessRuleException("Thời gian giao hàng từ 0 đến 365 ngày.");
        var code = string.IsNullOrWhiteSpace(data.SupplierItemCode) ? null : data.SupplierItemCode.Trim();
        if (code?.Length > 50) throw new BusinessRuleException("Mã hàng của NCC tối đa 50 ký tự.");

        SupplierItemCode = code;
        PurchaseUomId = data.PurchaseUomId;
        Moq = data.Moq;
        LeadTimeDays = data.LeadTimeDays;
        IsPreferred = data.IsPreferred;
    }

    /// <summary>Bỏ đánh dấu ưu tiên (khi NCC khác được chọn làm NCC ưu tiên của cùng mặt hàng).</summary>
    public void ClearPreferred() => IsPreferred = false;
}
