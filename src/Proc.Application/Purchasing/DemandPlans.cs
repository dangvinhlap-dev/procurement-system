using FluentValidation;

namespace Proc.Application.Purchasing;

/// <summary>Một dòng danh sách phiếu kế hoạch nhu cầu.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Year">Năm kế hoạch.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="LineCount">Số mặt hàng.</param>
/// <param name="Status">Draft, Confirmed, Replaced, Cancelled.</param>
/// <param name="ReplacesNo">Số phiếu được điều chỉnh (null = phiếu gốc).</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record DemandPlanListItemDto(
    Guid Id, string No, int Year, string CategoryCode, string CategoryName, int LineCount, string Status, string? ReplacesNo, string? CreatedByName);

/// <summary>Một dòng phiếu kế hoạch nhu cầu.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="Months">SL 12 tháng theo ĐVT dòng (phần tử 0 là tháng 1).</param>
/// <param name="Total">Tổng năm theo ĐVT dòng.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record DemandPlanLineDto(
    Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, IReadOnlyList<decimal> Months, decimal Total,
    string? Note);

/// <summary>Thao tác được phép trên phiếu kế hoạch nhu cầu.</summary>
/// <param name="CanEdit">Sửa, chốt (nháp, có quyền lập).</param>
/// <param name="CanCancel">Hủy (nháp hoặc đã chốt chưa có phiếu điều chỉnh soạn dở).</param>
/// <param name="CanAdjust">Lập phiếu điều chỉnh (đã chốt, năm chưa qua, chưa có phiếu điều chỉnh).</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record DemandPlanActions(bool CanEdit, bool CanCancel, bool CanAdjust, string? ReadOnlyReason);

/// <summary>Chi tiết phiếu kế hoạch nhu cầu.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Year">Năm kế hoạch.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Status">Draft, Confirmed, Replaced, Cancelled.</param>
/// <param name="ReplacesId">Phiếu được điều chỉnh.</param>
/// <param name="ReplacesNo">Số phiếu được điều chỉnh.</param>
/// <param name="ReplacedById">Phiếu điều chỉnh của phiếu này (chưa hủy).</param>
/// <param name="ReplacedByNo">Số phiếu điều chỉnh.</param>
/// <param name="ConfirmedAt">Thời điểm chốt.</param>
/// <param name="ConfirmedByName">Người chốt.</param>
/// <param name="CancelledOn">Ngày hủy.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="Version">Phiên bản dòng (chống ghi đè – gửi lại qua If-Match).</param>
/// <param name="Lines">Các dòng.</param>
/// <param name="Actions">Thao tác được phép.</param>
public sealed record DemandPlanDto(
    Guid Id, string No, int Year, Guid ItemCategoryId, string CategoryCode, string CategoryName, string? Note, string Status, Guid? ReplacesId,
    string? ReplacesNo, Guid? ReplacedById, string? ReplacedByNo, DateTimeOffset? ConfirmedAt, string? ConfirmedByName, DateOnly? CancelledOn,
    string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName, uint Version, IReadOnlyList<DemandPlanLineDto> Lines,
    DemandPlanActions Actions);

/// <summary>Lưu phiếu kế hoạch nhu cầu.</summary>
/// <param name="Year">Năm kế hoạch.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Các dòng.</param>
public sealed record SaveDemandPlanRequest(int Year, Guid ItemCategoryId, string? Note, IReadOnlyList<SaveDemandPlanLineRequest> Lines);

/// <summary>Lưu một dòng kế hoạch nhu cầu.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="Months">SL 12 tháng (phần tử 0 là tháng 1).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SaveDemandPlanLineRequest(Guid? Id, Guid ItemId, Guid UomId, IReadOnlyList<decimal> Months, string? Note);

/// <summary>Hủy phiếu kế hoạch nhu cầu.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record CancelDemandPlanRequest(string Reason);

