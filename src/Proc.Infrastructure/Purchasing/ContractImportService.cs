using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Application.Purchasing;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Purchasing;

/// <summary>
/// Nhập Excel hợp đồng nguyên tắc hiện hữu (lát B4c – design.md mục 18.7). Cả file chạy trong MỘT giao dịch, mỗi hợp đồng một savepoint
/// (như <see cref="ExcelTransfer"/>): hợp đồng lỗi quay về savepoint của nó để vẫn kiểm tra tiếp các hợp đồng khác; chỉ commit khi nhập thật
/// và không có lỗi. Mỗi hợp đồng tạo qua <see cref="IContractService.CreateAsync"/> – cùng quy tắc với lập tay (giá theo bảng giá, NCC không
/// ở danh sách đen, bộ phận đang dùng…), trạng thái Nháp, phải gửi duyệt như thường (Q4: hợp đồng phải duyệt).
/// </summary>
public sealed partial class ContractImportService : IContractImportService
{
    /// <summary>Số dòng tối đa mỗi file – mỗi hợp đồng tạo qua dịch vụ nên file quá lớn chạy rất lâu; chia nhỏ file.</summary>
    public const int MaxRows = 5000;

    private const string SheetName = "Hợp đồng";
    private const string PaperNo = "Số HĐ giấy";
    private const string SupplierCode = "Mã NCC";
    private const string PriceListNo = "Số bảng giá";
    private const string Title = "Tên hợp đồng";
    private const string SignDate = "Ngày ký";
    private const string ValidFrom = "Hiệu lực từ";
    private const string ValidTo = "Hiệu lực đến";
    private const string CreditLimit = "Hạn mức công nợ";
    private const string PriceAdjustment = "Điều chỉnh giá";
    private const string Payment = "Thanh toán";
    private const string Delivery = "Giao hàng";
    private const string Discount = "Chiết khấu";
    private const string Sponsorship = "Tài trợ";
    private const string Penalty = "Chế tài";
    private const string Other = "Điều khoản khác";
    private const string Departments = "Bộ phận được đặt";
    private const string Note = "Ghi chú";
    private const string ItemCode = "Mã hàng";
    private const string UomCode = "ĐVT";
    private const string ExpectedQty = "SL dự kiến";
    private const string MinCommitQty = "SL cam kết";
    private const string LineNote = "Ghi chú dòng";

    private enum Kind { Text, Date, Number }

