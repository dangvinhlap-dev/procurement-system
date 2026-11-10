using Microsoft.EntityFrameworkCore;
using Proc.Application.Analysis;
using Proc.Application.Common;
using Proc.Domain.Analysis;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Analysis;

/// <summary>
/// Chuẩn hóa dữ liệu mua lịch sử (xem <see cref="IPurchaseHistoryMappingService"/>, design.md mục 18.2).
/// Gom nhóm, chấm điểm trong bộ nhớ: dữ liệu một pháp nhân tối đa vài trăm nghìn dòng, chỉ đọc vài cột nên nhẹ; điểm giống tên (Dice
/// trên tập từ không dấu) không viết được bằng SQL đơn giản. Gán lại dòng bằng một câu UPDATE theo mã cũ/MST.
/// </summary>
public sealed class PurchaseHistoryMappingService : IPurchaseHistoryMappingService
{
    /// <summary>Số gợi ý khi chưa gõ từ khóa.</summary>
    public const int SuggestionCount = 5;

    /// <summary>Số kết quả tối đa khi tìm theo từ khóa.</summary>
    public const int SearchResultCount = 20;

    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Khởi tạo dịch vụ.
    /// </summary>
    /// <param name="db">CSDL.</param>
    /// <param name="scope">Phạm vi người dùng (quyền, công ty đang làm việc).</param>
    public PurchaseHistoryMappingService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseHistorySourceDto>> ListSourcesAsync(string kind, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryView);
        _scope.RequireCompany();
        var isItem = IsItem(kind);
        // Global query filter theo công ty: chỉ dữ liệu của pháp nhân đang làm việc.
        var rows = await _db.PurchaseHistoryRecords.AsNoTracking()
            .Select(r => new SourceRow(r.ItemCode, r.ItemName, r.Specification, r.Uom, r.SupplierName, r.SupplierTaxCode, r.Amount,
                r.PurchaseDate, isItem ? r.ItemId : r.SupplierId))
            .ToListAsync(cancellationToken);
        var groups = rows.GroupBy(r => isItem ? r.ItemCode : PurchaseHistoryKeys.Supplier(r.TaxCode, r.SupplierName)).ToList();

        var keys = groups.Select(g => g.Key).ToList();
        var mapped = isItem
            ? await _db.PurchaseHistoryItemMappings.AsNoTracking().Where(m => keys.Contains(m.SourceCode)).Select(m => m.SourceCode).ToListAsync(cancellationToken)
            : await _db.PurchaseHistorySupplierMappings.AsNoTracking().Where(m => keys.Contains(m.SourceKey)).Select(m => m.SourceKey).ToListAsync(cancellationToken);
        var mappedKeys = mapped.ToHashSet(StringComparer.Ordinal);
        var targetIds = rows.Where(r => r.TargetId != null).Select(r => r.TargetId!.Value).Distinct().ToList();
        // Tra mã, tên đích riêng (không join): Items có filter nhóm hàng, join sẽ làm mất nhóm dòng gán vào hàng của nhóm không được xem.
        var targets = isItem
            ? await _db.Items.AsNoTracking().Where(i => targetIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => (i.Code, i.Name), cancellationToken)
            : await _db.Suppliers.AsNoTracking().Where(s => targetIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => (s.Code, s.Name), cancellationToken);

