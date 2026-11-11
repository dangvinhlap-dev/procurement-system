using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Bảng tổng hợp trên danh sách (lát Y5b, design.md mục 19.3).</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu, ví dụ BTH-TCT-2611-0001.</param>
/// <param name="Date">Ngày lập.</param>
/// <param name="Form">Construction (2A) hoặc Goods (2B).</param>
/// <param name="Status">Draft, Submitted, Approved, Cancelled.</param>
/// <param name="DepartmentName">Bộ phận đề xuất.</param>
/// <param name="ConstructionProjectCode">Mã công trình (2A).</param>
/// <param name="LineCount">Số dòng.</param>
/// <param name="TotalAmount">Tổng đề xuất.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record PurchaseSummaryListItemDto(
    Guid Id, string No, DateOnly Date, string Form, string Status, string? DepartmentName, string? ConstructionProjectCode, int LineCount,
    decimal TotalAmount, string? CreatedByName);

/// <summary>Dòng bảng tổng hợp.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">Số thứ tự.</param>
/// <param name="RequestId">Đề nghị mua (để mở phiếu gốc).</param>
/// <param name="RequestNo">Số đề nghị.</param>
/// <param name="RequestLineId">Dòng đề nghị.</param>
/// <param name="RequestCategory">Loại mua của đề nghị: Construction, Goods, Service.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="Qty">SL đề xuất mua (chụp lúc lưu).</param>
/// <param name="EstPrice">Đơn giá tham khảo.</param>
/// <param name="Amount">Thành tiền.</param>
/// <param name="NeededDate">Ngày cần.</param>
/// <param name="PurchasingUnitId">Đơn vị mua.</param>
/// <param name="PurchasingUnitCode">Mã đơn vị mua.</param>
/// <param name="PurchasingUnitName">Tên đơn vị mua.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record PurchaseSummaryLineDto(
    Guid Id, int LineNo, Guid RequestId, string RequestNo, Guid RequestLineId, string RequestCategory, Guid ItemId, string ItemCode,
    string ItemName, Guid UomId, string UomCode, decimal Qty, decimal EstPrice, decimal Amount, DateOnly? NeededDate, Guid PurchasingUnitId,
    string PurchasingUnitCode, string PurchasingUnitName, string? Note);

/// <summary>
/// Khung chi phí trên đầu bảng tổng hợp như biểu mẫu (lát Y5b). 2A: dự toán – lũy kế đã mua (PO đã duyệt của công trình, chưa VAT) – tổng đề
/// xuất – còn lại sau đề xuất. 2B: ngân sách (định mức/dự trù) bộ phận × tháng của ngày lập – lũy kế đã dùng (design.md mục 18.24: PO + phần đề
/// nghị chưa lên PO, nên đã gồm các dòng của bảng này) – tổng đề xuất – còn lại = ngân sách − lũy kế.
/// </summary>
/// <param name="LimitAmount">Dự toán (2A) hoặc ngân sách tháng (2B); null = 2B tháng đó bộ phận không có ngân sách.</param>
/// <param name="AccumulatedAmount">Lũy kế đã mua (2A) / đã dùng (2B).</param>
/// <param name="ProposedAmount">Tổng đề xuất của bảng.</param>
/// <param name="RemainingAmount">Còn lại: 2A = dự toán − lũy kế − tổng đề xuất; 2B = ngân sách − lũy kế; null khi không có ngân sách.</param>
/// <param name="Exceeded">Vượt: còn lại âm.</param>
/// <param name="ProjectCode">Mã công trình (2A).</param>
/// <param name="ProjectName">Tên công trình (2A).</param>
/// <param name="BudgetMonth">Tháng ngân sách (2B), ngày đầu tháng.</param>
public sealed record PurchaseSummaryFrameDto(
    decimal? LimitAmount, decimal AccumulatedAmount, decimal ProposedAmount, decimal? RemainingAmount, bool Exceeded, string? ProjectCode,
    string? ProjectName, DateOnly? BudgetMonth);

