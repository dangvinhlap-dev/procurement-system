using FluentValidation;
using Proc.Application.Purchasing;

namespace Proc.Application.Pricing;

/// <summary>Một dòng danh sách thỏa thuận chiết khấu sản lượng gộp.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số thỏa thuận.</param>
/// <param name="Title">Tên thỏa thuận.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="CategoryCode">Mã nhóm hàng.</param>
/// <param name="CategoryName">Tên nhóm hàng.</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến.</param>
/// <param name="Period">Kỳ tính bậc: Month, Quarter, Year, Whole.</param>
/// <param name="Basis">Cơ sở: Quantity, Amount.</param>
/// <param name="CompanyCodes">Các công ty áp dụng, cách nhau dấu phẩy.</param>
/// <param name="LineCount">Số mặt hàng.</param>
/// <param name="TierCount">Số bậc.</param>
/// <param name="Status">Draft, Submitted, Active, Cancelled.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record VolumeRebateListItemDto(
    Guid Id, string No, string Title, string SupplierCode, string SupplierName, string CategoryCode, string CategoryName, DateOnly ValidFrom,
    DateOnly ValidTo, string Period, string Basis, string CompanyCodes, int LineCount, int TierCount, string Status, string? CreatedByName);

/// <summary>Một dòng mặt hàng của thỏa thuận.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT tính sản lượng.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record VolumeRebateLineDto(Guid Id, int LineNo, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode, string? Note);

/// <summary>Một bậc chiết khấu.</summary>
/// <param name="TierNo">Số thứ tự bậc.</param>
/// <param name="FromValue">Ngưỡng từ.</param>
/// <param name="Percent">% chiết khấu.</param>
public sealed record VolumeRebateTierDto(int TierNo, decimal FromValue, decimal Percent);

/// <summary>Công ty áp dụng.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
public sealed record VolumeRebateCompanyDto(Guid Id, string Code, string Name);

/// <summary>Thao tác được phép trên thỏa thuận.</summary>
/// <param name="CanEdit">Sửa, gửi duyệt (nháp, đang làm việc ở tổng công ty, có quyền lập).</param>
/// <param name="CanApprove">Duyệt / từ chối: đến lượt, có quyền duyệt, không phải người lập, không xung đột lợi ích.</param>
/// <param name="CanRecall">Rút về nháp khi đang chờ duyệt.</param>
/// <param name="CanCancel">Hủy (nháp hoặc hiệu lực).</param>
/// <param name="CanCopy">Chép thành thỏa thuận nháp mới (để đổi bậc, gia hạn).</param>
/// <param name="ReadOnlyReason">Lý do chỉ xem.</param>
public sealed record VolumeRebateActions(bool CanEdit, bool CanApprove, bool CanRecall, bool CanCancel, bool CanCopy, string? ReadOnlyReason);

/// <summary>Chi tiết thỏa thuận chiết khấu sản lượng gộp.</summary>
public sealed record VolumeRebateDto(
    Guid Id, string No, string CompanyCode, string Title, Guid SupplierId, string SupplierCode, string SupplierName, Guid ItemCategoryId,
    string CategoryCode, string CategoryName, DateOnly ValidFrom, DateOnly ValidTo, string Period, string Basis, int? TargetTierNo,
    IReadOnlyList<VolumeRebateCompanyDto> Companies, string? Note, string Status, string? RejectReason, DateTimeOffset? ApprovedAt,
    DateOnly? CancelledOn, string? CancelReason, DateTimeOffset CreatedAt, string? CreatedByName, IReadOnlyList<VolumeRebateLineDto> Lines,
    IReadOnlyList<VolumeRebateTierDto> Tiers, IReadOnlyList<ApprovalStepDto> Approvals, VolumeRebateActions Actions);

/// <summary>Lưu thỏa thuận chiết khấu sản lượng gộp.</summary>
/// <param name="Title">Tên thỏa thuận.</param>
/// <param name="SupplierId">NCC.</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến.</param>
/// <param name="Period">Kỳ tính bậc: Month, Quarter, Year, Whole.</param>
/// <param name="Basis">Cơ sở: Quantity, Amount.</param>
/// <param name="TargetTierNo">Bậc mục tiêu (không bắt buộc).</param>
/// <param name="CompanyIds">Công ty áp dụng.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Mặt hàng.</param>
/// <param name="Tiers">Bậc.</param>
public sealed record SaveVolumeRebateRequest(
    string Title, Guid SupplierId, Guid ItemCategoryId, DateOnly ValidFrom, DateOnly ValidTo, string Period, string Basis, int? TargetTierNo,
    IReadOnlyList<Guid> CompanyIds, string? Note, IReadOnlyList<SaveVolumeRebateLineRequest> Lines, IReadOnlyList<SaveVolumeRebateTierRequest> Tiers);

/// <summary>Lưu một dòng mặt hàng.</summary>
/// <param name="Id">Id dòng đang có (null = mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="Note">Ghi chú.</param>
public sealed record SaveVolumeRebateLineRequest(Guid? Id, Guid ItemId, Guid UomId, string? Note);

