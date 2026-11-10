using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Application.Pricing;
using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Pricing;
using Proc.Infrastructure.MasterData;
using Proc.Infrastructure.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Pricing;

/// <summary>
/// Phiếu khảo sát giá thị trường – ngân hàng giá (design.md mục 18.25, lát G4a). Phiếu thuộc tổng công ty (Văn phòng Ban): lập, sửa, chốt, hủy
/// khi đang làm việc ở tổng công ty; filter công ty và nhóm hàng giữ nguyên ở mọi truy vấn.
/// </summary>
public sealed class MarketPriceSurveyService : IMarketPriceSurveyService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;
    private readonly IDocumentNumberService _numbers;
    private readonly IClock _clock;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi người dùng.</param>
    /// <param name="numbers">Đánh số chứng từ.</param>
    /// <param name="clock">Đồng hồ.</param>
    public MarketPriceSurveyService(ProcDbContext db, ICurrentUserScope scope, IDocumentNumberService numbers, IClock clock)
    {
        _db = db;
        _scope = scope;
        _numbers = numbers;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarketSurveyListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var rows = await (
                from x in _db.MarketPriceSurveys.AsNoTracking()
                join c in _db.ItemCategories on x.ItemCategoryId equals c.Id
                orderby x.SurveyDate descending, x.No descending
                select new { x.Id, x.No, x.SurveyDate, CategoryCode = c.Code, CategoryName = c.Name, Lines = x.Lines.Count, x.Status, x.CreatedBy })
            .Take(2000)
            .ToListAsync(cancellationToken);
        var users = await UserNamesAsync(rows.Select(r => r.CreatedBy), cancellationToken);
        return rows.Select(r => new MarketSurveyListItemDto(
                r.Id, r.No, r.SurveyDate, r.CategoryCode, r.CategoryName, r.Lines, r.Status.ToString(), r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<MarketSurveyDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var survey = await _db.MarketPriceSurveys.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("Không tìm thấy phiếu khảo sát giá.");
        return await ToDtoAsync(survey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MarketSurveyDto> CreateAsync(SaveMarketSurveyRequest request, CancellationToken cancellationToken)
    {
        var companyId = await RequireHeadOfficeAsync(cancellationToken);
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckAsync(header, lines, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var no = await _numbers.NextAsync(DocumentTypes.MarketPriceSurvey, header.SurveyDate, cancellationToken);
        var survey = MarketPriceSurvey.Create(companyId, no, header, _clock.TodayInVietnam());
        survey.SetLines(lines);
        _db.MarketPriceSurveys.Add(survey);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(survey.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MarketSurveyDto> UpdateAsync(Guid id, SaveMarketSurveyRequest request, CancellationToken cancellationToken)
    {
        var survey = await FindOwnAsync(id, cancellationToken);
        var header = ToHeader(request);
        var lines = await ResolveLinesAsync(request.Lines, cancellationToken);
        await CheckAsync(header, lines, cancellationToken);
        survey.Update(header, _clock.TodayInVietnam());
        survey.SetLines(lines);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MarketSurveyDto> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var survey = await FindOwnAsync(id, cancellationToken);
        survey.Confirm(_clock.UtcNow, _scope.UserId ?? throw new ForbiddenAccessException("Chưa đăng nhập."));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MarketSurveyDto> CancelAsync(Guid id, CancelMarketSurveyRequest request, CancellationToken cancellationToken)
    {
        var survey = await FindOwnAsync(id, cancellationToken);
        survey.Cancel(_clock.TodayInVietnam(), request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarketPriceDto>> PricesAsync(IReadOnlyList<Guid> itemIds, int days, CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        if (days is < 1 or > 730) throw new BusinessRuleException("Số ngày nhìn lại phải từ 1 đến 730.");
        if (itemIds.Count == 0) return [];
        var ids = itemIds.Distinct().ToList();
        var since = _clock.TodayInVietnam().AddDays(-days);
        var rows = await (
                from x in _db.MarketPriceSurveys.AsNoTracking()
                where x.Status == MarketSurveyStatus.Confirmed && x.SurveyDate >= since
                from l in x.Lines
                where ids.Contains(l.ItemId)
                join s in _db.Suppliers on l.SupplierId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                orderby x.SurveyDate descending, x.No descending, l.LineNo
                select new { l.ItemId, SurveyId = x.Id, x.No, x.SurveyDate, SupplierCode = s == null ? null : s.Code, SupplierName = s == null ? null : s.Name,
                    l.SourceName, l.UnitPrice, l.UomFactor })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new MarketPriceDto(
                r.ItemId, r.SurveyId, r.No, r.SurveyDate,
                r.SupplierCode is null ? r.SourceName ?? "" : $"{r.SupplierCode} – {r.SupplierName}{(r.SourceName is null ? "" : $" ({r.SourceName})")}",
                Math.Round(r.UnitPrice / r.UomFactor, 6, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    /// <summary>
    /// Quy tắc của phiếu: nhóm hàng trong phạm vi; mặt hàng thuộc nhóm hàng (hoặc nhóm con); NCC (nếu chọn) tồn tại – mọi trạng thái, vì khảo
    /// sát thị trường gồm cả NCC tiềm năng chưa duyệt.
    /// </summary>
    private async Task CheckAsync(MarketSurveyHeader header, List<(MarketSurveyLineData Data, Item Item)> lines, CancellationToken cancellationToken)
    {
        var parents = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);
        if (!parents.ContainsKey(header.ItemCategoryId))
            throw new BusinessRuleException("Nhóm hàng không tồn tại hoặc ngoài phạm vi nhóm hàng của bạn.");
        var outside = lines.FirstOrDefault(l => !ItemAdministration.IsUnder(l.Item.CategoryId, header.ItemCategoryId, parents));
        if (outside.Item is not null) throw new BusinessRuleException($"Mặt hàng {outside.Item.Code} không thuộc nhóm hàng của phiếu.");
        var supplierIds = lines.Select(l => l.Data.SupplierId).OfType<Guid>().Distinct().ToList();
        if (supplierIds.Count > 0 && await _db.Suppliers.CountAsync(s => supplierIds.Contains(s.Id), cancellationToken) < supplierIds.Count)
            throw new BusinessRuleException("Có nhà cung cấp không tồn tại.");
    }

    /// <summary>Bắt buộc đang làm việc ở tổng công ty (ngân hàng giá do Văn phòng Ban xây dựng).</summary>
    private async Task<Guid> RequireHeadOfficeAsync(CancellationToken cancellationToken)
    {
        // Ngân hàng giá do Văn phòng Ban (tổng công ty) xây dựng – quyết định thành lập Ban cung ứng.
        if (!await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken))
            throw new ForbiddenAccessException("Phiếu khảo sát giá thị trường chỉ lập khi đang làm việc ở tổng công ty.");
        return _scope.RequireCompany();
    }

    /// <summary>Tìm phiếu khảo sát giá của tổng công ty kèm dòng; không thấy thì báo lỗi 404.</summary>
    private async Task<MarketPriceSurvey> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        await RequireHeadOfficeAsync(cancellationToken);
        var survey = await _db.MarketPriceSurveys.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("Không tìm thấy phiếu khảo sát giá.");
        var ids = survey.Lines.Select(l => l.ItemId).Distinct().ToList();
        if (await _db.Items.CountAsync(i => ids.Contains(i.Id), cancellationToken) < ids.Count)
            throw new ForbiddenAccessException("Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên bạn không thao tác được.");
        return survey;
    }

    private async Task<List<(MarketSurveyLineData Data, Item Item)>> ResolveLinesAsync(
        IReadOnlyList<SaveMarketSurveyLineRequest> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Include(i => i.Conversions).Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return lines.Select((l, index) => (
                new MarketSurveyLineData(l.Id, l.ItemId, l.UomId, l.SupplierId, l.SourceName, l.UnitPrice, l.Note),
                items.GetValueOrDefault(l.ItemId)
                ?? throw new NotFoundException($"Dòng {index + 1}: không tìm thấy mặt hàng (hoặc mặt hàng ngoài phạm vi nhóm hàng của bạn).")))
            .ToList();
    }

    /// <summary>Lấy họ tên theo danh sách Id người dùng (bỏ null, bỏ trùng).</summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var list = ids.OfType<Guid>().Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }

    /// <summary>Đổi phiếu khảo sát giá sang DTO kèm mã/tên mặt hàng, ĐVT và tên người lập.</summary>
    private async Task<MarketSurveyDto> ToDtoAsync(MarketPriceSurvey x, CancellationToken cancellationToken)
    {
        var itemIds = x.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id, cancellationToken);
        var uomIds = x.Lines.Select(l => l.UomId).Distinct().ToList();
        var uoms = await _db.Uoms.AsNoTracking().Where(u => uomIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var supplierIds = x.Lines.Select(l => l.SupplierId).OfType<Guid>().Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Code, s.Name })
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var category = await _db.ItemCategories.AsNoTracking().Where(c => c.Id == x.ItemCategoryId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(cancellationToken);
        var users = await UserNamesAsync([x.CreatedBy, x.ConfirmedBy], cancellationToken);

        var headOffice = await HeadOffice.IsWorkingAtAsync(_db, _scope, cancellationToken);
        string? readOnly = !headOffice ? "Phiếu khảo sát giá chỉ sửa khi đang làm việc ở tổng công ty."
            : items.Count < itemIds.Count ? "Phiếu có mặt hàng ngoài phạm vi nhóm hàng của bạn nên chỉ xem được."
            : !_scope.HasPermission(Permissions.Pricing.PriceBankManage) ? "Bạn không có quyền lập, sửa phiếu khảo sát giá." : null;
        var actions = new MarketSurveyActions(
            readOnly is null && x.Status == MarketSurveyStatus.Draft,
            readOnly is null && x.Status != MarketSurveyStatus.Cancelled,
            readOnly);
        var lines = x.Lines.OrderBy(l => l.LineNo).Select(l =>
        {
            var item = items.GetValueOrDefault(l.ItemId);
            var supplier = l.SupplierId is { } s ? suppliers.GetValueOrDefault(s) : null;
            return new MarketSurveyLineDto(
                l.Id, l.LineNo, l.ItemId, item?.Code ?? "(ngoài phạm vi)", item?.Name ?? "", l.UomId, uoms.GetValueOrDefault(l.UomId) ?? "",
                l.SupplierId, supplier?.Code, supplier?.Name, l.SourceName, l.UnitPrice, l.BaseUnitPrice, l.Note);
        }).ToList();
        return new MarketSurveyDto(
            x.Id, x.No, x.SurveyDate, x.ItemCategoryId, category?.Code ?? "", category?.Name ?? "", x.Note, x.Status.ToString(), x.ConfirmedAt,
            x.ConfirmedBy is { } cb ? users.GetValueOrDefault(cb) : null, x.CancelledOn, x.CancelReason, x.CreatedAt,
            x.CreatedBy is { } u ? users.GetValueOrDefault(u) : null, lines, actions);
    }

    private static MarketSurveyHeader ToHeader(SaveMarketSurveyRequest r) => new(r.SurveyDate, r.ItemCategoryId, r.Note);
}