/// <summary>Chi tiết bảng tổng hợp.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số phiếu.</param>
/// <param name="Date">Ngày lập.</param>
/// <param name="Form">Construction (2A) hoặc Goods (2B).</param>
/// <param name="Status">Trạng thái.</param>
/// <param name="DepartmentId">Bộ phận đề xuất.</param>
/// <param name="DepartmentName">Tên bộ phận.</param>
/// <param name="ConstructionProjectId">Công trình (2A).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="TotalAmount">Tổng đề xuất.</param>
/// <param name="Frame">Khung chi phí đầu phiếu.</param>
/// <param name="RejectReason">Lý do từ chối gần nhất.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Thời điểm lập.</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="ApprovedAt">Thời điểm duyệt đủ.</param>
/// <param name="Lines">Các dòng.</param>
/// <param name="Approvals">Lịch sử duyệt.</param>
/// <param name="Actions">Thao tác được phép.</param>
/// <param name="Version">Phiên bản dòng (chống ghi đè).</param>
public sealed record PurchaseSummaryDto(
    Guid Id, string No, DateOnly Date, string Form, string Status, Guid DepartmentId, string? DepartmentName, Guid? ConstructionProjectId,
    string? Note, decimal TotalAmount, PurchaseSummaryFrameDto Frame, string? RejectReason, string? CancelReason, DateTimeOffset CreatedAt,
    string? CreatedByName, DateTimeOffset? ApprovedAt, IReadOnlyList<PurchaseSummaryLineDto> Lines, IReadOnlyList<ApprovalStepDto> Approvals,
    PurchaseDocumentActions Actions, uint Version);

/// <summary>Dòng đề nghị đã duyệt còn cần mua, chưa thuộc bảng tổng hợp nào chưa hủy – để chọn vào bảng.</summary>
/// <param name="RequestLineId">Dòng đề nghị.</param>
/// <param name="RequestId">Đề nghị.</param>
/// <param name="RequestNo">Số đề nghị.</param>
/// <param name="RequestDate">Ngày đề nghị.</param>
/// <param name="RequestCategory">Loại mua.</param>
/// <param name="DepartmentId">Bộ phận.</param>
/// <param name="DepartmentName">Tên bộ phận.</param>
/// <param name="ConstructionProjectId">Công trình (loại A).</param>
/// <param name="ConstructionProjectCode">Mã công trình.</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="ItemCode">Mã hàng.</param>
/// <param name="ItemName">Tên hàng.</param>
/// <param name="UomId">ĐVT.</param>
/// <param name="UomCode">Mã ĐVT.</param>
/// <param name="OpenQty">SL còn cần mua theo ĐVT dòng.</param>
/// <param name="EstPrice">Đơn giá tham khảo.</param>
/// <param name="Amount">Thành tiền theo SL còn cần mua.</param>
/// <param name="NeededDate">Ngày cần.</param>
public sealed record PurchaseSummaryCandidateDto(
    Guid RequestLineId, Guid RequestId, string RequestNo, DateOnly RequestDate, string RequestCategory, Guid? DepartmentId, string? DepartmentName,
    Guid? ConstructionProjectId, string? ConstructionProjectCode, Guid ItemId, string ItemCode, string ItemName, Guid UomId, string UomCode,
    decimal OpenQty, decimal EstPrice, decimal Amount, DateOnly? NeededDate);

/// <summary>Lọc dòng đề nghị để chọn vào bảng tổng hợp.</summary>
/// <param name="Form">Construction (2A) hoặc Goods (2B); trống = mọi loại.</param>
/// <param name="DepartmentId">Chỉ bộ phận này.</param>
/// <param name="ConstructionProjectId">Chỉ công trình này (2A).</param>
/// <param name="SummaryId">Bảng đang sửa – dòng đã ở chính bảng này vẫn hiện.</param>
public sealed record PurchaseSummaryCandidateQuery(string? Form, Guid? DepartmentId, Guid? ConstructionProjectId, Guid? SummaryId);

/// <summary>Dòng gửi lên khi lưu bảng tổng hợp.</summary>
/// <param name="RequestLineId">Dòng đề nghị.</param>
/// <param name="PurchasingUnitId">Đơn vị mua.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record SavePurchaseSummaryLineRequest(Guid RequestLineId, Guid PurchasingUnitId, string? Note);