/// <summary>
/// Phiếu kế hoạch nhu cầu (design.md mục 18.25, lát G5a): mỗi công ty lập nhu cầu năm × nhóm hàng, SL 12 tháng mỗi mặt hàng; chốt để vào tổng
/// hợp nhu cầu gộp; sửa sau khi chốt bằng phiếu điều chỉnh.
/// </summary>
public interface IDemandPlanService
{
    /// <summary>Danh sách phiếu của công ty đang làm việc (theo phạm vi nhóm hàng), năm mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách.</returns>
    Task<IReadOnlyList<DemandPlanListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết phiếu.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy hoặc ngoài phạm vi.</exception>
    Task<DemandPlanDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu nháp cho công ty đang làm việc.</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi sai quy tắc (năm, nhóm hàng, trùng mặt hàng với phiếu khác…).</exception>
    Task<DemandPlanDto> CreateAsync(SaveDemandPlanRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa phiếu nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<DemandPlanDto> UpdateAsync(Guid id, SaveDemandPlanRequest request, CancellationToken cancellationToken);

    /// <summary>Chốt phiếu nháp; phiếu điều chỉnh chốt xong thì phiếu cũ thành Đã thay.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<DemandPlanDto> ConfirmAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập phiếu điều chỉnh (nháp, chép nội dung) cho phiếu đã chốt.</summary>
    /// <param name="id">Id phiếu đã chốt.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu điều chỉnh vừa lập.</returns>
    Task<DemandPlanDto> AdjustAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu (bắt buộc lý do).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    Task<DemandPlanDto> CancelAsync(Guid id, CancelDemandPlanRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Tổng hợp nhu cầu gộp (lát G5b): SL kế hoạch 12 tháng theo ĐVT cơ sở của các phiếu Đã chốt trong năm, mỗi mặt hàng một dòng, tách
    /// theo công ty. Toàn tập đoàn gộp mọi công ty (quyền <c>Purchase.DemandPlan.ViewAllCompanies</c>); không thì chỉ công ty đang làm việc.
    /// </summary>
    /// <param name="year">Năm kế hoạch.</param>
    /// <param name="categoryId">Nhóm hàng của mặt hàng (gồm nhóm con); null = mọi nhóm trong phạm vi.</param>
    /// <param name="allCompanies">true = gộp mọi công ty.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Bảng tổng hợp.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi năm không hợp lệ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà không có quyền.</exception>
    Task<DemandSummaryDto> SummaryAsync(int year, Guid? categoryId, bool allCompanies, CancellationToken cancellationToken);

    /// <summary>
    /// SL kế hoạch của các dòng (lát G5c) để làm SL dự kiến RFQ / hợp đồng nguyên tắc: tổng các tháng trong khoảng của phiếu Đã chốt, quy về
    /// ĐVT của từng dòng.
    /// </summary>
    /// <param name="request">Khoảng tháng, phạm vi công ty, các dòng (mặt hàng × ĐVT).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Một kết quả cho mỗi dòng, cùng thứ tự.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi khoảng tháng sai hoặc quá 36 tháng.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi xem toàn tập đoàn mà không có quyền.</exception>
    Task<IReadOnlyList<PlannedQuantityDto>> QuantitiesAsync(PlannedQuantityRequest request, CancellationToken cancellationToken);
}

/// <summary>Yêu cầu lấy SL kế hoạch cho các dòng của RFQ / hợp đồng nguyên tắc (lát G5c).</summary>
/// <param name="FromMonth">Tháng đầu (ngày bất kỳ trong tháng; tính đủ tháng).</param>
/// <param name="ToMonth">Tháng cuối (ngày bất kỳ trong tháng; tính đủ tháng).</param>
/// <param name="AllCompanies">true = cộng kế hoạch mọi công ty (cần quyền xem kế hoạch nhu cầu toàn tập đoàn).</param>
/// <param name="Lines">Các dòng cần lấy SL.</param>
public sealed record PlannedQuantityRequest(DateOnly FromMonth, DateOnly ToMonth, bool AllCompanies, IReadOnlyList<PlannedQuantityLineRequest> Lines);

/// <summary>Một dòng cần lấy SL kế hoạch.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT của dòng (kết quả quy về ĐVT này).</param>
public sealed record PlannedQuantityLineRequest(Guid ItemId, Guid UomId);

/// <summary>Kế hoạch của một công ty cho một dòng.</summary>
/// <param name="CompanyCode">Mã công ty.</param>
/// <param name="Qty">SL theo ĐVT dòng.</param>
/// <param name="PlanNos">Số các phiếu Đã chốt góp vào SL.</param>
public sealed record PlannedQuantityCompanyDto(string CompanyCode, decimal Qty, IReadOnlyList<string> PlanNos);

/// <summary>SL kế hoạch của một dòng.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT dòng.</param>
/// <param name="Qty">Tổng SL kế hoạch theo ĐVT dòng (4 số lẻ); null khi không có kế hoạch đã chốt trong khoảng, hoặc ĐVT không quy đổi được.</param>
/// <param name="Companies">Chi tiết từng công ty có kế hoạch.</param>
public sealed record PlannedQuantityDto(Guid ItemId, Guid UomId, decimal? Qty, IReadOnlyList<PlannedQuantityCompanyDto> Companies);

/// <summary>Kiểm tra yêu cầu lấy SL kế hoạch.</summary>
public sealed class PlannedQuantityRequestValidator : AbstractValidator<PlannedQuantityRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public PlannedQuantityRequestValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu chưa có dòng mặt hàng.");
        RuleFor(x => x.Lines.Count).LessThanOrEqualTo(2000).WithMessage("Tối đa 2.000 dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.UomId).NotEmpty();
        });
    }
}

