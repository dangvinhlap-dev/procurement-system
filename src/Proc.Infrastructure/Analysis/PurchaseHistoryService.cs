using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Proc.Application.Analysis;
using Proc.Application.Common;
using Proc.Application.Excel;
using Proc.Domain.Analysis;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Excel;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Analysis;

/// <summary>
/// Dữ liệu mua lịch sử (xem <see cref="IPurchaseHistoryService"/>, design.md mục 18.1).
/// Khác nhập danh mục (mục 5.10, từng dòng qua dịch vụ, tối đa 5.000 dòng): dòng mua lịch sử chỉ cần kiểm tra của domain, không đụng
/// bản ghi nào khác, nên kiểm tra hết trong bộ nhớ rồi ghi một lần bằng COPY nhị phân – 50.000 dòng chỉ vài giây.
/// </summary>
public sealed class PurchaseHistoryService : IPurchaseHistoryService
{
    /// <summary>Số dòng tối đa mỗi file (≈ 24 tháng mua của một pháp nhân lớn).</summary>
    public const int MaxRows = 50_000;

    /// <summary>Số lỗi tối đa trả về – file sai cả cột thì báo 500 lỗi đầu là đủ để sửa, không gửi 50.000 lỗi về trình duyệt.</summary>
    public const int MaxReportedErrors = 500;

    /// <summary>Số dòng tối đa mỗi lần xem danh sách.</summary>
    public const int ListLimit = 5000;

    private const string SheetName = "Dữ liệu mua";

    private static class H
    {
        public const string Department = "Bộ phận sử dụng";
        public const string ItemCode = "Mã vật tư";
        public const string ItemName = "Tên vật tư";
        public const string Specification = "Quy cách";
        public const string Brand = "Thương hiệu";
        public const string Origin = "Xuất xứ";
        public const string Standard = "Tiêu chuẩn";
        public const string Uom = "ĐVT";
        public const string RequestDate = "Ngày yêu cầu";
        public const string PurchaseDate = "Ngày mua";
        public const string Quantity = "Số lượng";
        public const string UnitPrice = "Đơn giá trước VAT";
        public const string Vat = "% VAT";
        public const string Currency = "Tiền tệ";
        public const string RelatedCost = "Chi phí liên quan";
        public const string RelatedCostNote = "Ghi chú chi phí";
        public const string SupplierName = "Tên NCC";
        public const string SupplierTaxCode = "MST NCC";
        public const string SupplierContact = "Liên hệ NCC";
        public const string PaymentTerms = "Điều kiện thanh toán";
        public const string Discount = "Chiết khấu";
        public const string Sponsorship = "Tài trợ";
        public const string OnTime = "Đúng hạn";
        public const string QualityPassed = "Đạt chất lượng";
        public const string Complaint = "Đổi trả / khiếu nại";
    }