        return groups
            .Select(g =>
            {
                var first = g.First();
                // Tên, quy cách hay gặp nhất: cùng mã cũ đôi khi ghi tên hơi khác nhau giữa các lần mua.
                var name = MostCommon(g.Select(r => isItem ? r.ItemName : r.SupplierName))!;
                var detail = isItem
                    ? string.Join(" · ", new[] { MostCommon(g.Select(r => r.Specification)), MostCommon(g.Select(r => r.Uom)) }.Where(x => x != null))
                    : null;
                var distinctTargets = g.Select(r => r.TargetId).Distinct().ToList();
                Guid? targetId = distinctTargets is [{ } only] ? only : null;
                var target = targetId is { } t && targets.TryGetValue(t, out var info) ? info : ((string Code, string Name)?)null;
                return new PurchaseHistorySourceDto(
                    g.Key, isItem ? g.Key : first.TaxCode, name, detail, g.Count(), g.Count(r => r.TargetId != null), g.Sum(r => r.Amount),
                    g.Max(r => r.PurchaseDate), targetId, target?.Code, target?.Name, mappedKeys.Contains(g.Key));
            })
            .OrderByDescending(s => s.Amount).ThenBy(s => s.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseHistoryCandidateDto>> ListCandidatesAsync(
        string kind, string key, string? search, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryView);
        _scope.RequireCompany();
        var isItem = IsItem(kind);
        var source = await SourceRowsAsync(isItem, key, cancellationToken);
        if (source.Count == 0) throw new NotFoundException("Không có dòng mua lịch sử nào mang mã/NCC này.");
        var words = SearchText.Normalize(search).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        List<(Guid Id, string Code, string Name, string? Detail)> catalog;
        if (isItem)
        {
            // Filter nhóm hàng: chỉ gợi ý mặt hàng thuộc nhóm người dùng được xem (design.md mục 9.2).
            var items = _db.Items.AsNoTracking().Where(i => i.IsActive);
            foreach (var word in words)
            {
                var pattern = LikePatterns.Contains(word);
                items = items.Where(i => EF.Functions.Like(i.SearchText, pattern, LikePatterns.Escape));
            }

            catalog = (await items.Select(i => new { i.Id, i.Code, i.Name, i.Specification }).ToListAsync(cancellationToken))
                .Select(i => (i.Id, i.Code, i.Name, i.Specification)).ToList();
        }
        else
        {
            // Mọi trạng thái NCC (kể cả tạm ngừng, danh sách đen): dữ liệu lịch sử phải gán đúng NCC đã bán, dù nay còn dùng hay không.
            var suppliers = _db.Suppliers.AsNoTracking();
            foreach (var word in words)
            {
                var pattern = LikePatterns.Contains(word);
                suppliers = suppliers.Where(s => EF.Functions.Like(s.SearchText, pattern, LikePatterns.Escape));
            }

            catalog = (await suppliers.Select(s => new { s.Id, s.Code, s.Name, s.TaxCode }).ToListAsync(cancellationToken))
                .Select(s => (s.Id, s.Code, s.Name, s.TaxCode)).ToList();
        }

        // Mặt hàng: so tên + quy cách (cùng tên khác quy cách là hàng khác); NCC: so tên, bỏ từ loại hình doanh nghiệp.
        var ignored = isItem ? null : NameSimilarity.CompanyWords;
        // So với cách ghi hay gặp nhất của nguồn (cùng mã cũ đôi khi ghi tên hơi khác nhau).
        var sourceName = isItem
            ? MostCommon(source.Select(r => $"{r.ItemName} {r.Specification}"))
            : MostCommon(source.Select(r => r.SupplierName));
        var sourceWords = NameSimilarity.Words(sourceName, ignored);
        var scored = catalog
            .Select(c => new PurchaseHistoryCandidateDto(c.Id, c.Code, c.Name, c.Detail,
                NameSimilarity.Score(sourceWords, NameSimilarity.Words(isItem ? $"{c.Name} {c.Detail}" : c.Name, ignored))))
            .Where(c => words.Length > 0 || c.Score > 0)
            .OrderByDescending(c => c.Score).ThenBy(c => c.Code, StringComparer.Ordinal);
        return scored.Take(words.Length > 0 ? SearchResultCount : SuggestionCount).ToList();
    }