/// <summary>Lập / sửa bảng tổng hợp (nháp).</summary>
/// <param name="Date">Ngày lập.</param>
/// <param name="Form">Construction (2A) hoặc Goods (2B).</param>
/// <param name="DepartmentId">Bộ phận đề xuất.</param>
/// <param name="ConstructionProjectId">Công trình (bắt buộc với 2A).</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Các dòng.</param>
public sealed record SavePurchaseSummaryRequest(
    DateOnly Date, string Form, Guid DepartmentId, Guid? ConstructionProjectId, string? Note, IReadOnlyList<SavePurchaseSummaryLineRequest> Lines);

/// <summary>
/// Bảng tổng hợp đề xuất mua 2A/2B của công ty đang làm việc (lát Y5b, design.md mục 19.3 bước 2): Phòng CUVT chọn dòng đề nghị đã duyệt còn cần
/// mua, gán đơn vị mua, trình duyệt theo bộ cấp BTH. Phạm vi công ty, nhóm hàng đi qua <see cref="Common.ICurrentUserScope"/>.
/// </summary>
public interface IPurchaseSummaryService
{
    /// <summary>Danh sách bảng tổng hợp, mới nhất trước.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tối đa 2000 phiếu.</returns>
    Task<IReadOnlyList<PurchaseSummaryListItemDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Chi tiết bảng tổng hợp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có ở công ty đang làm việc.</exception>
    Task<PurchaseSummaryDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Dòng đề nghị đã duyệt còn cần mua, chưa ở bảng tổng hợp chưa hủy nào (trừ bảng đang sửa).</summary>
    /// <param name="query">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Theo ngày, số đề nghị, số dòng.</returns>
    Task<IReadOnlyList<PurchaseSummaryCandidateDto>> CandidatesAsync(PurchaseSummaryCandidateQuery query, CancellationToken cancellationToken);

    /// <summary>Lập bảng tổng hợp nháp.</summary>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu vừa lập.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai quy tắc (biểu mẫu, bộ phận, dòng đã ở bảng khác…).</exception>
    Task<PurchaseSummaryDto> CreateAsync(SavePurchaseSummaryRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa bảng tổng hợp nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi sửa.</returns>
    Task<PurchaseSummaryDto> UpdateAsync(Guid id, SavePurchaseSummaryRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt theo bộ cấp BTH.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi gửi.</returns>
    Task<PurchaseSummaryDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp đang chờ.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi duyệt.</returns>
    Task<PurchaseSummaryDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối: phiếu về nháp kèm lý do.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến (bắt buộc).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi từ chối.</returns>
    Task<PurchaseSummaryDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Rút về nháp – người lập (hoặc quản trị), phiếu đang chờ duyệt.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu về nháp.</returns>
    Task<PurchaseSummaryDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy phiếu (design.md mục 20.2a): nháp – người lập; đã gửi duyệt trở đi – chỉ quản trị; bắt buộc lý do.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do hủy.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hủy.</returns>
    Task<PurchaseSummaryDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập / sửa bảng tổng hợp. Quy tắc nghiệp vụ (biểu mẫu – bộ phận – công trình – dòng) do Domain và dịch vụ kiểm tra.</summary>
public sealed class SavePurchaseSummaryRequestValidator : AbstractValidator<SavePurchaseSummaryRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SavePurchaseSummaryRequestValidator()
    {
        RuleFor(x => x.Form).NotEmpty().WithMessage("Hãy chọn biểu mẫu 2A hoặc 2B.");
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Hãy chọn bộ phận đề xuất.");
        RuleFor(x => x.Note).MaximumLength(PurchaseSummary.NoteMaxLength);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.RequestLineId).NotEmpty().WithMessage("Có dòng chưa chọn dòng đề nghị.");
            line.RuleFor(l => l.PurchasingUnitId).NotEmpty().WithMessage("Có dòng chưa chọn đơn vị mua.");
            line.RuleFor(l => l.Note).MaximumLength(PurchaseSummary.LineNoteMaxLength);
        });
    }
}