    /// <summary>Các cột của file – 12 nhóm trường của đề xuất Ban Cung ứng.</summary>
    private static readonly ExcelColumn[] Columns =
    [
        new(H.Department, "Phòng ban, phân xưởng dùng vật tư."),
        new(H.ItemCode, "Mã vật tư theo cách đánh mã của pháp nhân. Trùng mã hàng hóa trong danh mục chung thì tự gán mặt hàng.", Required: true, Text: true),
        new(H.ItemName, "Tên vật tư như trên chứng từ cũ.", Required: true),
        new(H.Specification, "Quy cách, kích thước."),
        new(H.Brand, "Thương hiệu."),
        new(H.Origin, "Xuất xứ."),
        new(H.Standard, "Tiêu chuẩn kỹ thuật (TCVN, ISO…)."),
        new(H.Uom, "Đơn vị tính ghi bằng chữ (Cái, Kg…).", Required: true),
        new(H.RequestDate, "Ngày yêu cầu mua, dd/MM/yyyy; không được sau ngày mua."),
        new(H.PurchaseDate, "Ngày mua (ngày hóa đơn hoặc ngày nhận hàng), dd/MM/yyyy; không được sau hôm nay.", Required: true),
        new(H.Quantity, "Số lượng > 0; nhận cả 1.234,5 và 1234.5.", Required: true),
        new(H.UnitPrice, "Đơn giá chưa VAT ≥ 0 (hàng tặng ghi 0).", Required: true),
        new(H.Vat, "Thuế suất VAT, số từ 0 đến 100 (ví dụ 8, 10)."),
        new(H.Currency, "Mã tiền tệ 3 chữ cái; để trống là VND."),
        new(H.RelatedCost, "Chi phí vận chuyển, bốc xếp… của dòng, ≥ 0."),
        new(H.RelatedCostNote, "Ghi chú chi phí liên quan."),
        new(H.SupplierName, "Tên nhà cung cấp.", Required: true),
        new(H.SupplierTaxCode, "Mã số thuế NCC. Trùng MST của NCC trong danh mục thì tự gán NCC.", Text: true),
        new(H.SupplierContact, "Người liên hệ, điện thoại."),
        new(H.PaymentTerms, "Điều kiện thanh toán (trả trước, 30 ngày…)."),
        new(H.Discount, "Chiết khấu được hưởng (2%, 500.000 đ…)."),
        new(H.Sponsorship, "Tài trợ, khuyến mại kèm theo."),
        new(H.OnTime, "Giao đúng hạn: Có / Không; trống = chưa đánh giá."),
        new(H.QualityPassed, "Đạt chất lượng: Có / Không; trống = chưa đánh giá."),
        new(H.Complaint, "Đổi trả, khiếu nại (nếu có)."),
    ];

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IClock _clock;