    /// <inheritdoc />
    public async Task<PurchaseHistoryMappingResult> SaveAsync(SavePurchaseHistoryMappingRequest request, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryImport);
        var companyId = _scope.RequireCompany();
        var isItem = IsItem(request.Kind);
        var key = isItem ? PurchaseHistoryKeys.Item(request.Key) : request.Key;
        // Đích kiểm tra lại ở server qua filter: mặt hàng phải thuộc nhóm hàng người dùng được xem (không tin id từ client).
        var targetExists = isItem
            ? await _db.Items.AnyAsync(i => i.Id == request.TargetId, cancellationToken)
            : await _db.Suppliers.AnyAsync(s => s.Id == request.TargetId, cancellationToken);
        if (!targetExists) throw new NotFoundException(isItem ? "Không tìm thấy mặt hàng chung." : "Không tìm thấy NCC trong danh mục.");
        var recordIds = await RecordIdsAsync(isItem, key, cancellationToken);
        if (recordIds.Count == 0) throw new NotFoundException("Không có dòng mua lịch sử nào mang mã/NCC này.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (isItem)
        {
            var mapping = await _db.PurchaseHistoryItemMappings.FirstOrDefaultAsync(m => m.SourceCode == key, cancellationToken);
            if (mapping is null) _db.PurchaseHistoryItemMappings.Add(PurchaseHistoryItemMapping.Create(companyId, key, request.TargetId));
            else mapping.Retarget(request.TargetId);
        }
        else
        {
            var mapping = await _db.PurchaseHistorySupplierMappings.FirstOrDefaultAsync(m => m.SourceKey == key, cancellationToken);
            if (mapping is null) _db.PurchaseHistorySupplierMappings.Add(PurchaseHistorySupplierMapping.Create(companyId, key, request.TargetId));
            else mapping.Retarget(request.TargetId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var updated = await AssignAsync(isItem, recordIds, request.TargetId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PurchaseHistoryMappingResult(updated);
    }

    /// <inheritdoc />
    public async Task<PurchaseHistoryMappingResult> DeleteAsync(string kind, string key, CancellationToken cancellationToken)
    {
        _scope.EnsurePermission(Permissions.Analysis.PurchaseHistoryImport);
        _scope.RequireCompany();
        var isItem = IsItem(kind);
        if (isItem) key = PurchaseHistoryKeys.Item(key);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        Guid? fallback;
        if (isItem)
        {
            var mapping = await _db.PurchaseHistoryItemMappings.FirstOrDefaultAsync(m => m.SourceCode == key, cancellationToken)
                          ?? throw new NotFoundException("Mã vật tư cũ này chưa có ánh xạ.");
            _db.PurchaseHistoryItemMappings.Remove(mapping);
            // Quay về tự khớp như lúc nhập (design.md mục 18.1): mã cũ trùng mã hàng chung.
            fallback = await _db.Items.Where(i => i.Code == key).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            var mapping = await _db.PurchaseHistorySupplierMappings.FirstOrDefaultAsync(m => m.SourceKey == key, cancellationToken)
                          ?? throw new NotFoundException("NCC cũ này chưa có ánh xạ.");
            _db.PurchaseHistorySupplierMappings.Remove(mapping);
            var taxCode = PurchaseHistoryKeys.TaxCodeOf(key);
            fallback = taxCode is null
                ? null
                : await _db.Suppliers.Where(s => s.TaxCode == taxCode).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var updated = await AssignAsync(isItem, await RecordIdsAsync(isItem, key, cancellationToken), fallback, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PurchaseHistoryMappingResult(updated);
    }

    /// <summary>Loại ánh xạ là mặt hàng (true) hay NCC (false); loại khác thì báo lỗi.</summary>
    private static bool IsItem(string? kind) => kind switch
    {
        PurchaseHistoryMappingKinds.Item => true,
        PurchaseHistoryMappingKinds.Supplier => false,
        _ => throw new BusinessRuleException("Loại ánh xạ phải là mặt hàng hoặc NCC."),
    };

    /// <summary>
    /// Các dòng của công ty mang khóa nguồn. NCC theo tên: lọc thô bằng chuỗi tìm kiếm (đã chứa tên không dấu) rồi so khóa đúng trong
    /// bộ nhớ – khóa tên tính bằng hàm chuẩn hóa của .NET, CSDL không có cột riêng.
    /// </summary>
    private async Task<List<SourceRecord>> SourceRowsAsync(bool isItem, string key, CancellationToken cancellationToken)
    {
        var records = _db.PurchaseHistoryRecords.AsNoTracking();
        if (isItem)
        {
            var code = PurchaseHistoryKeys.Item(key);
            records = records.Where(r => r.ItemCode == code);
        }
        else if (PurchaseHistoryKeys.TaxCodeOf(key) is { } taxCode)
        {
            records = records.Where(r => r.SupplierTaxCode == taxCode);
        }
        else if (PurchaseHistoryKeys.IsValidSupplierKey(key))
        {
            var pattern = LikePatterns.Contains(key[PurchaseHistoryKeys.NamePrefix.Length..]);
            records = records.Where(r => r.SupplierTaxCode == null && EF.Functions.Like(r.SearchText, pattern, LikePatterns.Escape));
        }
        else
        {
            return [];
        }

        var rows = await records
            .Select(r => new SourceRecord(r.Id, r.ItemName, r.Specification, r.SupplierName, r.SupplierTaxCode))
            .ToListAsync(cancellationToken);
        return isItem ? rows : rows.Where(r => PurchaseHistoryKeys.Supplier(r.TaxCode, r.SupplierName) == key).ToList();
    }

    /// <summary>Lấy Id các dòng dữ liệu mua lịch sử có cùng khóa nguồn (mã vật tư cũ hoặc NCC cũ).</summary>
    private async Task<List<Guid>> RecordIdsAsync(bool isItem, string key, CancellationToken cancellationToken) =>
        (await SourceRowsAsync(isItem, key, cancellationToken)).Select(r => r.Id).ToList();

    /// <summary>
    /// Gán mặt hàng/NCC cho các dòng. Dòng mua lịch sử không ghi nhật ký từng dòng (design.md mục 18.1) – nhật ký nằm ở ánh xạ.
    /// Chia lô 5.000 id để câu lệnh không quá dài.
    /// </summary>
    private async Task<int> AssignAsync(bool isItem, List<Guid> recordIds, Guid? targetId, CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var chunk in recordIds.Chunk(5000))
        {
            var records = _db.PurchaseHistoryRecords.Where(r => chunk.Contains(r.Id));
            updated += isItem
                ? await records.ExecuteUpdateAsync(s => s.SetProperty(r => r.ItemId, targetId), cancellationToken)
                : await records.ExecuteUpdateAsync(s => s.SetProperty(r => r.SupplierId, targetId), cancellationToken);
        }

        return updated;
    }

    /// <summary>Giá trị xuất hiện nhiều nhất (bỏ null); hòa thì lấy theo thứ tự chữ để kết quả ổn định.</summary>
    private static string? MostCommon(IEnumerable<string?> values) =>
        values.Where(v => v != null).GroupBy(v => v!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).FirstOrDefault();

    /// <summary>Một dòng dữ liệu mua lịch sử đọc ra để gom nhóm theo khóa nguồn.</summary>
    private sealed record SourceRow(
        string ItemCode, string ItemName, string? Specification, string Uom, string SupplierName, string? TaxCode, decimal Amount,
        DateOnly PurchaseDate, Guid? TargetId);

    /// <summary>Một dòng dữ liệu mua lịch sử kèm Id, dùng để gợi ý mặt hàng/NCC trùng gần đúng.</summary>
    private sealed record SourceRecord(Guid Id, string ItemName, string? Specification, string SupplierName, string? TaxCode);
}