/// <summary>Lưu một bậc.</summary>
/// <param name="FromValue">Ngưỡng từ.</param>
/// <param name="Percent">% chiết khấu.</param>
public sealed record SaveVolumeRebateTierRequest(decimal FromValue, decimal Percent);

/// <summary>Hủy thỏa thuận.</summary>
/// <param name="Reason">Lý do, bắt buộc.</param>
public sealed record CancelVolumeRebateRequest(string Reason);

/// <summary>Phần của một công ty trong kỳ: sản lượng, doanh số và chiết khấu được chia theo tỷ lệ doanh số.</summary>
/// <param name="CompanyId">Công ty.</param>
/// <param name="CompanyCode">Mã công ty.</param>
/// <param name="Quantity">SL theo ĐVT thỏa thuận (chỉ có nghĩa khi tính theo số lượng).</param>
/// <param name="Amount">Doanh số mua trước VAT.</param>
/// <param name="SharePercent">Tỷ lệ doanh số trong tổng (%, 4 số lẻ).</param>
/// <param name="RebateShare">Chiết khấu phải nhận được chia.</param>
public sealed record VolumeRebateCompanyShareDto(Guid CompanyId, string CompanyCode, decimal Quantity, decimal Amount, decimal SharePercent, decimal RebateShare);

/// <summary>
/// Lũy kế một kỳ tính bậc. State: Future (chưa tới kỳ), Open (đang trong kỳ), Ended (đã hết kỳ). TargetStatus: None (không đặt bậc mục
/// tiêu), NotStarted, Reached (đã đạt), OnTrack (dự báo đạt), AtRisk (dự báo hụt), Missed (hết kỳ chưa đạt).
/// </summary>
/// <param name="PeriodFrom">Từ ngày.</param>
/// <param name="PeriodTo">Đến ngày.</param>
/// <param name="State">Tình trạng kỳ.</param>
/// <param name="Volume">Lũy kế theo cơ sở tính bậc (SL hoặc doanh số).</param>
/// <param name="Amount">Doanh số gộp trước VAT.</param>
/// <param name="TierNo">Bậc đạt được (null = chưa đạt bậc nào).</param>
/// <param name="Percent">% của bậc đạt được (0 khi chưa đạt).</param>
/// <param name="RebateAmount">Chiết khấu phải nhận gộp = doanh số × % ÷ 100.</param>
/// <param name="NextTierNo">Bậc kế tiếp (null = đã ở bậc cao nhất).</param>
/// <param name="NextFromValue">Ngưỡng bậc kế tiếp.</param>
/// <param name="Shortfall">Còn thiếu để lên bậc kế tiếp.</param>
/// <param name="Forecast">Dự báo cuối kỳ theo nhịp mua (chỉ kỳ đang chạy).</param>
/// <param name="TargetFromValue">Ngưỡng bậc mục tiêu.</param>
/// <param name="TargetStatus">Tình trạng so bậc mục tiêu.</param>
/// <param name="IsNearNextTier">Sắp chạm bậc kế tiếp (đã đạt từ (100 − X)% ngưỡng).</param>
/// <param name="Companies">Phần của từng công ty (ở công ty thành viên chỉ có công ty mình).</param>
public sealed record VolumeRebatePeriodDto(
    DateOnly PeriodFrom, DateOnly PeriodTo, string State, decimal Volume, decimal Amount, int? TierNo, decimal Percent, decimal RebateAmount,
    int? NextTierNo, decimal? NextFromValue, decimal? Shortfall, decimal? Forecast, decimal? TargetFromValue, string TargetStatus,
    bool IsNearNextTier, IReadOnlyList<VolumeRebateCompanyShareDto> Companies);

/// <summary>Lũy kế mọi kỳ của một thỏa thuận.</summary>
/// <param name="AgreementId">Thỏa thuận.</param>
/// <param name="Basis">Cơ sở tính bậc.</param>
/// <param name="UomCode">ĐVT sản lượng (tính theo số lượng).</param>
/// <param name="Periods">Các kỳ theo thời gian.</param>
public sealed record VolumeRebateProgressDto(Guid AgreementId, string Basis, string? UomCode, IReadOnlyList<VolumeRebatePeriodDto> Periods);

/// <summary>Một dòng màn hình theo dõi bậc chiết khấu: kỳ hiện tại (hoặc kỳ cuối vừa hết) của một thỏa thuận hiệu lực.</summary>
/// <param name="AgreementId">Thỏa thuận.</param>
/// <param name="No">Số.</param>
/// <param name="Title">Tên.</param>
/// <param name="SupplierCode">Mã NCC.</param>
/// <param name="SupplierName">Tên NCC.</param>
/// <param name="Basis">Cơ sở.</param>
/// <param name="UomCode">ĐVT sản lượng.</param>
/// <param name="TargetTierNo">Bậc mục tiêu.</param>
/// <param name="Period">Kỳ.</param>
public sealed record VolumeRebateTrackingRowDto(
    Guid AgreementId, string No, string Title, string SupplierCode, string SupplierName, string Basis, string? UomCode, int? TargetTierNo,
    VolumeRebatePeriodDto Period);

