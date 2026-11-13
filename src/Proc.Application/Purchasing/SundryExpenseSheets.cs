using FluentValidation;
using Proc.Domain.Purchasing;

namespace Proc.Application.Purchasing;

/// <summary>Bảng kê chi phí tạp vụ trên danh sách (lát Y11a, design.md mục 19.2 loại C).</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số BKT.</param>
/// <param name="Date">Ngày bảng kê.</param>
/// <param name="Status">Trạng thái duyệt (Draft, Submitted, Approved, Cancelled).</param>
/// <param name="DepartmentCode">Mã bộ phận.</param>
/// <param name="DepartmentName">Tên bộ phận.</param>
/// <param name="LineCount">Số khoản chi.</param>
/// <param name="TotalAmount">Tổng bảng kê.</param>
/// <param name="IsOverBudget">Vượt định mức tháng lúc lưu / gửi gần nhất.</param>
/// <param name="PaymentRequestNo">Số đề nghị thanh toán tổng hợp 1C chưa hủy đang giữ bảng kê (lát Y11b); null = chưa đề nghị.</param>
/// <param name="PaymentRequestId">Id đề nghị đó.</param>
/// <param name="CreatedByName">Người lập.</param>
public sealed record SundryExpenseSheetListItemDto(
    Guid Id, string No, DateOnly Date, string Status, string DepartmentCode, string DepartmentName, int LineCount, decimal TotalAmount,
    bool IsOverBudget, string? PaymentRequestNo, Guid? PaymentRequestId, string? CreatedByName);

/// <summary>Một khoản chi.</summary>
/// <param name="Id">Id dòng.</param>
/// <param name="LineNo">STT.</param>
/// <param name="ExpenseDate">Ngày chi.</param>
/// <param name="Description">Nội dung chi.</param>
/// <param name="Vendor">Nơi mua / người bán.</param>
/// <param name="ReceiptNo">Số hóa đơn / chứng từ.</param>
/// <param name="Amount">Số tiền.</param>
public sealed record SundryExpenseLineDto(Guid Id, int LineNo, DateOnly ExpenseDate, string Description, string? Vendor, string? ReceiptNo, decimal Amount);

/// <summary>Chi tiết bảng kê chi phí tạp vụ.</summary>
/// <param name="Id">Id.</param>
/// <param name="No">Số BKT.</param>
/// <param name="Date">Ngày bảng kê.</param>
/// <param name="Status">Trạng thái duyệt.</param>
/// <param name="DepartmentId">Bộ phận.</param>
/// <param name="DepartmentCode">Mã bộ phận.</param>
/// <param name="DepartmentName">Tên bộ phận.</param>
/// <param name="TotalAmount">Tổng bảng kê.</param>
/// <param name="Budget">Định mức tháng (ngân sách bộ phận × tháng) lúc lưu / gửi gần nhất; null = tháng đó bộ phận chưa có định mức.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="PaymentRequestNo">Số đề nghị 1C chưa hủy đang giữ bảng kê.</param>
/// <param name="PaymentRequestId">Id đề nghị đó.</param>
/// <param name="RejectReason">Lý do từ chối gần nhất.</param>
/// <param name="CancelReason">Lý do hủy.</param>
/// <param name="CreatedAt">Lúc lập (UTC).</param>
/// <param name="CreatedByName">Người lập.</param>
/// <param name="ApprovedAt">Lúc duyệt xong (UTC).</param>
/// <param name="Lines">Các khoản chi.</param>
/// <param name="Approvals">Lịch sử duyệt.</param>
/// <param name="Actions">Lệnh được phép.</param>
/// <param name="Version">Phiên bản dòng (chống ghi đè).</param>
public sealed record SundryExpenseSheetDto(
    Guid Id, string No, DateOnly Date, string Status, Guid DepartmentId, string DepartmentCode, string DepartmentName, decimal TotalAmount,
    BudgetCheckDto? Budget, string? Note, string? PaymentRequestNo, Guid? PaymentRequestId, string? RejectReason, string? CancelReason,
    DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset? ApprovedAt, IReadOnlyList<SundryExpenseLineDto> Lines,
    IReadOnlyList<ApprovalStepDto> Approvals, PurchaseDocumentActions Actions, uint Version);

/// <summary>Một khoản chi gửi lên khi lưu.</summary>
/// <param name="ExpenseDate">Ngày chi.</param>
/// <param name="Description">Nội dung chi.</param>
/// <param name="Vendor">Nơi mua / người bán.</param>
/// <param name="ReceiptNo">Số hóa đơn / chứng từ.</param>
/// <param name="Amount">Số tiền.</param>
public sealed record SaveSundryExpenseLineRequest(DateOnly ExpenseDate, string? Description, string? Vendor, string? ReceiptNo, decimal Amount);

