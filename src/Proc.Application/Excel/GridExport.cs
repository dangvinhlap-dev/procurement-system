using FluentValidation;

namespace Proc.Application.Excel;

/// <summary>Một cột của lưới cần xuất Excel.</summary>
/// <param name="Title">Tiêu đề cột.</param>
/// <param name="Type">Kiểu cột của DataGrid: text, status, date, datetime, money, quantity – quyết định kiểu ô và định dạng số.</param>
/// <param name="Width">Độ rộng trên màn hình (px), để đặt độ rộng cột Excel tương ứng.</param>
public sealed record GridExportColumn(string Title, string? Type, int? Width);

/// <summary>
/// Xuất một danh sách hoặc báo cáo đang xem ra Excel (design.md mục 11: mọi báo cáo xuất được Excel). Giao diện gửi đúng các cột đang hiện
/// và các dòng đã lọc; giá trị thô dạng chữ: số kiểu quốc tế (1234.5), ngày yyyy-MM-dd, ngày giờ ISO 8601 UTC.
/// </summary>
/// <param name="Title">Tiêu đề báo cáo (dòng 1 của sheet).</param>
/// <param name="Subtitle">Dòng mô tả điều kiện lọc (kỳ, kho…); null nếu không có.</param>
/// <param name="FileName">Tên file không đuôi, ví dụ hang-cham-luan-chuyen.</param>
/// <param name="Columns">Các cột theo thứ tự hiển thị.</param>
/// <param name="Rows">Các dòng; mỗi dòng có đúng số ô bằng số cột.</param>
public sealed record GridExportRequest(
    string Title, string? Subtitle, string FileName, IReadOnlyList<GridExportColumn> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>Dựng file Excel từ lưới đang xem. Không đọc CSDL: chỉ định dạng lại dữ liệu người dùng đã được xem.</summary>
public interface IGridExcelExporter
{
    /// <summary>Dựng file .xlsx.</summary>
    /// <param name="request">Lưới cần xuất (đã kiểm tra hợp lệ).</param>
    /// <returns>File để tải.</returns>
    ExcelFile Build(GridExportRequest request);
}

/// <summary>Kiểm tra yêu cầu xuất lưới – giới hạn kích thước để một request không làm đầy bộ nhớ server.</summary>
public sealed class GridExportRequestValidator : AbstractValidator<GridExportRequest>
{
    /// <summary>Số cột tối đa.</summary>
    public const int MaxColumns = 100;

    /// <summary>Số dòng tối đa – bằng giới hạn dòng lớn nhất của các màn hình (tồn kho 5.000) nhân 10 cho dư.</summary>
    public const int MaxRows = 50_000;

    private static readonly HashSet<string> Types = ["text", "status", "date", "datetime", "money", "quantity"];

    /// <summary>Khai báo quy tắc.</summary>
    public GridExportRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Thiếu tiêu đề báo cáo.").MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(500);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Tên file chỉ gồm chữ thường không dấu, số và dấu gạch ngang.");
        RuleFor(x => x.Columns).NotEmpty().WithMessage("Không có cột nào để xuất.")
            .Must(c => c.Count <= MaxColumns).WithMessage($"Xuất tối đa {MaxColumns} cột.");
        RuleForEach(x => x.Columns).ChildRules(c =>
        {
            c.RuleFor(x => x.Title).NotNull().MaximumLength(200);
            c.RuleFor(x => x.Type).Must(t => t is null || Types.Contains(t)).WithMessage("Kiểu cột không hợp lệ.");
        });
        RuleFor(x => x.Rows).Must(r => r.Count <= MaxRows).WithMessage("Xuất tối đa 50.000 dòng mỗi lần – hãy lọc hẹp hơn.");
        RuleFor(x => x).Must(x => x.Rows.All(r => r.Count == x.Columns.Count))
            .WithMessage("Số ô của mỗi dòng phải bằng số cột.").When(x => x.Columns is { Count: > 0 });
    }
}
