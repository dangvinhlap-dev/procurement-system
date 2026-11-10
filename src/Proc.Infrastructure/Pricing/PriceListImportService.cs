using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Pricing;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Nhập Excel bảng giá NCC (design.md mục 8.5, làm theo mẫu nhập hợp đồng – lát B4c). Cả file chạy trong MỘT giao dịch, mỗi bảng giá một
/// savepoint: bảng lỗi quay về savepoint của nó để vẫn kiểm tra tiếp các bảng khác; chỉ commit khi nhập thật và không có lỗi. Mỗi bảng giá
/// tạo qua <see cref="IPriceListService.CreateAsync"/> – cùng quy tắc với lập tay (NCC không ở danh sách đen, "toàn tập đoàn" chỉ ở tổng
/// công ty, ĐVT quy đổi, thuế suất, không trùng bậc…), trạng thái Nháp, phải gửi duyệt như thường (lát B1: bảng giá phải duyệt mới hiệu lực).
/// </summary>
public sealed partial class PriceListImportService : IPriceListImportService
{
    /// <summary>Số dòng tối đa mỗi file – mỗi bảng giá tạo qua dịch vụ nên file quá lớn chạy rất lâu; chia nhỏ file.</summary>
    public const int MaxRows = 5000;

    private const string SheetName = "Bảng giá";
    private const string SupplierCode = "Mã NCC";
    private const string Title = "Tên bảng giá";
    private const string GroupWide = "Toàn tập đoàn";
    private const string ValidFrom = "Hiệu lực từ";
    private const string ValidTo = "Hiệu lực đến";
    private const string DeliveryTerms = "Điều kiện giao hàng";
    private const string Note = "Ghi chú";
    private const string ItemCode = "Mã hàng";
    private const string UomCode = "ĐVT";
    private const string MinQty = "SL từ";
    private const string UnitPrice = "Đơn giá";
    private const string DiscountPct = "% CK";
    private const string VatCode = "Thuế suất";
    private const string LineNote = "Ghi chú dòng";

    private enum Kind { Text, Date, Bool }