    /// <summary>Cột đầu hợp đồng (trừ số HĐ giấy): ghi ở dòng đầu; dòng sau để trống hoặc ghi giống hệt.</summary>
    private static readonly (string Header, Kind Kind)[] HeaderColumns =
    [
        (SupplierCode, Kind.Text), (PriceListNo, Kind.Text), (Title, Kind.Text), (SignDate, Kind.Date), (ValidFrom, Kind.Date), (ValidTo, Kind.Date),
        (CreditLimit, Kind.Number), (PriceAdjustment, Kind.Text), (Payment, Kind.Text), (Delivery, Kind.Text), (Discount, Kind.Text),
        (Sponsorship, Kind.Text), (Penalty, Kind.Text), (Other, Kind.Text), (Departments, Kind.Text), (Note, Kind.Text),
    ];

    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        new(PaperNo, "Số hợp đồng giấy đã ký. Các dòng cùng số là một hợp đồng (mỗi dòng một mặt hàng × ĐVT).", Required: true, Text: true),
        new(SupplierCode, "Mã NCC trong danh mục (NCC không ở danh sách đen; gửi duyệt cần NCC Đã duyệt).", Required: true, Text: true),
        new(PriceListNo, "Số bảng giá NCC dùng chung: bảng của công ty đang làm việc (chưa ngừng) hoặc bảng toàn tập đoàn đã hiệu lực, cùng NCC.",
            Required: true, Text: true),
        new(Title, "Tên hợp đồng, tối đa 200 ký tự.", Required: true),
        new(SignDate, "dd/MM/yyyy."),
        new(ValidFrom, "dd/MM/yyyy. Số hợp đồng CTR cấp theo ngày này.", Required: true),
        new(ValidTo, "dd/MM/yyyy, không trước ngày Hiệu lực từ.", Required: true),
        new(CreditLimit, "VND, ≥ 0."),
        new(PriceAdjustment, "Cơ chế điều chỉnh giá."),
        new(Payment, "Thanh toán, công nợ."),
        new(Delivery, "Giao hàng (thời gian, địa điểm từng đơn vị)."),
        new(Discount, "Chiết khấu thương mại, theo sản lượng, doanh số."),
        new(Sponsorship, "Tài trợ, hỗ trợ, khuyến mại."),
        new(Penalty, "Chế tài chậm giao, giao sai, hàng lỗi."),
        new(Other, "Điều khoản khác (bảo mật, xung đột lợi ích, chấm dứt, gia hạn, tranh chấp)."),
        new(Departments, "Mã bộ phận được phép đặt hàng, cách nhau dấu phẩy; để trống = mọi bộ phận.", Text: true),
        new(Note, "Ghi chú của hợp đồng."),
        new(ItemCode, "Mã hàng có trong bảng giá. Để trống = dòng chỉ có đầu hợp đồng (hợp đồng chưa có sản lượng dự kiến).", Text: true),
        new(UomCode, "Mã ĐVT có giá trong bảng giá (bắt buộc khi có Mã hàng).", Text: true),
        new(ExpectedQty, "SL dự kiến > 0 (bắt buộc khi có Mã hàng). Đơn giá, % CK lấy từ bảng giá."),
        new(MinCommitQty, "SL cam kết mua tối thiểu, ≤ SL dự kiến; để trống = không cam kết."),
        new(LineNote, "Ghi chú dòng."),
    ];

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IContractService _contracts;
    private readonly IValidator<SaveContractRequest> _validator;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext (dùng chung với <paramref name="contracts"/> trong cùng request – cùng giao dịch).</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="contracts">Dịch vụ hợp đồng – tạo từng hợp đồng theo đúng quy tắc lập tay.</param>
    /// <param name="validator">Validator của API lưu hợp đồng (độ dài các ô).</param>
    public ContractImportService(ProcDbContext db, ICurrentUserScope scope, IContractService contracts, IValidator<SaveContractRequest> validator)
    {
        _db = db;
        _scope = scope;
        _contracts = contracts;
        _validator = validator;
    }

    /// <inheritdoc />
    public ExcelFile GetTemplate()
    {
        _scope.EnsurePermission(Permissions.Purchase.ContractManage);
        List<string> guide =
        [
            "Nhập hợp đồng nguyên tắc đang thực hiện vào công ty đang làm việc. Hợp đồng nhập vào ở trạng thái Nháp – kiểm tra rồi gửi duyệt như lập tay.",
            "Dòng 1 là tiêu đề cột (cột có dấu * là bắt buộc), dữ liệu từ dòng 2. Mỗi dòng một mặt hàng × ĐVT; các dòng cùng Số HĐ giấy là một hợp đồng.",
            "Cột đầu hợp đồng (Mã NCC … Ghi chú) ghi ở dòng đầu của hợp đồng; các dòng sau để trống hoặc ghi giống hệt (khác thì báo lỗi).",
            "Không cập nhật hợp đồng đã có: Số HĐ giấy đã có (chưa hủy) với cùng NCC thì báo lỗi – sửa trên màn hình hoặc lập phụ lục.",
            $"Bấm \"Kiểm tra thử\" để xem lỗi trước; \"Nhập thật\" chỉ lưu khi mọi hợp đồng đều đúng. Tối đa {MaxRows} dòng mỗi lần nhập.",
        ];
        return new ExcelFile("mau-hop-dong-nguyen-tac.xlsx", ExcelWriter.Write(SheetName, Columns, [], guide));
    }

    /// <inheritdoc />
    public async Task<ContractImportResult> ImportAsync(Stream content, bool commit, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Purchase.ContractManage);
        // Hợp đồng thuộc công ty đang làm việc (pháp nhân ký) – không nhận công ty từ file.
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
            if (missing.Count > 0) return Failed($"File thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu hợp đồng nguyên tắc.");

            var rows = sheet.RowsUsed().Where(r => r.RowNumber() > 1).Select(r => new ExcelRowReader(r, columns)).Where(r => !r.IsEmpty).ToList();
            if (rows.Count > MaxRows) return Failed($"File có {rows.Count} dòng, tối đa {MaxRows} dòng mỗi lần nhập. Hãy chia nhỏ file.");
            return await ImportRowsAsync(rows, commit, cancellationToken);
        }
    }

    /// <summary>Kiểm tra và nhập các dòng Excel hợp đồng (gộp theo số HĐ giấy); commit = false thì chỉ kiểm tra.</summary>
    private async Task<ContractImportResult> ImportRowsAsync(List<ExcelRowReader> rows, bool commit, CancellationToken cancellationToken)
    {
        var errors = new List<ExcelImportError>();
        // Gộp theo số HĐ giấy (không phân biệt hoa thường), giữ thứ tự xuất hiện; dòng không có số thì báo lỗi riêng.
        var groups = new List<(string PaperNo, List<ExcelRowReader> Rows)>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Text(PaperNo) is not { } paperNo)
            {
                errors.Add(new ExcelImportError(row.RowNumber, null, $"Thiếu \"{PaperNo}\"."));
                continue;
            }

            if (!index.TryGetValue(paperNo, out var at))
            {
                index[paperNo] = at = groups.Count;
                groups.Add((paperNo, []));
            }

            groups[at].Rows.Add(row);
        }

        var (contracts, lines) = (0, 0);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (paperNo, groupRows) in groups)
        {
            // Dòng có mã hàng theo đúng thứ tự gửi sang dịch vụ – để đổi lỗi "Dòng k" của dịch vụ về số dòng Excel.
            var lineRows = groupRows.Where(r => r.Text(ItemCode) is not null).ToList();
            await transaction.CreateSavepointAsync("contract", cancellationToken);
            try
            {
                var request = await ReadContractAsync(groupRows, lineRows, cancellationToken);
                await _contracts.CreateAsync(request, cancellationToken);
                await transaction.ReleaseSavepointAsync("contract", cancellationToken);
                _db.ChangeTracker.Clear();
                contracts++;
                lines += request.Lines.Count;
            }
            catch (ForbiddenAccessException ex)
            {
                // Mặt hàng ngoài phạm vi, không có quyền… thì báo một lần rồi dừng như nhập danh mục.
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                return new ContractImportResult(rows.Count, 0, 0, [new ExcelImportError(0, null, ex.Message)], false);
            }
            catch (Exception ex) when (Describe(ex, groupRows[0].RowNumber, lineRows) is { } error)
            {
                await transaction.RollbackToSavepointAsync("contract", cancellationToken);
                // Hợp đồng lỗi có thể còn trong ChangeTracker; xóa để không lưu kèm ở hợp đồng sau.
                _db.ChangeTracker.Clear();
                errors.Add(new ExcelImportError(error.Row, paperNo, error.Message));
            }
        }

        var committed = commit && errors.Count == 0;
        if (committed) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        errors.Sort((a, b) => a.Row.CompareTo(b.Row));
        return new ContractImportResult(rows.Count, contracts, lines, errors, committed);
    }

    /// <summary>
    /// Đọc một hợp đồng: đầu ở dòng đầu (dòng sau trống hoặc giống hệt), tra mã NCC, số bảng giá, mã bộ phận, mã hàng, mã ĐVT; chặn nhập lại
    /// hợp đồng đã có (cùng số HĐ giấy, cùng NCC, chưa hủy). Kiểm tra độ dài bằng validator của API.
    /// </summary>
    /// <exception cref="RowException">Khi một dòng sai – kèm số dòng Excel.</exception>
    private async Task<SaveContractRequest> ReadContractAsync(
        List<ExcelRowReader> groupRows, List<ExcelRowReader> lineRows, CancellationToken cancellationToken)
    {
        var first = groupRows[0];
        var paperNo = first.Text(PaperNo)!;
        foreach (var row in groupRows.Skip(1)) At(row, () => CheckSameHeader(first, row));

        var (supplierCode, priceListNo, title, signDate, from, to, credit) = At(first, () => (
            first.RequiredText(SupplierCode), first.RequiredText(PriceListNo), first.RequiredText(Title), first.Date(SignDate),
            first.Date(ValidFrom) ?? throw new ExcelRowException($"Thiếu \"{ValidFrom}\"."), first.Date(ValidTo) ?? throw new ExcelRowException($"Thiếu \"{ValidTo}\"."),
            first.Decimal(CreditLimit)));
        var supplierId = await _db.Suppliers.Where(s => s.Code == supplierCode).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(first.RowNumber, $"Không có NCC mã {supplierCode}.");
        var priceListId = await PriceListIdAsync(priceListNo, cancellationToken)
            ?? throw new RowException(first.RowNumber, $"Không có bảng giá số {priceListNo} của công ty đang làm việc hoặc toàn tập đoàn.");
        var departmentIds = await DepartmentIdsAsync(first, cancellationToken);
        // Nhập lại cùng file sẽ tạo hợp đồng trùng – chặn; muốn đổi hợp đồng đã có thì sửa trên màn hình hoặc lập phụ lục.
        if (await _db.PurchaseContracts.Where(c => c.SupplierId == supplierId && c.ContractNo == paperNo && c.Status != ContractStatus.Cancelled)
                .Select(c => c.No).FirstOrDefaultAsync(cancellationToken) is { } existing)
            throw new RowException(first.RowNumber, $"Đã có hợp đồng {existing} số HĐ giấy {paperNo} với NCC {supplierCode} – không nhập lại.");

        foreach (var row in groupRows.Skip(1).Where(r => !lineRows.Contains(r)))
            throw new RowException(row.RowNumber, $"Thiếu \"{ItemCode}\" – các dòng sau dòng đầu của hợp đồng phải là dòng sản lượng.");
        var lines = new List<SaveContractLineRequest>();
        foreach (var row in lineRows) lines.Add(await ReadLineAsync(row, cancellationToken));

        var request = new SaveContractRequest(
            supplierId, priceListId, paperNo, title, signDate, from, to, first.Text(PriceAdjustment), first.Text(Payment), credit, first.Text(Delivery),
            first.Text(Discount), first.Text(Sponsorship), first.Text(Penalty), first.Text(Other), first.Text(Note), departmentIds, lines);
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new RowException(first.RowNumber, validation.Errors[0].ErrorMessage);
        return request;
    }

    /// <summary>Một dòng sản lượng: mã hàng (trong phạm vi nhóm hàng), mã ĐVT, SL dự kiến bắt buộc; giá do dịch vụ lấy từ bảng giá.</summary>
    private async Task<SaveContractLineRequest> ReadLineAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var (itemCode, uomCode, qty, min) = At(row, () => (
            row.RequiredText(ItemCode), row.RequiredText(UomCode), row.Decimal(ExpectedQty) ?? throw new ExcelRowException($"Thiếu \"{ExpectedQty}\"."),
            row.Decimal(MinCommitQty)));
        // Filter nhóm hàng tự áp: mặt hàng ngoài phạm vi coi như không có.
        var itemId = await _db.Items.Where(i => i.Code == itemCode).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(row.RowNumber, $"Không có mặt hàng mã {itemCode} (hoặc ngoài phạm vi nhóm hàng của bạn).");
        var uomId = await _db.Uoms.Where(u => u.Code == uomCode).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new RowException(row.RowNumber, $"Không có ĐVT mã {uomCode}.");
        return new SaveContractLineRequest(null, itemId, uomId, qty, min, row.Text(LineNote));
    }

    /// <summary>
    /// Bảng giá theo số: ưu tiên bảng của công ty đang làm việc, rồi bảng toàn tập đoàn (thuộc tổng công ty – bỏ filter công ty có điều
    /// kiện như <see cref="ContractService"/>). Bảng có dùng được cho hợp đồng hay không do dịch vụ hợp đồng kiểm tra.
    /// </summary>
    private Task<Guid?> PriceListIdAsync(string no, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        return _db.SupplierPriceLists.IgnoreQueryFilters([ProcDbContext.CompanyFilter])
            .Where(p => p.No == no && (p.CompanyId == companyId || p.IsGroupWide))
            .OrderBy(p => p.CompanyId == companyId ? 0 : 1)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Mã bộ phận cách nhau dấu phẩy/chấm phẩy → Id bộ phận của công ty đang làm việc (filter công ty tự áp).</summary>
    private async Task<List<Guid>> DepartmentIdsAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var codes = (row.Text(Departments) ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (codes.Count == 0) return [];
        var found = await _db.Departments.Where(d => codes.Contains(d.Code)).Select(d => new { d.Id, d.Code }).ToListAsync(cancellationToken);
        var missing = codes.Where(c => found.All(d => d.Code != c)).ToList();
        if (missing.Count > 0) throw new RowException(row.RowNumber, $"Không có bộ phận mã {string.Join(", ", missing)} ở công ty đang làm việc.");
        return found.Select(d => d.Id).ToList();
    }

    /// <summary>Dòng sau của hợp đồng: ô đầu hợp đồng nào có ghi thì phải giống dòng đầu (so theo giá trị đã đọc – ngày, số, chữ).</summary>
    /// <exception cref="ExcelRowException">Khi khác dòng đầu.</exception>
    private static void CheckSameHeader(ExcelRowReader first, ExcelRowReader row)
    {
        foreach (var (header, kind) in HeaderColumns)
        {
            var value = Read(row, header, kind);
            if (value is not null && !Equals(value, Read(first, header, kind)))
                throw new ExcelRowException($"\"{header}\" khác dòng {first.RowNumber} của cùng số HĐ giấy – để trống hoặc ghi giống dòng đầu.");
        }
    }

    /// <summary>Đọc giá trị một ô theo kiểu (ngày, số, chữ).</summary>
    private static object? Read(ExcelRowReader row, string header, Kind kind) => kind switch
    {
        Kind.Date => row.Date(header),
        Kind.Number => row.Decimal(header),
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
    /// Lỗi nghiệp vụ/dữ liệu thành (dòng Excel, thông điệp); lỗi khác (lỗi hệ thống) để nổi lên thành 500. Lỗi "Dòng k: …" của dịch vụ hợp
    /// đồng (k = thứ tự dòng sản lượng) đổi sang dòng Excel thứ k trong các dòng có mã hàng.
    /// </summary>
    private static (int Row, string Message)? Describe(Exception ex, int firstRow, List<ExcelRowReader> lineRows)
    {
        if (ex is RowException row) return (row.Row, row.Message);
        var message = ex is BusinessRuleException or NotFoundException ? ex.Message : DuplicateKeyMessages.Describe(ex);
        if (message is null) return null;
        if (LineErrorRegex().Match(message) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var k) && k >= 1 && k <= lineRows.Count)
            return (lineRows[k - 1].RowNumber, m.Groups[2].Value);
        return (firstRow, message);
    }

    private static ContractImportResult Failed(string message) => new(0, 0, 0, [new ExcelImportError(0, null, message)], false);

    /// <summary>Mẫu nhận biết lỗi đã có tiền tố "Dòng N" từ Domain để đổi sang số dòng Excel.</summary>
    [GeneratedRegex(@"^Dòng (\d+): (.+)$", RegexOptions.Singleline)]
    private static partial Regex LineErrorRegex();

    /// <summary>Lỗi của một dòng Excel cụ thể.</summary>
    private sealed class RowException(int row, string message) : Exception(message)
    {
        public int Row { get; } = row;
    }
}
