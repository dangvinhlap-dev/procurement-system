using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Domain.Common;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Excel;

/// <summary>
/// Nhập/xuất danh mục bằng Excel (xem <see cref="IExcelTransfer"/>).
/// Nhập: cả file chạy trong MỘT giao dịch, mỗi dòng một savepoint. Dòng lỗi thì quay về savepoint của nó (các dòng khác vẫn
/// được kiểm tra tiếp để báo hết lỗi một lần); cuối cùng chỉ commit khi người dùng chọn "Nhập thật" và không có lỗi nào –
/// còn lại rollback, nên "Kiểm tra thử" chạy đúng như nhập thật (cùng quy tắc, cùng kiểm tra trùng) mà không lưu gì.
/// </summary>
public sealed class ExcelTransfer : IExcelTransfer
{
    /// <summary>Số dòng tối đa mỗi file – nhập từng dòng qua dịch vụ nên file quá lớn sẽ chạy rất lâu; chia nhỏ file.</summary>
    public const int MaxRows = 5000;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IEnumerable<ExcelCatalog> _catalogs;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext (dùng chung với các dịch vụ danh mục trong cùng request – cùng giao dịch).</param>
    /// <param name="scope">Phạm vi của người thao tác.</param>
    /// <param name="catalogs">Các danh mục hỗ trợ Excel.</param>
    internal ExcelTransfer(ProcDbContext db, ICurrentUserScope scope, IEnumerable<ExcelCatalog> catalogs)
    {
        _db = db;
        _scope = scope;
        _catalogs = catalogs;
    }

    /// <inheritdoc />
    public async Task<ExcelFile> ExportAsync(string catalog, bool templateOnly, CancellationToken cancellationToken)
    {
        var definition = Find(catalog);
        _scope.EnsurePermission(definition.ViewPermission);
        var rows = templateOnly ? [] : await definition.ExportAsync(cancellationToken);
        var content = ExcelWriter.Write(definition.SheetName, definition.Columns, rows, Guide(definition));
        return new ExcelFile(templateOnly ? $"mau-{definition.FileName}.xlsx" : $"{definition.FileName}.xlsx", content);
    }

    /// <inheritdoc />
    public async Task<ExcelImportResult> ImportAsync(string catalog, Stream content, bool commit, CancellationToken cancellationToken)
    {
        var definition = Find(catalog);
        _scope.EnsurePermission(definition.ManagePermission);

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(definition, "Không đọc được file. Hãy dùng file Excel .xlsx (tải file mẫu ở nút Nhập Excel).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => ExcelHeaders.Normalize(s.Name) == ExcelHeaders.Normalize(definition.SheetName))
                ?? workbook.Worksheets.First();
            var columns = ReadHeader(sheet);
            var missing = definition.Columns.Where(c => c.Required && !columns.ContainsKey(ExcelHeaders.Normalize(c.Header))).Select(c => c.Header).ToList();
            if (missing.Count > 0)
                return Failed(definition, $"File thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu của danh mục {definition.SheetName}.");

            var rows = sheet.RowsUsed().Where(r => r.RowNumber() > 1).Select(r => new ExcelRowReader(r, columns)).Where(r => !r.IsEmpty).ToList();
            if (rows.Count > MaxRows)
                return Failed(definition, $"File có {rows.Count} dòng, tối đa {MaxRows} dòng mỗi lần nhập. Hãy chia nhỏ file.");

            return await ImportRowsAsync(definition, rows, commit, cancellationToken);
        }
    }

    private async Task<ExcelImportResult> ImportRowsAsync(
        ExcelCatalog definition, List<ExcelRowReader> rows, bool commit, CancellationToken cancellationToken)
    {
        var errors = new List<ExcelImportError>();
        var created = 0;
        var updated = 0;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in rows)
        {
            var code = SafeCode(definition, row);
            // Cùng một mã hai dòng thì dòng sau ghi đè dòng trước – nhiều khả năng là gõ nhầm, báo lỗi cho người dùng xem lại.
            if (code is not null && !seen.TryAdd(code, row.RowNumber))
            {
                errors.Add(new ExcelImportError(row.RowNumber, code, $"Mã {code} trùng với dòng {seen[code]}."));
                continue;
            }

            await transaction.CreateSavepointAsync("row", cancellationToken);
            try
            {
                if (await definition.ImportRowAsync(row, cancellationToken)) created++;
                else updated++;
                await transaction.ReleaseSavepointAsync("row", cancellationToken);
            }
            catch (ForbiddenAccessException ex)
            {
                // Không có quyền (ví dụ không ở tổng công ty) thì mọi dòng đều lỗi như nhau – báo một lần rồi dừng.
                await transaction.RollbackAsync(cancellationToken);
                return new ExcelImportResult(definition.Key, rows.Count, 0, 0, [new ExcelImportError(0, null, ex.Message)], false);
            }
            catch (Exception ex) when (Describe(ex) is { } message)
            {
                await transaction.RollbackToSavepointAsync("row", cancellationToken);
                // Bản ghi lỗi có thể còn trong ChangeTracker; xóa để không lưu kèm ở dòng sau.
                _db.ChangeTracker.Clear();
                errors.Add(new ExcelImportError(row.RowNumber, code, message));
            }
        }

        var committed = commit && errors.Count == 0;
        if (committed) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return new ExcelImportResult(definition.Key, rows.Count, created, updated, errors, committed);
    }

    /// <summary>Lỗi nghiệp vụ/dữ liệu của một dòng thành thông điệp; lỗi khác (lỗi hệ thống) để nổi lên thành 500.</summary>
    private static string? Describe(Exception ex) => ex switch
    {
        ExcelRowException or BusinessRuleException or NotFoundException => ex.Message,
        _ => DuplicateKeyMessages.Describe(ex),
    };

    private static string? SafeCode(ExcelCatalog definition, ExcelRowReader row)
    {
        try
        {
            return definition.CodeOf(row);
        }
        catch (ExcelRowException)
        {
            return null;
        }
    }

    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var header = ExcelHeaders.Normalize(cell.GetFormattedString());
            if (header.Length > 0) columns.TryAdd(header, cell.Address.ColumnNumber);
        }

        return columns;
    }

    private static ExcelImportResult Failed(ExcelCatalog definition, string message) =>
        new(definition.Key, 0, 0, 0, [new ExcelImportError(0, null, message)], false);

    private static List<string> Guide(ExcelCatalog definition) =>
    [
        $"Danh mục: {definition.SheetName}. Dòng 1 là tiêu đề cột (cột có dấu * là bắt buộc), dữ liệu từ dòng 2.",
        "Nhập theo mã: mã đã có thì cập nhật, chưa có thì thêm mới. Không xóa bản ghi nào; muốn ngừng dùng ghi \"Không\" ở cột Còn dùng.",
        "Bấm \"Kiểm tra thử\" để xem lỗi trước; \"Nhập thật\" chỉ lưu khi mọi dòng đều đúng.",
        $"Tối đa {MaxRows} dòng mỗi lần nhập. Các cột mã để dạng Text để không mất số 0 đầu.",
        .. definition.Guide,
    ];

    private ExcelCatalog Find(string catalog) =>
        _catalogs.FirstOrDefault(c => string.Equals(c.Key, catalog, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException($"Danh mục {catalog} chưa hỗ trợ nhập/xuất Excel.");
}