    /// <summary>Cột đầu bảng giá (trừ mã NCC, tên – là khóa gộp): ghi ở dòng đầu; dòng sau để trống hoặc ghi giống hệt.</summary>
    private static readonly (string Header, Kind Kind)[] HeaderColumns =
    [
        (GroupWide, Kind.Bool), (ValidFrom, Kind.Date), (ValidTo, Kind.Date), (DeliveryTerms, Kind.Text), (Note, Kind.Text),
    ];

    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        new(SupplierCode, "Mã NCC trong danh mục (NCC không ở danh sách đen). Các dòng cùng Mã NCC + Tên bảng giá là một bảng giá.",
            Required: true, Text: true),
        new(Title, "Tên bảng giá, tối đa 200 ký tự.", Required: true),
        new(GroupWide, "Có/Không – áp dụng toàn tập đoàn (chỉ khi đang làm việc ở tổng công ty). Để trống = Không."),
        new(ValidFrom, "dd/MM/yyyy. Số bảng giá PRL cấp theo ngày này.", Required: true),
        new(ValidTo, "dd/MM/yyyy, không trước ngày Hiệu lực từ; để trống = không thời hạn."),
        new(DeliveryTerms, "Điều kiện giao hàng, tối đa 200 ký tự."),
        new(Note, "Ghi chú của bảng giá."),
        new(ItemCode, "Mã hàng trong danh mục (trong phạm vi nhóm hàng của bạn).", Required: true, Text: true),
        new(UomCode, "Mã ĐVT: ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.", Required: true, Text: true),
        new(MinQty, "Bậc số lượng: giá áp khi SL đặt (theo ĐVT dòng) ≥ giá trị này. Để trống = 0 (mọi số lượng)."),
        new(UnitPrice, "Đơn giá VND trước chiết khấu, trước VAT, ≥ 0.", Required: true),
        new(DiscountPct, "% chiết khấu 0–100. Để trống = 0."),
        new(VatCode, $"Thuế suất: {string.Join(", ", VatRates.All)}. Để trống = theo mặt hàng.", Text: true),
        new(LineNote, "Ghi chú dòng."),
    ];

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IPriceListService _priceLists;
    private readonly IValidator<SavePriceListRequest> _validator;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (dùng chung với <paramref name="priceLists"/> trong cùng request – cùng giao dịch).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="priceLists">Dịch vụ bảng giá – tạo từng bảng theo đúng quy tắc lập tay.</param>
    /// <param name="validator">Validator của API lưu bảng giá (độ dài các ô).</param>
    public PriceListImportService(ProcDbContext db, ICurrentUserScope scope, IPriceListService priceLists, IValidator<SavePriceListRequest> validator)
    {
        _db = db;
        _scope = scope;
        _priceLists = priceLists;
        _validator = validator;
    }

    /// <inheritdoc />
    public ExcelFile GetTemplate()
    {
        _scope.EnsurePermission(Permissions.Pricing.PriceListManage);
        List<string> guide =
        [
            "Nhập bảng giá NCC vào công ty đang làm việc. Bảng giá nhập vào ở trạng thái Nháp – kiểm tra rồi gửi duyệt như lập tay.",
            "Dòng 1 là tiêu đề cột (cột có dấu * là bắt buộc), dữ liệu từ dòng 2. Mỗi dòng một bậc giá (mặt hàng × ĐVT × SL từ); các dòng cùng Mã NCC + Tên bảng giá là một bảng giá.",
            "Cột đầu bảng giá (Toàn tập đoàn … Ghi chú) ghi ở dòng đầu của bảng; các dòng sau để trống hoặc ghi giống hệt (khác thì báo lỗi).",
            "Không cập nhật bảng giá đã có: bảng cùng NCC, cùng tên, cùng ngày hiệu lực (chưa ngừng) đã có thì báo lỗi – sửa trên màn hình.",
            $"Bấm \"Kiểm tra thử\" để xem lỗi trước; \"Nhập thật\" chỉ lưu khi mọi bảng giá đều đúng. Tối đa {MaxRows} dòng mỗi lần nhập.",
        ];
        return new ExcelFile("mau-bang-gia-ncc.xlsx", ExcelWriter.Write(SheetName, Columns, [], guide));
    }

    /// <inheritdoc />
    public async Task<PriceListImportResult> ImportAsync(Stream content, bool commit, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Pricing.PriceListManage);
        // Bảng giá thuộc công ty đang làm việc (công ty lập) – không nhận công ty từ file.
        _scope.RequireCompany();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed("Không đọc được file. Hãy dùng file Excel .xlsx (tải file mẫu ở hộp thoại Nhập Excel).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => ExcelHeaders.Normalize(s.Name) == ExcelHeaders.Normalize(SheetName)) ?? workbook.Worksheets.First();
            var columns = ExcelTransfer.ReadHeader(sheet);
            var missing = Columns.Where(c => c.Required && !columns.ContainsKey(ExcelHeaders.Normalize(c.Header))).Select(c => c.Header).ToList();
            if (missing.Count > 0) return Failed($"File thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu bảng giá NCC.");

            var rows = sheet.RowsUsed().Where(r => r.RowNumber() > 1).Select(r => new ExcelRowReader(r, columns)).Where(r => !r.IsEmpty).ToList();
            if (rows.Count > MaxRows) return Failed($"File có {rows.Count} dòng, tối đa {MaxRows} dòng mỗi lần nhập. Hãy chia nhỏ file.");
            return await ImportRowsAsync(rows, commit, cancellationToken);
        }
    }

    /// <summary>Kiểm tra và nhập các dòng Excel bảng giá (gộp theo mã NCC + tên bảng giá); commit = false thì chỉ kiểm tra.</summary>
    private async Task<PriceListImportResult> ImportRowsAsync(List<ExcelRowReader> rows, bool commit, CancellationToken cancellationToken)
    {
        var errors = new List<ExcelImportError>();
        // Gộp theo (mã NCC, tên bảng giá) – không phân biệt hoa thường, giữ thứ tự xuất hiện; dòng thiếu khóa báo lỗi riêng.
        var groups = new List<(string SupplierCode, List<ExcelRowReader> Rows)>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Text(SupplierCode) is not { } supplierCode || row.Text(Title) is not { } title)
            {
                errors.Add(new ExcelImportError(row.RowNumber, row.Text(SupplierCode), $"Thiếu \"{SupplierCode}\" hoặc \"{Title}\"."));
                continue;
            }

            var key = $"{supplierCode}\u001f{title}";
            if (!index.TryGetValue(key, out var at))
            {
                index[key] = at = groups.Count;
                groups.Add((supplierCode, []));
            }

            groups[at].Rows.Add(row);
        }

        var (priceLists, lines) = (0, 0);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (supplierCode, groupRows) in groups)
        {
            await transaction.CreateSavepointAsync("price_list", cancellationToken);
            try
            {
                var request = await ReadPriceListAsync(groupRows, cancellationToken);
                await _priceLists.CreateAsync(request, cancellationToken);
                await transaction.ReleaseSavepointAsync("price_list", cancellationToken);
                _db.ChangeTracker.Clear();
                priceLists++;
                lines += request.Lines.Count;
            }
            catch (ForbiddenAccessException ex)
            {
                // Mặt hàng ngoài phạm vi, không có quyền… thì báo một lần rồi dừng như nhập danh mục.
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                return new PriceListImportResult(rows.Count, 0, 0, [new ExcelImportError(0, null, ex.Message)], false);
            }
            catch (Exception ex) when (Describe(ex, groupRows) is { } error)
            {
                await transaction.RollbackToSavepointAsync("price_list", cancellationToken);
                // Bảng lỗi có thể còn trong ChangeTracker; xóa để không lưu kèm ở bảng sau.
                _db.ChangeTracker.Clear();
                errors.Add(new ExcelImportError(error.Row, supplierCode, error.Message));
            }
        }

        var committed = commit && errors.Count == 0;
        if (committed) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        errors.Sort((a, b) => a.Row.CompareTo(b.Row));
        return new PriceListImportResult(rows.Count, priceLists, lines, errors, committed);
    }

    /// <summary>
    /// Đọc một bảng giá: đầu ở dòng đầu (dòng sau trống hoặc giống hệt), tra mã NCC, mã hàng, mã ĐVT; chặn nhập lại bảng đã có (cùng NCC,
    /// cùng tên, cùng ngày hiệu lực, chưa ngừng – ở công ty đang làm việc). Kiểm tra độ dài bằng validator của API.
    /// </summary>
    /// <exception cref="RowException">Khi một dòng sai – kèm số dòng Excel.</exception>
    private async Task<SavePriceListRequest> ReadPriceListAsync(List<ExcelRowReader> groupRows, CancellationToken cancellationToken)
    {
        var first = groupRows[0];
        foreach (var row in groupRows.Skip(1)) At(row, () => CheckSameHeader(first, row));

        var (supplierCode, title, groupWide, from, to) = At(first, () => (
            first.RequiredText(SupplierCode), first.RequiredText(Title), first.Bool(GroupWide) ?? false,
            first.Date(ValidFrom) ?? throw new ExcelRowException($"Thiếu \"{ValidFrom}\"."), first.Date(ValidTo)));
        var supplierId = await _db.Suppliers.Where(s => s.Code == supplierCode).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(first.RowNumber, $"Không có NCC mã {supplierCode}.");
        // Nhập lại cùng file sẽ tạo bảng giá trùng – chặn; muốn đổi bảng đã có thì sửa trên màn hình (nháp) hoặc ngừng rồi lập bảng mới.
        // Filter công ty tự áp: chỉ so với bảng giá của công ty đang làm việc.
        if (await _db.SupplierPriceLists
                .Where(p => p.SupplierId == supplierId && p.Title == title && p.ValidFrom == from && p.Status != PriceListStatus.Inactive)
                .Select(p => p.No).FirstOrDefaultAsync(cancellationToken) is { } existing)
            throw new RowException(first.RowNumber, $"Đã có bảng giá {existing} \"{title}\" của NCC {supplierCode} hiệu lực từ {from:dd/MM/yyyy} – không nhập lại.");

        var lines = new List<SavePriceListLineRequest>();
        foreach (var row in groupRows) lines.Add(await ReadLineAsync(row, cancellationToken));

        var request = new SavePriceListRequest(supplierId, title, groupWide, from, to, first.Text(DeliveryTerms), first.Text(Note), lines);
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new RowException(first.RowNumber, validation.Errors[0].ErrorMessage);
        return request;
    }

    /// <summary>Một bậc giá: mã hàng (trong phạm vi nhóm hàng), mã ĐVT, đơn giá bắt buộc; SL từ, % CK trống = 0; thuế suất trống = theo mặt hàng.</summary>
    private async Task<SavePriceListLineRequest> ReadLineAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var (itemCode, uomCode, min, price, discount) = At(row, () => (
            row.RequiredText(ItemCode), row.RequiredText(UomCode), row.Decimal(MinQty) ?? 0m,
            row.Decimal(UnitPrice) ?? throw new ExcelRowException($"Thiếu \"{UnitPrice}\"."), row.Decimal(DiscountPct) ?? 0m));
        // Filter nhóm hàng tự áp: mặt hàng ngoài phạm vi coi như không có.
        var itemId = await _db.Items.Where(i => i.Code == itemCode).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(row.RowNumber, $"Không có mặt hàng mã {itemCode} (hoặc ngoài phạm vi nhóm hàng của bạn).");
        var uomId = await _db.Uoms.Where(u => u.Code == uomCode).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(row.RowNumber, $"Không có ĐVT mã {uomCode}.");
        // Thuế suất ghi "10%" hay "10" đều nhận; giá trị hợp lệ do miền kiểm tra (VatRates).
        var vat = row.Text(VatCode)?.TrimEnd('%').Trim();
        return new SavePriceListLineRequest(null, itemId, uomId, min, price, discount, vat, row.Text(LineNote));
    }

    /// <summary>Dòng sau của bảng giá: ô đầu bảng nào có ghi thì phải giống dòng đầu (so theo giá trị đã đọc – ngày, có/không, chữ).</summary>
    /// <exception cref="ExcelRowException">Khi khác dòng đầu.</exception>
    private static void CheckSameHeader(ExcelRowReader first, ExcelRowReader row)
    {
        foreach (var (header, kind) in HeaderColumns)
        {
            var value = Read(row, header, kind);
            if (value is not null && !Equals(value, Read(first, header, kind)))
                throw new ExcelRowException($"\"{header}\" khác dòng {first.RowNumber} của cùng bảng giá – để trống hoặc ghi giống dòng đầu.");
        }
    }

    /// <summary>Đọc giá trị một ô theo kiểu (ngày, có/không, chữ).</summary>
    private static object? Read(ExcelRowReader row, string header, Kind kind) => kind switch
    {
        Kind.Date => row.Date(header),
        Kind.Bool => row.Bool(header),
        _ => row.Text(header),
    };

    /// <summary>Chạy hàm đọc ô; lỗi đọc ô đổi thành lỗi kèm số dòng.</summary>
    private static T At<T>(ExcelRowReader row, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (ExcelRowException ex)
        {
            throw new RowException(row.RowNumber, ex.Message);
        }
    }

    /// <summary>Chạy một bước kiểm tra cho dòng Excel; lỗi thì gắn số dòng vào thông báo.</summary>
    private static void At(ExcelRowReader row, Action check) => At(row, () =>
    {
        check();
        return 0;
    });

    /// <summary>
    /// Lỗi nghiệp vụ/dữ liệu thành (dòng Excel, thông điệp); lỗi khác (lỗi hệ thống) để nổi lên thành 500. Lỗi "Dòng k (mã hàng): …" của
    /// bảng giá (k = thứ tự dòng trong bảng, mỗi dòng file là một dòng bảng giá) đổi sang dòng Excel thứ k của bảng.
    /// </summary>
    private static (int Row, string Message)? Describe(Exception ex, List<ExcelRowReader> groupRows)
    {
        if (ex is RowException row) return (row.Row, row.Message);
        var message = ex is BusinessRuleException or NotFoundException ? ex.Message : DuplicateKeyMessages.Describe(ex);
        if (message is null) return null;
        if (LineErrorRegex().Match(message) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var k) && k >= 1 && k <= groupRows.Count)
            return (groupRows[k - 1].RowNumber, m.Groups[2].Value);
        return (groupRows[0].RowNumber, message);
    }

    private static PriceListImportResult Failed(string message) => new(0, 0, 0, [new ExcelImportError(0, null, message)], false);

    /// <summary>"Dòng 3 (VB6205): thuế suất phải là…" – phần mã hàng trong ngoặc có thể không có.</summary>
    [GeneratedRegex(@"^Dòng (\d+)(?: \([^)]*\))?: (.+)$", RegexOptions.Singleline)]
    private static partial Regex LineErrorRegex();

    /// <summary>Lỗi của một dòng Excel cụ thể.</summary>
    private sealed class RowException(int row, string message) : Exception(message)
    {
        public int Row { get; } = row;
    }
}
