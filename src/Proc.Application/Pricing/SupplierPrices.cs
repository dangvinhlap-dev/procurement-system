using FluentValidation;

namespace Proc.Application.Pricing;

/// <summary>Một dòng danh sách bảng giá NCC.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số bảng giá.</param>
/// <param name="Title">Tên.</param>
/// <param name="CompanyCode">Công ty lập.</param>
/// <param name="IsGroupWide">Áp dụng toàn tập đoàn.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến.</param>
/// <param name="Status">Draft, Active, Inactive.</param>
/// <param name="LineCount">Số dòng.</param>
/// <param name="IsOwn">Bảng giá của công ty đang làm việc (sửa được); false = bảng giá toàn tập đoàn do công ty khác lập.</param>
public sealed record PriceListListItemDto(
    Guid Id, string No, string Title, string CompanyCode, bool IsGroupWide, string SupplierCode, string SupplierName, DateOnly ValidFrom,
    DateOnly? ValidTo, string Status, int LineCount, bool IsOwn);

/// <summary>Một dòng bảng giá.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="UomFactor">Hệ số ĐVT cơ sở.</param>
/// <param name="MinQty">Bậc số lượng.</param>
/// <param name="UnitPrice">Đơn giá.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất.</param>
/// <param name="BaseNetPrice">Giá thực mua theo ĐVT cơ sở, trước VAT.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PriceListLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, decimal UomFactor, decimal MinQty,
    decimal UnitPrice, decimal DiscountPct, string? VatCode, decimal BaseNetPrice, string? Note);

/// <summary>Thao tác được phép trên bảng giá.</summary>
/// <param name="CanEdit">Sửa (nháp, của công ty mình, có quyền).</param>
/// <param name="CanActivate">Đưa vào hiệu lực.</param>
/// <param name="CanDeactivate">Ngừng áp dụng.</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record PriceListActions(bool CanEdit, bool CanActivate, bool CanDeactivate, string? ReadOnlyReason);

/// <summary>Chi tiết bảng giá NCC.</summary>
public sealed record PriceListDto(
    Guid Id, string No, string Title, Guid CompanyId, string CompanyCode, bool IsGroupWide, Guid SupplierId, string SupplierCode,
    string SupplierName, DateOnly ValidFrom, DateOnly? ValidTo, string CurrencyCode, string? DeliveryTerms, string? Note, string Status,
    DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? ActivatedAt, IReadOnlyList<PriceListLineDto> Lines, PriceListActions Actions);

/// <summary>Lưu bảng giá.</summary>
public sealed record SavePriceListRequest(
    Guid SupplierId, string Title, bool IsGroupWide, DateOnly ValidFrom, DateOnly? ValidTo, string? DeliveryTerms, string? Note,
    IReadOnlyList<SavePriceListLineRequest> Lines);

/// <summary>Lưu một dòng bảng giá.</summary>
public sealed record SavePriceListLineRequest(
    Guid? Id, Guid ItemId, Guid UomId, decimal MinQty, decimal UnitPrice, decimal DiscountPct, string? VatCode, string? Note);

/// <summary>Giá gợi ý từ bảng giá hiệu lực cho PO: các bậc của một mặt hàng trong bảng giá được chọn.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT của bậc.</param>
/// <param name="UomFactor">Hệ số ĐVT.</param>
/// <param name="MinQty">Bậc số lượng.</param>
/// <param name="UnitPrice">Đơn giá.</param>
/// <param name="DiscountPct">% chiết khấu.</param>
/// <param name="VatCode">Thuế suất (đã thay bằng thuế suất mặt hàng nếu dòng để trống).</param>
/// <param name="PriceListNo">Số bảng giá nguồn.</param>
public sealed record ResolvedSupplierPriceDto(
    Guid ItemId, Guid UomId, decimal UomFactor, decimal MinQty, decimal UnitPrice, decimal DiscountPct, string? VatCode, string PriceListNo);

