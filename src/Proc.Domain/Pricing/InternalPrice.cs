using Proc.Domain.Common;
using Proc.Domain.MasterData;

namespace Proc.Domain.Pricing;

/// <summary>
/// Một dòng bảng giá nội bộ (design.md mục 4, 5, 6.3): giá công ty bán (<see cref="CompanyId"/>) bán một mặt hàng cho công ty
/// mua trong tập đoàn, theo ĐVT cơ sở, trong một khoảng hiệu lực. Công ty mua trống = áp cho mọi công ty mua; khi có cả giá riêng
/// và giá chung cùng hiệu lực thì giá riêng được dùng. Phiếu xuất bán nội bộ lấy giá từ đây, không nhập tay.
/// </summary>
[Auditable("Giá bán nội bộ", LabelProperty = nameof(Label))]
public sealed class InternalPrice : AuditableEntity, ICompanyOwned
{
    private InternalPrice()
    {
    }

    /// <summary>Công ty bán.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Công ty mua; null = mọi công ty mua.</summary>
    public Guid? BuyerCompanyId { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Giá bán nội bộ chưa VAT theo ĐVT cơ sở – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Thuế suất (KCT, 0, 5, 8, 10); trống = theo thuế suất mặc định của mặt hàng.</summary>
    public string? VatCode { get; private set; }

    /// <summary>Hiệu lực từ ngày.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Hiệu lực đến ngày; null = tới khi có giá mới.</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Nhãn trong nhật ký.</summary>
    public string Label => $"giá từ {ValidFrom:dd/MM/yyyy}";

    /// <summary>Tạo giá bán nội bộ.</summary>
    /// <param name="sellerCompanyId">Công ty bán (công ty đang làm việc).</param>
    /// <param name="buyerCompanyId">Công ty mua; null = mọi công ty.</param>
    /// <param name="item">Mặt hàng (phải là hàng tồn kho đang dùng).</param>
    /// <param name="unitPrice">Giá ≥ 0.</param>
    /// <param name="vatCode">Thuế suất.</param>
    /// <param name="validFrom">Từ ngày.</param>
    /// <param name="validTo">Đến ngày.</param>
    /// <param name="note">Ghi chú.</param>
    /// <returns>Giá mới.</returns>
    /// <exception cref="BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    public static InternalPrice Create(
        Guid sellerCompanyId, Guid? buyerCompanyId, Item item, decimal unitPrice, string? vatCode, DateOnly validFrom, DateOnly? validTo, string? note)
    {
        if (sellerCompanyId == Guid.Empty) throw new BusinessRuleException("Giá nội bộ phải thuộc một công ty bán.");
        // Bán nội bộ là giữa hai pháp nhân khác nhau (design.md mục 4); trong một công ty dùng phiếu chuyển kho.
        if (buyerCompanyId == sellerCompanyId) throw new BusinessRuleException("Công ty mua phải khác công ty bán.");
        if (item.Type != ItemType.Inventory) throw new BusinessRuleException($"Mặt hàng {item.Code} không phải hàng tồn kho.");
        if (!item.IsActive) throw new BusinessRuleException($"Mặt hàng {item.Code} đã ngừng dùng.");
        var price = new InternalPrice { CompanyId = sellerCompanyId, BuyerCompanyId = buyerCompanyId, ItemId = item.Id };
        price.Set(unitPrice, vatCode, validFrom, validTo, note);
        return price;
    }

    /// <summary>Sửa giá, thuế suất, hiệu lực, ghi chú (công ty mua và mặt hàng không đổi – muốn đổi thì thêm dòng mới).</summary>
    /// <exception cref="BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    public void Update(decimal unitPrice, string? vatCode, DateOnly validFrom, DateOnly? validTo, string? note) =>
        Set(unitPrice, vatCode, validFrom, validTo, note);

    /// <summary>Hai dòng giá cùng công ty mua, cùng mặt hàng có khoảng hiệu lực chồng nhau không.</summary>
    /// <param name="from">Từ ngày của dòng kia.</param>
    /// <param name="to">Đến ngày của dòng kia (null = không hạn).</param>
    /// <returns>true nếu chồng nhau.</returns>
    public bool Overlaps(DateOnly from, DateOnly? to) =>
        from <= (ValidTo ?? DateOnly.MaxValue) && ValidFrom <= (to ?? DateOnly.MaxValue);

    private void Set(decimal unitPrice, string? vatCode, DateOnly validFrom, DateOnly? validTo, string? note)
    {
        if (unitPrice < 0) throw new BusinessRuleException("Giá bán nội bộ không được âm.");
        if (validTo is { } to && to < validFrom) throw new BusinessRuleException("Ngày hết hiệu lực phải sau hoặc bằng ngày bắt đầu.");
        var vat = string.IsNullOrWhiteSpace(vatCode) ? null : vatCode.Trim().ToUpperInvariant();
        if (vat is not null && !VatRates.All.Contains(vat))
            throw new BusinessRuleException($"Thuế suất phải là một trong {string.Join(", ", VatRates.All)}.");
        UnitPrice = Math.Round(unitPrice, 6, MidpointRounding.AwayFromZero);
        VatCode = vat;
        ValidFrom = validFrom;
        ValidTo = validTo;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