    /// <summary>
    /// Tạo dịch vụ.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người thao tác.</param>
    /// <param name="clock">Đồng hồ – ngày mua không được sau hôm nay.</param>
    public PurchaseHistoryService(ProcDbContext db, ICurrentUserScope scope, IClock clock)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
    }

    /// <inheritdoc />
    public ExcelFile GetTemplate()
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryImport);
        List<string> guide =
        [
            "Dữ liệu mua lịch sử 12–24 tháng của pháp nhân đang làm việc – chỉ để phân tích, không ghi sổ kho, không ảnh hưởng tồn kho, giá vốn.",
            "Dòng 1 là tiêu đề cột (cột có dấu * là bắt buộc), dữ liệu từ dòng 2. Đổi thứ tự cột không sao; cột không dùng có thể xóa.",
            "Bấm \"Kiểm tra thử\" để xem lỗi trước; \"Nhập thật\" chỉ lưu khi mọi dòng đều đúng. Mỗi lần nhập là một lô – nhập nhầm thì xóa cả lô.",
            $"Tối đa {Number(MaxRows)} dòng, 20 MB mỗi file. Cột mã vật tư, MST để dạng Text để không mất số 0 đầu.",
        ];
        return new ExcelFile("mau-du-lieu-mua-lich-su.xlsx", ExcelWriter.Write(SheetName, Columns, [], guide));
    }

    /// <inheritdoc />
    public async Task<PurchaseHistoryImportResult> ImportAsync(string fileName, Stream content, bool commit, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryImport);
        // Mỗi pháp nhân nhập ở công ty làm việc của mình (design.md mục 18.1) – không nhận công ty từ file hay từ client.
        var companyId = _scope.RequireCompany();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed("Không đọc được file. Hãy dùng file Excel .xlsx (tải file mẫu ở nút Nhập Excel).");
        }

        List<PurchaseHistoryRecord> records = [];
        List<ExcelImportError> errors = [];
        var errorCount = 0;
        int totalRows;
        var batchId = Guid.CreateVersion7();
        var today = _clock.TodayInVietnam();
        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => ExcelHeaders.Normalize(s.Name) == ExcelHeaders.Normalize(SheetName))
                        ?? workbook.Worksheets.First();
            var columns = ExcelTransfer.ReadHeader(sheet);
            var missing = Columns.Where(c => c.Required && !columns.ContainsKey(ExcelHeaders.Normalize(c.Header))).Select(c => c.Header).ToList();
            if (missing.Count > 0)
                return Failed($"File thiếu cột bắt buộc: {string.Join(", ", missing)}. Hãy dùng file mẫu dữ liệu mua lịch sử.");

            var rows = sheet.RowsUsed().Where(r => r.RowNumber() > 1).Select(r => new ExcelRowReader(r, columns)).Where(r => !r.IsEmpty).ToList();
            totalRows = rows.Count;
            if (totalRows == 0) return Failed("File không có dòng dữ liệu nào.");
            if (totalRows > MaxRows)
                return Failed($"File có {Number(totalRows)} dòng, tối đa {Number(MaxRows)} dòng mỗi lần nhập. Hãy chia nhỏ file (ví dụ theo năm).");

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    records.Add(PurchaseHistoryRecord.Create(companyId, batchId, row.RowNumber, Read(row), today));
                }
                catch (Exception ex) when (ex is ExcelRowException or BusinessRuleException)
                {
                    errorCount++;
                    if (errors.Count < MaxReportedErrors) errors.Add(new ExcelImportError(row.RowNumber, SafeText(row, H.ItemCode), ex.Message));
                }
            }
        }

        if (errorCount > errors.Count)
            errors.Add(new ExcelImportError(0, null, $"Còn {errorCount - errors.Count} dòng lỗi khác chưa liệt kê – sửa các lỗi trên rồi kiểm tra lại."));

        var (matchedItems, matchedSuppliers) = await MatchAsync(records, cancellationToken);
        if (!commit || errors.Count > 0)
            return new PurchaseHistoryImportResult(totalRows, records.Count, matchedItems, matchedSuppliers, errors, false, null);

        var batch = PurchaseHistoryBatch.Create(batchId, companyId, fileName, records);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.PurchaseHistoryBatches.Add(batch);
        // Lưu lô qua EF để có nhật ký (AuditLog) và người nhập; các dòng ghi bằng COPY trong cùng giao dịch.
        await _db.SaveChangesAsync(cancellationToken);
        await CopyRecordsAsync(records, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return new PurchaseHistoryImportResult(totalRows, records.Count, matchedItems, matchedSuppliers, [], true, batchId);
    }

    /// <inheritdoc />
    public async Task<PurchaseHistoryListResult> ListAsync(PurchaseHistoryQuery query, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryView);
        _scope.RequireCompany();
        // Global query filter theo công ty đang làm việc: chỉ thấy dữ liệu của pháp nhân mình.
        var records = _db.PurchaseHistoryRecords.AsNoTracking();
        if (query.BatchId is { } batchId) records = records.Where(r => r.BatchId == batchId);
        if (query.From is { } from) records = records.Where(r => r.PurchaseDate >= from);
        if (query.To is { } to) records = records.Where(r => r.PurchaseDate <= to);
        records = query.Match switch
        {
            PurchaseHistoryMatchFilters.Matched => records.Where(r => r.ItemId != null && r.SupplierId != null),
            PurchaseHistoryMatchFilters.Unmatched => records.Where(r => r.ItemId == null || r.SupplierId == null),
            _ => records,
        };
        foreach (var word in SearchText.Normalize(query.Search).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = LikePatterns.Contains(word);
            records = records.Where(r => EF.Functions.Like(r.SearchText, pattern, LikePatterns.Escape));
        }

        // Mã hàng, mã NCC đã khớp: tra riêng sau (không join) – Items có filter nhóm hàng, join sẽ làm mất dòng của nhóm không được xem.
        var rows = await records
            .OrderByDescending(r => r.PurchaseDate).ThenBy(r => r.BatchId).ThenBy(r => r.RowNumber)
            .Take(ListLimit + 1)
            .ToListAsync(cancellationToken);
        var truncated = rows.Count > ListLimit;
        if (truncated) rows = rows[..ListLimit];

        var itemIds = rows.Where(r => r.ItemId != null).Select(r => r.ItemId!.Value).Distinct().ToList();
        var supplierIds = rows.Where(r => r.SupplierId != null).Select(r => r.SupplierId!.Value).Distinct().ToList();
        var itemCodes = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, cancellationToken);
        var supplierCodes = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);

        return new PurchaseHistoryListResult(
            rows.Select(r => new PurchaseHistoryRecordDto(
                r.Id, r.BatchId, r.RowNumber, r.Department, r.ItemCode, r.ItemName, r.Specification, r.Brand, r.Origin, r.Standard, r.Uom,
                r.RequestDate, r.PurchaseDate, r.Quantity, r.UnitPrice, r.VatPercent, r.Currency, r.Amount, r.RelatedCost, r.RelatedCostNote,
                r.SupplierName, r.SupplierTaxCode, r.SupplierContact, r.PaymentTerms, r.Discount, r.Sponsorship, r.OnTime, r.QualityPassed,
                r.Complaint, r.ItemId, r.ItemId is { } i ? itemCodes.GetValueOrDefault(i) : null,
                r.SupplierId, r.SupplierId is { } s ? supplierCodes.GetValueOrDefault(s) : null)).ToList(),
            truncated,
            ListLimit);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseHistoryBatchDto>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryView);
        _scope.RequireCompany();
        return await (
                from b in _db.PurchaseHistoryBatches.AsNoTracking()
                join u in _db.Users on b.CreatedBy equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby b.CreatedAt descending
                select new PurchaseHistoryBatchDto(b.Id, b.FileName, b.RecordCount, b.FromDate, b.ToDate, b.CreatedAt, u == null ? null : u.FullName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryImport);
        _scope.RequireCompany();
        // Filter công ty: lô của công ty khác coi như không có (không lộ là có tồn tại).
        var batch = await _db.PurchaseHistoryBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy lô dữ liệu mua lịch sử.");
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.PurchaseHistoryRecords.Where(r => r.BatchId == batchId).ExecuteDeleteAsync(cancellationToken);
        _db.PurchaseHistoryBatches.Remove(batch);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static PurchaseHistoryData Read(ExcelRowReader row) => new(
        row.Text(H.Department), row.Text(H.ItemCode), row.Text(H.ItemName), row.Text(H.Specification), row.Text(H.Brand),
        row.Text(H.Origin), row.Text(H.Standard), row.Text(H.Uom), row.Date(H.RequestDate), row.Date(H.PurchaseDate),
        row.Decimal(H.Quantity), row.Decimal(H.UnitPrice), row.Decimal(H.Vat), row.Text(H.Currency), row.Decimal(H.RelatedCost),
        row.Text(H.RelatedCostNote), row.Text(H.SupplierName), row.Text(H.SupplierTaxCode), row.Text(H.SupplierContact),
        row.Text(H.PaymentTerms), row.Text(H.Discount), row.Text(H.Sponsorship), row.Bool(H.OnTime), row.Bool(H.QualityPassed),
        row.Text(H.Complaint));

    /// <summary>
    /// Tự khớp danh mục: mã vật tư trùng mã hàng hóa chung → mặt hàng; MST trùng NCC → NCC. Không khớp vẫn nhập (lát E2 ánh xạ tiếp).
    /// Tra theo danh sách mã khác nhau (vài nghìn) thay vì từng dòng.
    /// </summary>
    /// <returns>Số dòng khớp mặt hàng, số dòng khớp NCC.</returns>
    private async Task<(int Items, int Suppliers)> MatchAsync(List<PurchaseHistoryRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0) return (0, 0);
        var codes = records.Select(r => r.ItemCode).Distinct().ToList();
        var taxCodes = records.Where(r => r.SupplierTaxCode != null).Select(r => r.SupplierTaxCode!).Distinct().ToList();
        // Items có filter nhóm hàng: người nhập chỉ khớp được mặt hàng thuộc nhóm mình được xem – đúng phạm vi (design.md mục 9.2).
        var items = await _db.Items.AsNoTracking().Where(i => codes.Contains(i.Code)).ToDictionaryAsync(i => i.Code, i => i.Id, cancellationToken);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.TaxCode != null && taxCodes.Contains(s.TaxCode))
            .ToDictionaryAsync(s => s.TaxCode!, s => s.Id, cancellationToken);
        int matchedItems = 0, matchedSuppliers = 0;
        foreach (var record in records)
        {
            Guid? itemId = items.TryGetValue(record.ItemCode, out var i) ? i : null;
            Guid? supplierId = record.SupplierTaxCode is { } tax && suppliers.TryGetValue(tax, out var s) ? s : null;
            record.Match(itemId, supplierId);
            if (itemId != null) matchedItems++;
            if (supplierId != null) matchedSuppliers++;
        }

        return (matchedItems, matchedSuppliers);
    }

    /// <summary>
    /// Ghi các dòng bằng COPY nhị phân trong giao dịch đang mở. Tên bảng, cột và thứ tự cột lấy từ mô hình EF nên thêm trường mới chỉ cần
    /// sửa entity và cấu hình.
    /// </summary>
    private async Task CopyRecordsAsync(IReadOnlyList<PurchaseHistoryRecord> records, CancellationToken cancellationToken)
    {
        var entity = _db.Model.FindEntityType(typeof(PurchaseHistoryRecord))!;
        var properties = entity.GetProperties().ToList();
        var columns = string.Join(", ", properties.Select(p => $"\"{p.GetColumnName()}\""));
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        // Tên bảng, cột từ mô hình EF (không phải dữ liệu người dùng) nên ghép chuỗi an toàn.
        await using var import = await connection.BeginBinaryImportAsync(
            $"COPY \"{entity.GetTableName()}\" ({columns}) FROM STDIN (FORMAT BINARY)", cancellationToken);
        foreach (var record in records)
        {
            await import.StartRowAsync(cancellationToken);
            foreach (var property in properties)
            {
                switch (property.PropertyInfo!.GetValue(record))
                {
                    case null:
                        await import.WriteNullAsync(cancellationToken);
                        break;
                    case Guid g:
                        await import.WriteAsync(g, NpgsqlDbType.Uuid, cancellationToken);
                        break;
                    case string text:
                        await import.WriteAsync(text, NpgsqlDbType.Varchar, cancellationToken);
                        break;
                    case decimal d:
                        await import.WriteAsync(d, NpgsqlDbType.Numeric, cancellationToken);
                        break;
                    case int n:
                        await import.WriteAsync(n, NpgsqlDbType.Integer, cancellationToken);
                        break;
                    case DateOnly date:
                        await import.WriteAsync(date, NpgsqlDbType.Date, cancellationToken);
                        break;
                    case bool b:
                        await import.WriteAsync(b, NpgsqlDbType.Boolean, cancellationToken);
                        break;
                    case var other:
                        throw new InvalidOperationException($"COPY chưa hỗ trợ kiểu {other.GetType().Name} của cột {property.Name}.");
                }
            }
        }

        await import.CompleteAsync(cancellationToken);
    }

    private static string? SafeText(ExcelRowReader row, string header)
    {
        try
        {
            return row.Text(header);
        }
        catch (ExcelRowException)
        {
            return null;
        }
    }

    /// <summary>Số nguyên kiểu Việt Nam: 50000 → "50.000".</summary>
    private static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    private static PurchaseHistoryImportResult Failed(string message) => new(0, 0, 0, 0, [new ExcelImportError(0, null, message)], false, null);
}