/// <summary>Lập / sửa bảng kê (nháp).</summary>
/// <param name="Date">Ngày bảng kê.</param>
/// <param name="DepartmentId">Bộ phận.</param>
/// <param name="Note">Ghi chú.</param>
/// <param name="Lines">Các khoản chi.</param>
public sealed record SaveSundryExpenseSheetRequest(DateOnly Date, Guid DepartmentId, string? Note, IReadOnlyList<SaveSundryExpenseLineRequest> Lines);

/// <summary>Bộ lọc danh sách: khoảng ngày bảng kê.</summary>
/// <param name="From">Từ ngày.</param>
/// <param name="To">Đến ngày.</param>
public sealed record SundryExpenseSheetFilter(DateOnly? From, DateOnly? To);

/// <summary>
/// Bảng kê chi phí tạp vụ 2C của công ty đang làm việc (lát Y11a, design.md mục 19.2 loại C): bộ phận kê chi phí lặt vặt đã phát sinh, tính vào
/// định mức tháng, duyệt theo bộ cấp BKT; bảng kê đã duyệt gộp vào đề nghị thanh toán tổng hợp 1C. Phạm vi công ty đi qua
/// <see cref="Common.ICurrentUserScope"/>.
/// </summary>
public interface ISundryExpenseSheetService
{
    /// <summary>Danh sách bảng kê, mới nhất trước.</summary>
    /// <param name="filter">Bộ lọc.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Tối đa 2.000 phiếu.</returns>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền xem.</exception>
    Task<IReadOnlyList<SundryExpenseSheetListItemDto>> ListAsync(SundryExpenseSheetFilter filter, CancellationToken cancellationToken);

    /// <summary>Chi tiết bảng kê.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Chi tiết.</returns>
    /// <exception cref="Common.NotFoundException">Khi không thấy (hoặc ngoài công ty).</exception>
    Task<SundryExpenseSheetDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lập bảng kê nháp (cấp số BKT), kiểm định mức tháng.</summary>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi dữ liệu sai quy tắc.</exception>
    Task<SundryExpenseSheetDto> CreateAsync(SaveSundryExpenseSheetRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa bảng kê nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Dữ liệu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã lưu.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi phiếu không còn nháp hoặc dữ liệu sai.</exception>
    Task<SundryExpenseSheetDto> UpdateAsync(Guid id, SaveSundryExpenseSheetRequest request, CancellationToken cancellationToken);

    /// <summary>Gửi duyệt: kiểm lại định mức tháng, vượt thì thêm chặng TVB (Q5).</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi gửi.</returns>
    /// <exception cref="Domain.Common.BusinessRuleException">Khi không gửi được (không có dòng, thiếu người duyệt động…).</exception>
    Task<SundryExpenseSheetDto> SubmitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Duyệt cấp hiện tại.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Ý kiến.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi duyệt.</returns>
    Task<SundryExpenseSheetDto> ApproveAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Từ chối (về nháp, lý do bắt buộc).</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu sau khi từ chối.</returns>
    Task<SundryExpenseSheetDto> RejectAsync(Guid id, ApprovalActionRequest request, CancellationToken cancellationToken);

    /// <summary>Người lập (hoặc quản trị) rút phiếu chờ duyệt về nháp.</summary>
    /// <param name="id">Id.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu nháp.</returns>
    Task<SundryExpenseSheetDto> RecallAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Hủy bảng kê (nháp – người lập; đã gửi – quản trị; lý do bắt buộc – design.md mục 20.2a). Đang ở đề nghị 1C chưa hủy thì chặn.</summary>
    /// <param name="id">Id.</param>
    /// <param name="request">Lý do.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Phiếu đã hủy.</returns>
    Task<SundryExpenseSheetDto> CancelAsync(Guid id, CancelDocumentRequest request, CancellationToken cancellationToken);
}

/// <summary>Kiểm tra lập / sửa bảng kê. Quy tắc nghiệp vụ (ngày chi, số tiền) do Domain kiểm tra.</summary>
public sealed class SaveSundryExpenseSheetRequestValidator : AbstractValidator<SaveSundryExpenseSheetRequest>
{
    /// <summary>Khai báo quy tắc.</summary>
    public SaveSundryExpenseSheetRequestValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Hãy chọn bộ phận phát sinh chi phí.");
        RuleFor(x => x.Note).MaximumLength(SundryExpenseSheet.NoteMaxLength);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).MaximumLength(SundryExpenseSheet.DescriptionMaxLength);
            line.RuleFor(l => l.Vendor).MaximumLength(SundryExpenseSheet.VendorMaxLength);
            line.RuleFor(l => l.ReceiptNo).MaximumLength(SundryExpenseSheet.ReceiptNoMaxLength);
        });
    }
}