/// <summary>Bảng giá NCC (design.md mục 8.1).</summary>
public interface IPriceListService
{
    /// <summary>Bảng giá của công ty đang làm việc và bảng giá toàn tập đoàn.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách, mới nhất trước.</returns>
    Task<IReadOnlyList<PriceListListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết bảng giá.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng giá.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy hoặc ngoài phạm vi.</exception>
    Task<PriceListDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập bảng giá nháp.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng giá vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi sai quy tắc.</exception>
    Task<PriceListDto> CreateAsync(SavePriceListRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa bảng giá nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng giá sau khi sửa.</returns>
    Task<PriceListDto> UpdateAsync(Guid id, SavePriceListRequest request, CancellationToken cancellationToken);

    /// <summary>Đưa bảng giá vào hiệu lực.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng giá.</returns>
    Task<PriceListDto> ActivateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ngừng áp dụng bảng giá.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng giá.</returns>
    Task<PriceListDto> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Giá gợi ý cho PO: với mỗi mặt hàng, chọn một bảng giá hiệu lực vào ngày <paramref name="date"/> của NCC – bảng của công ty
    /// ưu tiên hơn bảng toàn tập đoàn, rồi bảng có ngày hiệu lực mới nhất – và trả mọi bậc của mặt hàng trong bảng đó.
    /// </summary>
    /// <param name="supplierId">NCC.</param>
    /// <param name="date">Ngày đặt hàng.</param>
    /// <param name="itemIds">Các mặt hàng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các bậc giá.</returns>
    Task<IReadOnlyList<ResolvedSupplierPriceDto>> ResolveAsync(Guid supplierId, DateOnly date, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);
}

/// <summary>Giá hiện hành của một bảng giá cho mặt hàng đang so sánh.</summary>
public sealed record ComparePriceListRowDto(
    string CompanyCode, bool IsGroupWide, string SupplierCode, string SupplierName, string PriceListNo, DateOnly ValidFrom, DateOnly? ValidTo,
    string UomCode, decimal MinQty, decimal UnitPrice, decimal DiscountPct, decimal BaseNetPrice);

/// <summary>
/// Giá thực mua của một công ty với một NCC cho mặt hàng đang so sánh.
/// </summary>
/// <param name="CompanyCode">Công ty mua.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="LastDate">Ngày giá gần nhất (phiếu nhập hoặc PO, cái nào mới hơn).</param>
/// <param name="LastSource">Nguồn giá gần nhất: Receipt hoặc PO.</param>
/// <param name="LastDocumentNo">Số chứng từ của giá gần nhất.</param>
/// <param name="LastPrice">Giá gần nhất theo ĐVT cơ sở, trước VAT.</param>
/// <param name="MinPrice">Thấp nhất 12 tháng (phiếu nhập đã ghi sổ).</param>
/// <param name="MaxPrice">Cao nhất 12 tháng.</param>
/// <param name="AvgPrice">Bình quân gia quyền theo SL 12 tháng.</param>
/// <param name="Qty12Months">SL nhập 12 tháng (ĐVT cơ sở).</param>
/// <param name="AboveGroupMinPct">Giá gần nhất cao hơn giá gần nhất thấp nhất tập đoàn bao nhiêu % (0 nếu là thấp nhất).</param>
/// <param name="IsAboveThreshold">Cao hơn quá ngưỡng X% – tô màu.</param>
public sealed record ComparePurchaseRowDto(
    string CompanyCode, string SupplierCode, string SupplierName, DateOnly LastDate, string LastSource, string LastDocumentNo, decimal LastPrice,
    decimal? MinPrice, decimal? MaxPrice, decimal? AvgPrice, decimal Qty12Months, decimal AboveGroupMinPct, bool IsAboveThreshold);

/// <summary>Một mục lịch sử giá (design.md mục 8.1 – PriceHistory).</summary>
/// <param name="Date">Ngày chứng từ (bảng giá: ngày hiệu lực).</param>
/// <param name="CompanyCode">Công ty.</param>
/// <param name="SupplierCode">NCC.</param>
/// <param name="Source">PriceList, PO, Receipt, Quotation.</param>
/// <param name="DocumentNo">Số chứng từ.</param>
/// <param name="BaseQty">SL theo ĐVT cơ sở (bảng giá: bậc SL).</param>
/// <param name="BasePrice">Giá theo ĐVT cơ sở, trước VAT, VND.</param>
public sealed record PriceHistoryEntryDto(DateOnly Date, string CompanyCode, string SupplierCode, string Source, string DocumentNo, decimal BaseQty, decimal BasePrice);

/// <summary>Kết quả so sánh giá một mặt hàng (design.md mục 8.2).</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="BaseUomCode">ĐVT cơ sở (mọi giá quy về ĐVT này).</param>
/// <param name="AllCompanies">true = toàn tập đoàn; false = chỉ công ty đang làm việc.</param>
/// <param name="ThresholdPct">Ngưỡng X% dùng để tô màu.</param>
/// <param name="GroupMinPrice">Thấp nhất 12 tháng (phiếu nhập) trong phạm vi.</param>
/// <param name="GroupMaxPrice">Cao nhất 12 tháng.</param>
/// <param name="GroupAvgPrice">Bình quân gia quyền 12 tháng.</param>
/// <param name="PriceLists">Giá bảng giá đang hiệu lực.</param>
/// <param name="Purchases">Giá thực mua theo công ty × NCC.</param>
/// <param name="History">Lịch sử giá 12 tháng, mới nhất trước.</param>
public sealed record PriceComparisonDto(
    Guid ItemId, string ItemCode, string ItemName, string BaseUomCode, bool AllCompanies, decimal ThresholdPct, decimal? GroupMinPrice,
    decimal? GroupMaxPrice, decimal? GroupAvgPrice, IReadOnlyList<ComparePriceListRowDto> PriceLists, IReadOnlyList<ComparePurchaseRowDto> Purchases,
    IReadOnlyList<PriceHistoryEntryDto> History);

/// <summary>So sánh giá theo mặt hàng (design.md mục 8.2).</summary>
public interface IPriceComparisonService
{
    /// <summary>
    /// So sánh giá một mặt hàng. <paramref name="allCompanies"/> cần quyền <c>Report.Price.ViewAllCompanies</c>.
    /// </summary>
    /// <param name="itemId">Mặt hàng.</param>
    /// <param name="allCompanies">Xem toàn tập đoàn.</param>
    /// <param name="thresholdPct">Ngưỡng X% (0–1000).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Kết quả so sánh.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà thiếu quyền.</exception>
    Task<PriceComparisonDto> CompareAsync(Guid itemId, bool allCompanies, decimal thresholdPct, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu bảng giá.</summary>
public sealed class SavePriceListRequestValidator : AbstractValidator<SavePriceListRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SavePriceListRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Hãy nhập tên bảng giá.").MaximumLength(200);
        RuleFor(x => x.DeliveryTerms).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Bảng giá phải có ít nhất một dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}