/// <summary>Một công ty trong bảng tổng hợp nhu cầu.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Code">Mã công ty.</param>
/// <param name="Name">Tên công ty.</param>
/// <param name="ConfirmedPlans">Số phiếu Đã chốt của năm (theo nhóm hàng lọc) – đã tính vào tổng hợp.</param>
/// <param name="DraftPlans">Số phiếu Nháp của năm – chưa tính, để Văn phòng Ban nhắc công ty chốt.</param>
public sealed record DemandSummaryCompanyDto(Guid CompanyId, string Code, string Name, int ConfirmedPlans, int DraftPlans);

/// <summary>Nhu cầu của một công ty cho một mặt hàng.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="Months">SL 12 tháng theo ĐVT cơ sở, phần tử 0 là tháng 1.</param>
/// <param name="Total">Cả năm theo ĐVT cơ sở.</param>
/// <param name="PlanNos">Số các phiếu Đã chốt có mặt hàng này.</param>
public sealed record DemandSummaryCellDto(Guid CompanyId, IReadOnlyList<decimal> Months, decimal Total, IReadOnlyList<string> PlanNos);

/// <summary>Một dòng tổng hợp nhu cầu: một mặt hàng.</summary>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="BaseUomCode">ĐVT cơ sở.</param>
/// <param name="CategoryCode">Mã nhóm hàng của mặt hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng của mặt hàng.</param>
/// <param name="Companies">Nhu cầu từng công ty có kế hoạch (công ty không có thì không có phần tử).</param>
/// <param name="Months">Tổng các công ty theo tháng.</param>
/// <param name="Total">Tổng các công ty cả năm.</param>
public sealed record DemandSummaryRowDto(
    Guid ItemId, string ItemCode, string ItemName, string BaseUomCode, string CategoryCode, string CategoryName,
    IReadOnlyList<DemandSummaryCellDto> Companies, IReadOnlyList<decimal> Months, decimal Total);

/// <summary>Bảng tổng hợp nhu cầu gộp của một năm.</summary>
/// <param name="Year">Năm kế hoạch.</param>
/// <param name="AllCompanies">true khi gộp mọi công ty (toàn tập đoàn).</param>
/// <param name="Companies">Các công ty là cột của bảng.</param>
/// <param name="Rows">Dòng mặt hàng, mã hàng tăng dần.</param>
public sealed record DemandSummaryDto(int Year, bool AllCompanies, IReadOnlyList<DemandSummaryCompanyDto> Companies, IReadOnlyList<DemandSummaryRowDto> Rows);

/// <summary>Kiểm tra dữ liệu lưu phiếu kế hoạch nhu cầu.</summary>
public sealed class SaveDemandPlanRequestValidator : AbstractValidator<SaveDemandPlanRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveDemandPlanRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Năm kế hoạch không hợp lệ.");
        RuleFor(x => x.ItemCategoryId).NotEmpty().WithMessage("Hãy chọn nhóm hàng.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một mặt hàng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.Months).Must(m => m is { Count: 12 }).WithMessage("Phải có số lượng đủ 12 tháng.");
            l.RuleForEach(x => x.Months).GreaterThanOrEqualTo(0).WithMessage("Số lượng không được âm.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra yêu cầu hủy phiếu kế hoạch nhu cầu.</summary>
public sealed class CancelDemandPlanRequestValidator : AbstractValidator<CancelDemandPlanRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public CancelDemandPlanRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(1000);
    }
}
