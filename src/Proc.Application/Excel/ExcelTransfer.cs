namespace Proc.Application.Excel;

/// <summary>Một lỗi khi nhập Excel.</summary>
/// <param name="Row">Số dòng trên sheet (dòng tiêu đề là 1), 0 nếu lỗi của cả file.</param>
/// <param name="Code">Mã bản ghi của dòng (nếu đọc được) để người dùng tìm nhanh.</param>
/// <param name="Message">Thông điệp tiếng Việt.</param>
public sealed record ExcelImportError(int Row, string? Code, string Message);

/// <summary>Kết quả nhập Excel.</summary>
/// <param name="Catalog">Mã danh mục.</param>
/// <param name="TotalRows">Số dòng dữ liệu (bỏ dòng trống).</param>
/// <param name="Created">Số dòng sẽ thêm / đã thêm.</param>
/// <param name="Updated">Số dòng sẽ cập nhật / đã cập nhật.</param>
/// <param name="Errors">Các lỗi; có lỗi thì không dòng nào được lưu.</param>
/// <param name="Committed">true khi đã lưu thật; false khi chỉ kiểm tra thử hoặc có lỗi.</param>
public sealed record ExcelImportResult(
    string Catalog, int TotalRows, int Created, int Updated, IReadOnlyList<ExcelImportError> Errors, bool Committed);

/// <summary>File Excel trả về để tải.</summary>
/// <param name="FileName">Tên file gợi ý, ví dụ hang-hoa.xlsx.</param>
/// <param name="Content">Nội dung file.</param>
public sealed record ExcelFile(string FileName, byte[] Content);

/// <summary>Các danh mục hỗ trợ nhập/xuất Excel (mã dùng trên đường dẫn API).</summary>
public static class ExcelCatalogs
{
    /// <summary>Đơn vị tính.</summary>
    public const string Uoms = "uoms";

    /// <summary>Nhóm hàng.</summary>
    public const string ItemCategories = "item-categories";

    /// <summary>Hàng hóa.</summary>
    public const string Items = "items";

    /// <summary>Nhóm NCC.</summary>
    public const string SupplierGroups = "supplier-groups";

    /// <summary>Nhà cung cấp.</summary>
    public const string Suppliers = "suppliers";
}

/// <summary>
/// Nhập/xuất danh mục bằng Excel (design.md mục 10.9, 13.2 – giai đoạn 1: nhập danh mục thật của công ty thí điểm).
/// File xuất ra cũng là file mẫu để nhập: sửa xong nhập lại được. Nhập theo mã: mã đã có thì cập nhật, chưa có thì thêm.
/// Mỗi dòng đi qua đúng các quy tắc của màn hình nhập tay; có lỗi ở bất kỳ dòng nào thì cả file không được lưu.
/// </summary>
public interface IExcelTransfer
{
    /// <summary>
    /// Xuất danh mục (trong phạm vi người dùng) ra Excel; <paramref name="templateOnly"/> = true chỉ có tiêu đề và hướng dẫn.
    /// </summary>
    /// <param name="catalog">Mã danh mục (<see cref="ExcelCatalogs"/>).</param>
    /// <param name="templateOnly">Chỉ xuất file mẫu trống.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>File .xlsx.</returns>
    /// <exception cref="Common.NotFoundException">Khi danh mục không hỗ trợ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền xem danh mục.</exception>
    Task<ExcelFile> ExportAsync(string catalog, bool templateOnly, CancellationToken cancellationToken);

    /// <summary>
    /// Nhập danh mục từ Excel.
    /// </summary>
    /// <param name="catalog">Mã danh mục.</param>
    /// <param name="content">Nội dung file .xlsx.</param>
    /// <param name="commit">false = kiểm tra thử (không lưu gì); true = lưu nếu không có lỗi.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số dòng thêm/cập nhật và các lỗi.</returns>
    /// <exception cref="Common.NotFoundException">Khi danh mục không hỗ trợ.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền quản lý danh mục.</exception>
    Task<ExcelImportResult> ImportAsync(string catalog, Stream content, bool commit, CancellationToken cancellationToken);
}
