using FluentValidation;

namespace Proc.Application.Pricing;

/// <summary>Một dòng danh sách phiếu khảo sát giá thị trường.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="SurveyDate">Ngày khảo sát.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="LineCount">Số dòng giá.</param>
/// <param name="Status">Draft, Confirmed, Cancelled.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record MarketSurveyListItemDto(
    Guid Id, string No, DateOnly SurveyDate, string CategoryCode, string CategoryName, int LineCount, string Status, string? CreatedByName);

/// <summary>Một dòng phiếu khảo sát giá.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="SupplierId">NCC trong danh mục (null = nguồn ngoài).</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="SourceName">Tên nguồn ngoài danh mục.</param>
/// <param name="UnitPrice">Đơn giá VND, trước VAT, theo ĐVT dòng.</param>
/// <param name="BaseUnitPrice">Đơn giá quy về ĐVT cơ sở.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record MarketSurveyLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, Guid? SupplierId, string? SupplierCode,
    string? SupplierName, string? SourceName, decimal UnitPrice, decimal BaseUnitPrice, string? Note);

/// <summary>Thao tác được phép trên phiếu khảo sát giá.</summary>
/// <param name="CanEdit">Sửa, chốt (nháp, tổng công ty, có quyền lập).</param>
/// <param name="CanCancel">Hủy (nháp hoặc đã chốt).</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record MarketSurveyActions(bool CanEdit, bool CanCancel, string? ReadOnlyReason);

/// <summary>Chi tiết phiếu khảo sát giá thị trường.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="SurveyDate">Ngày khảo sát.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Status">Draft, Confirmed, Cancelled.</param>
/// <param name="ConfirmedAt">Thời điểm chốt.</param>
/// <param name="ConfirmedByName">Người chốt.</param>
/// <param name="CancelledOn">Ngày hủy.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="Lines">Các dòng.</param>
/// <param name="Actions">Thao tác được phép.</param>
public sealed record MarketSurveyDto(
    Guid Id, string No, DateOnly SurveyDate, Guid ItemCategoryId, string CategoryCode, string CategoryName, string? Note, string Status,
    DateTimeOffset? ConfirmedAt, string? ConfirmedByName, DateOnly? CancelledOn, string? CancelReason, DateTimeOffset CreatedAt,
    string? CreatedByName, IReadOnlyList<MarketSurveyLineDto> Lines, MarketSurveyActions Actions);

/// <summary>Lưu phiếu khảo sát giá.</summary>
/// <param name="SurveyDate">Ngày khảo sát.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Các dòng.</param>
public sealed record SaveMarketSurveyRequest(DateOnly SurveyDate, Guid ItemCategoryId, string? Note, IReadOnlyList<SaveMarketSurveyLineRequest> Lines);

/// <summary>Lưu một dòng khảo sát giá.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="SupplierId">NCC trong danh mục (không bắt buộc).</param>
/// <param name="SourceName">Tên nguồn ngoài danh mục – bắt buộc khi không chọn NCC.</param>
/// <param name="UnitPrice">Đơn giá VND, trước VAT.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SaveMarketSurveyLineRequest(Guid? Id, Guid ItemId, Guid UomId, Guid? SupplierId, string? SourceName, decimal UnitPrice, string? Note);

/// <summary>Hủy phiếu khảo sát giá.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record CancelMarketSurveyRequest(string Reason);

/// <summary>
/// Một giá thị trường trong ngân hàng giá: dòng của phiếu khảo sát đã chốt, quy về ĐVT cơ sở – để Ban so sánh khi duyệt giá tháng (lát G4b).
/// </summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="SurveyId">Phiếu khảo sát.</param>
/// <param name="SurveyNo">Số phiếu.</param>
/// <param name="SurveyDate">Ngày khảo sát.</param>
/// <param name="Source">NCC (mã – tên) hoặc tên nguồn ngoài.</param>
/// <param name="BaseUnitPrice">Đơn giá theo ĐVT cơ sở, VND trước VAT.</param>
public sealed record MarketPriceDto(Guid ItemId, Guid SurveyId, string SurveyNo, DateOnly SurveyDate, string Source, decimal BaseUnitPrice);

/// <summary>
/// Phiếu khảo sát giá thị trường – ngân hàng giá (design.md mục 18.25, lát G4a): Văn phòng Ban lập ở tổng công ty theo nhóm hàng, chốt để
/// giá vào ngân hàng giá; không qua duyệt.
/// </summary>
public interface IMarketPriceSurveyService
{
    /// <summary>Danh sách phiếu của tổng công ty (theo phạm vi nhóm hàng), mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<MarketSurveyListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy hoặc ngoài phạm vi.</exception>
    Task<MarketSurveyDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu nháp (chỉ khi đang làm việc ở tổng công ty).</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi sai quy tắc.</exception>
    Task<MarketSurveyDto> CreateAsync(SaveMarketSurveyRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<MarketSurveyDto> UpdateAsync(Guid id, SaveMarketSurveyRequest request, CancellationToken cancellationToken);

    /// <summary>Chốt phiếu nháp – giá vào ngân hàng giá.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MarketSurveyDto> ConfirmAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu (bắt buộc lý do).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<MarketSurveyDto> CancelAsync(Guid id, CancelMarketSurveyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Giá thị trường của các mặt hàng từ phiếu đã chốt, ngày khảo sát trong <paramref name="days"/> ngày gần nhất (tính đến hôm nay), mới
    /// nhất trước.
    /// </summary>
    /// <param name="itemIds">Mặt hàng.</param>
    /// <param name="days">Số ngày nhìn lại (1–730).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các giá.</returns>
    Task<IReadOnlyList<MarketPriceDto>> PricesAsync(IReadOnlyList<Guid> itemIds, int days, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu phiếu khảo sát giá.</summary>
public sealed class SaveMarketSurveyRequestValidator : AbstractValidator<SaveMarketSurveyRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveMarketSurveyRequestValidator()
    {
        RuleFor(x => x.ItemCategoryId).NotEmpty().WithMessage("Hãy chọn nhóm hàng.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng giá.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.UnitPrice).GreaterThan(0).WithMessage("Đơn giá phải > 0.");
            l.RuleFor(x => x.SourceName).MaximumLength(200);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra yêu cầu hủy phiếu khảo sát giá.</summary>
public sealed class CancelMarketSurveyRequestValidator : AbstractValidator<CancelMarketSurveyRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public CancelMarketSurveyRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(1000);
    }
}