/// <summary>
/// Thỏa thuận chiết khấu bậc theo sản lượng gộp (design.md mục 18.25 – lát G6a): tổng công ty lập cho NCC × nhóm hàng, áp cho các công ty
/// chọn, Ban cung ứng duyệt qua quy trình loại CKB; theo dõi lũy kế các công ty so với bậc, cảnh báo sắp chạm/hụt bậc, chia chiết khấu theo
/// tỷ lệ doanh số.
/// </summary>
public interface IVolumeRebateService
{
    /// <summary>Danh sách: ở tổng công ty mọi thỏa thuận; ở công ty thành viên chỉ thỏa thuận hiệu lực áp cho công ty đó.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Danh sách, hiệu lực mới nhất trước.</returns>
    Task<IReadOnlyList<VolumeRebateListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy hoặc ngoài phạm vi.</exception>
    Task<VolumeRebateDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập thỏa thuận nháp (chỉ khi đang làm việc ở tổng công ty).</summary>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận vừa lập.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không làm việc ở tổng công ty.</exception>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi sai quy tắc (trùng NCC × mặt hàng × công ty × thời hạn, NCC chưa duyệt…).</exception>
    Task<VolumeRebateDto> CreateAsync(SaveVolumeRebateRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa thỏa thuận nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Nội dung.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận sau khi sửa.</returns>
    Task<VolumeRebateDto> UpdateAsync(Guid id, SaveVolumeRebateRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt (kiểm tra lại NCC và trùng lặp).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận (Chờ duyệt, hoặc Hiệu lực nếu không có cấp nào).</returns>
    Task<VolumeRebateDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đến lượt; duyệt đủ thì hiệu lực.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận.</returns>
    Task<VolumeRebateDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (bắt buộc lý do): về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận.</returns>
    Task<VolumeRebateDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút thỏa thuận đang chờ duyệt về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận.</returns>
    Task<VolumeRebateDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy thỏa thuận nháp hoặc hiệu lực (bắt buộc lý do).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận.</returns>
    Task<VolumeRebateDto> CancelAsync(Guid id, CancelVolumeRebateRequest request, CancellationToken cancellationToken);

    /// <summary>Chép thành thỏa thuận nháp mới (cùng nội dung; thời hạn nối tiếp ngay sau thỏa thuận nguồn, cùng độ dài).</summary>
    /// <param name="id">Thỏa thuận nguồn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Thỏa thuận mới.</returns>
    Task<VolumeRebateDto> CopyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lũy kế mọi kỳ của thỏa thuận (nháp cũng xem được để thử bậc).</summary>
    /// <param name="id">Id.</param>
    /// <param name="nearPercent">X% để cảnh báo sắp chạm bậc (1–50, mặc định 10).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Lũy kế theo kỳ.</returns>
    Task<VolumeRebateProgressDto> ProgressAsync(Guid id, decimal? nearPercent, CancellationToken cancellationToken);

    /// <summary>Theo dõi bậc: kỳ hiện tại của mọi thỏa thuận hiệu lực (kể cả thỏa thuận hết hạn trong 31 ngày qua).</summary>
    /// <param name="nearPercent">X% để cảnh báo sắp chạm bậc (1–50, mặc định 10).</param>
    /// <param name="onlyAlerts">Chỉ dòng sắp chạm bậc, nguy cơ hụt hoặc đã hụt bậc mục tiêu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng theo dõi.</returns>
    Task<IReadOnlyList<VolumeRebateTrackingRowDto>> TrackingAsync(decimal? nearPercent, bool onlyAlerts, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra dữ liệu lưu thỏa thuận chiết khấu sản lượng gộp.</summary>
public sealed class SaveVolumeRebateRequestValidator : AbstractValidator<SaveVolumeRebateRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveVolumeRebateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Hãy nhập tên thỏa thuận.").MaximumLength(200);
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.ItemCategoryId).NotEmpty().WithMessage("Hãy chọn nhóm hàng.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).WithMessage("Ngày hiệu lực đến phải sau hoặc bằng ngày hiệu lực từ.");
        RuleFor(x => x.Period).NotEmpty().WithMessage("Hãy chọn kỳ tính bậc.");
        RuleFor(x => x.Basis).NotEmpty().WithMessage("Hãy chọn cơ sở tính bậc.");
        RuleFor(x => x.CompanyIds).NotEmpty().WithMessage("Hãy chọn ít nhất một công ty áp dụng.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Thỏa thuận phải có ít nhất một mặt hàng.");
        RuleFor(x => x.Tiers).NotEmpty().WithMessage("Thỏa thuận phải có ít nhất một bậc chiết khấu.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Hãy chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Hãy chọn ĐVT.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra yêu cầu hủy thỏa thuận.</summary>
public sealed class CancelVolumeRebateRequestValidator : AbstractValidator<CancelVolumeRebateRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public CancelVolumeRebateRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Hãy nhập lý do hủy.").MaximumLength(1000);
    }
}
