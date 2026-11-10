using Proc.Domain.Common;
using Proc.Domain.Documents;
using Proc.Domain.MasterData;

namespace Proc.Domain.Pricing;

/// <summary>Trạng thái phiếu khảo sát giá thị trường (design.md mục 18.25 – lát G4a).</summary>
public enum MarketSurveyStatus
{
    /// <summary>Nháp – đang nhập, sửa được.</summary>
    Draft = 1,

    /// <summary>Đã chốt – giá đã vào ngân hàng giá, không sửa.</summary>
    Confirmed = 2,

    /// <summary>Đã hủy (kèm lý do) – giá không còn dùng để tham khảo.</summary>
    Cancelled = 3,
}

/// <summary>Đầu phiếu khảo sát giá.</summary>
/// <param name="SurveyDate">Ngày khảo sát (không sau hôm nay).</param>
/// <param name="ItemCategoryId">Nhóm hàng.</param>
/// <param name="Note">Ghi chú (phương pháp khảo sát, phạm vi…).</param>
public sealed record MarketSurveyHeader(DateOnly SurveyDate, Guid ItemCategoryId, string? Note);

/// <summary>Dữ liệu một dòng khảo sát giá.</summary>
/// <param name="Id">Id dòng đang có (null = dòng mới).</param>
/// <param name="ItemId">Mặt hàng.</param>
/// <param name="UomId">ĐVT của giá (cơ sở hoặc quy đổi).</param>
/// <param name="SupplierId">NCC trong danh mục (không bắt buộc).</param>
/// <param name="SourceName">Tên nguồn giá ngoài danh mục (cửa hàng, báo giá, trang web…) – bắt buộc khi không chọn NCC.</param>
/// <param name="UnitPrice">Đơn giá VND, trước VAT.</param>
/// <param name="Note">Ghi chú dòng.</param>
public sealed record MarketSurveyLineData(Guid? Id, Guid ItemId, Guid UomId, Guid? SupplierId, string? SourceName, decimal UnitPrice, string? Note);

/// <summary>
/// Phiếu khảo sát giá thị trường – số loại <see cref="DocumentTypes.MarketPriceSurvey"/> (design.md mục 18.25, lát G4a). Quyết định thành lập
/// Ban cung ứng: Văn phòng Ban khảo sát giá định kỳ và chuyển dữ liệu vào ngân hàng giá, làm căn cứ cho Ban duyệt giá tháng. Phiếu lập ở tổng
/// công ty theo nhóm hàng; mỗi dòng một giá của một nguồn (NCC trong danh mục hoặc nguồn ngoài) cho một mặt hàng – một mặt hàng có thể nhiều
/// dòng (nhiều nguồn). Nháp → Đã chốt (giá vào ngân hàng giá, không sửa); hủy kèm lý do. Không qua duyệt: Văn phòng Ban chỉ thu thập dữ liệu,
/// quyết định giá vẫn ở phiếu duyệt tháng (GTH).
/// </summary>
[Auditable("Phiếu khảo sát giá thị trường", LabelProperty = nameof(No))]
public sealed class MarketPriceSurvey : AuditableEntity, ICompanyOwned, IHasDocumentNumber
{
    /// <summary>Độ dài ghi chú, lý do tối đa.</summary>
    public const int TextMaxLength = 1000;

    private readonly List<MarketPriceSurveyLine> _lines = [];

    private MarketPriceSurvey()
    {
    }

    /// <summary>Công ty lập (tổng công ty).</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ KSG-TCT-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? DocumentNumber => No;

    /// <summary>Ngày khảo sát.</summary>
    public DateOnly SurveyDate { get; private set; }

    /// <summary>Nhóm hàng (mặt hàng trên dòng thuộc nhóm này hoặc nhóm con).</summary>
    public Guid ItemCategoryId { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public MarketSurveyStatus Status { get; private set; } = MarketSurveyStatus.Draft;

    /// <summary>Thời điểm chốt (UTC).</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Người chốt.</summary>
    public Guid? ConfirmedBy { get; private set; }

    /// <summary>Ngày hủy.</summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Các dòng giá.</summary>
    public IReadOnlyList<MarketPriceSurveyLine> Lines => _lines;

    /// <summary>Lập phiếu nháp.</summary>
    /// <param name="companyId">Công ty lập (tổng công ty – tầng trên kiểm tra).</param>
    /// <param name="no">Số phiếu.</param>
    /// <param name="header">Đầu phiếu.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <returns>Phiếu nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi thiếu công ty, số hoặc đầu phiếu sai.</exception>
    public static MarketPriceSurvey Create(Guid companyId, string no, MarketSurveyHeader header, DateOnly today)
    {
        if (companyId == Guid.Empty) throw new BusinessRuleException("Thiếu công ty lập phiếu.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Phiếu phải có số.");
        var survey = new MarketPriceSurvey { CompanyId = companyId, No = no };
        survey.Update(header, today);
        return survey;
    }

    /// <summary>Sửa đầu phiếu khi còn nháp.</summary>
    /// <param name="header">Đầu phiếu mới.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc dữ liệu sai.</exception>
    public void Update(MarketSurveyHeader header, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(header);
        EnsureDraft();
        // Khảo sát là ghi nhận giá đã thấy trên thị trường – không có ngày khảo sát ở tương lai.
        if (header.SurveyDate > today) throw new BusinessRuleException("Ngày khảo sát không được sau hôm nay.");
        if (header.ItemCategoryId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhóm hàng.");
        var note = Clean(header.Note);
        if (note is { Length: > TextMaxLength }) throw new BusinessRuleException($"Ghi chú tối đa {TextMaxLength} ký tự.");
        SurveyDate = header.SurveyDate;
        ItemCategoryId = header.ItemCategoryId;
        Note = note;
    }

    /// <summary>
    /// Thay toàn bộ dòng. Một mặt hàng có thể nhiều dòng (nhiều nguồn giá) nhưng không trùng mặt hàng × nguồn (cùng NCC hoặc cùng tên nguồn).
    /// </summary>
    /// <param name="lines">Dòng kèm mặt hàng đã nạp (có ĐVT quy đổi).</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp, không có dòng, trùng nguồn hoặc dòng sai.</exception>
    public void SetLines(IReadOnlyList<(MarketSurveyLineData Data, Item Item)> lines)
    {
        EnsureDraft();
        if (lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng giá.");
        var keep = new List<MarketPriceSurveyLine>();
        var lineNo = 0;
        foreach (var (data, item) in lines)
        {
            lineNo++;
            MarketPriceSurveyLine line;
            if (data.Id is { } id)
            {
                line = _lines.FirstOrDefault(l => l.Id == id) ?? throw new BusinessRuleException($"Dòng {lineNo}: không tìm thấy dòng cần sửa – hãy tải lại phiếu.");
            }
            else
            {
                line = new MarketPriceSurveyLine(Id);
                _lines.Add(line);
            }

            line.Set(lineNo, data, item);
            keep.Add(line);
        }

        var duplicate = keep.GroupBy(l => (l.ItemId, l.SupplierId, Source: l.SupplierId is null ? l.SourceName!.ToUpperInvariant() : null))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new BusinessRuleException($"Dòng {string.Join(", ", duplicate.Select(l => l.LineNo))}: trùng mặt hàng và nguồn giá – mỗi nguồn một dòng.");
        _lines.RemoveAll(l => !keep.Contains(l));
    }

    /// <summary>Chốt phiếu: giá vào ngân hàng giá, từ đây không sửa (sai thì hủy và lập phiếu mới).</summary>
    /// <param name="at">Thời điểm chốt (UTC).</param>
    /// <param name="userId">Người chốt.</param>
    /// <exception cref="BusinessRuleException">Khi không còn nháp hoặc chưa có dòng.</exception>
    public void Confirm(DateTimeOffset at, Guid userId)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new BusinessRuleException("Phiếu phải có ít nhất một dòng giá mới chốt được.");
        Status = MarketSurveyStatus.Confirmed;
        ConfirmedAt = at;
        ConfirmedBy = userId;
    }

    /// <summary>Hủy phiếu nháp hoặc đã chốt (ví dụ nhập sai giá) – bắt buộc lý do; giá của phiếu thôi dùng để tham khảo.</summary>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <param name="reason">Lý do.</param>
    /// <exception cref="BusinessRuleException">Khi phiếu đã hủy hoặc thiếu lý do.</exception>
    public void Cancel(DateOnly today, string? reason)
    {
        if (Status == MarketSurveyStatus.Cancelled) throw new BusinessRuleException($"Phiếu {No} đã hủy.");
        var clean = Clean(reason) ?? throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (clean.Length > TextMaxLength) throw new BusinessRuleException($"Lý do tối đa {TextMaxLength} ký tự.");
        Status = MarketSurveyStatus.Cancelled;
        CancelledOn = today;
        CancelReason = clean;
    }

    /// <summary>Chặn sửa khi phiếu khảo sát không còn ở trạng thái Nháp.</summary>
    private void EnsureDraft()
    {
        if (Status != MarketSurveyStatus.Draft) throw new BusinessRuleException($"Phiếu {No} không ở trạng thái nháp.");
    }

    /// <summary>Bỏ khoảng trắng hai đầu; chuỗi rỗng hoặc toàn khoảng trắng thành null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Dòng phiếu khảo sát giá: một giá của một nguồn cho một mặt hàng.</summary>
[Auditable("Dòng phiếu khảo sát giá thị trường")]
public sealed class MarketPriceSurveyLine : Entity
{
    /// <summary>Độ dài tên nguồn tối đa.</summary>
    public const int SourceMaxLength = 200;

    /// <summary>Giới hạn đơn giá (numeric(19,6) – CLAUDE.md mục 4).</summary>
    private const decimal MaxPrice = 10_000_000_000_000m;

    private MarketPriceSurveyLine()
    {
    }

    internal MarketPriceSurveyLine(Guid surveyId) => SurveyId = surveyId;

    /// <summary>Phiếu.</summary>
    public Guid SurveyId { get; private set; }

    /// <summary>Số thứ tự.</summary>
    public int LineNo { get; private set; }

    /// <summary>Mặt hàng.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>ĐVT của giá.</summary>
    public Guid UomId { get; private set; }

    /// <summary>Hệ số ĐVT cơ sở – numeric(18,6); dùng quy giá về ĐVT cơ sở khi so sánh.</summary>
    public decimal UomFactor { get; private set; }

    /// <summary>NCC trong danh mục (null = nguồn ngoài danh mục).</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Tên nguồn giá (bắt buộc khi không có NCC).</summary>
    public string? SourceName { get; private set; }

    /// <summary>Đơn giá VND, trước VAT – numeric(19,6).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Giá quy về ĐVT cơ sở = đơn giá ÷ hệ số ĐVT, làm tròn 6 số (0,5 lên). Ví dụ 120.000/thùng 12 cái → 10.000/cái.
    /// </summary>
    public decimal BaseUnitPrice => Math.Round(UnitPrice / UomFactor, 6, MidpointRounding.AwayFromZero);

    /// <summary>Kiểm tra và gán dữ liệu một dòng khảo sát giá (mặt hàng, nguồn, giá).</summary>
    internal void Set(int lineNo, MarketSurveyLineData data, Item item)
    {
        var prefix = $"Dòng {lineNo} ({item.Code})";
        if (data.ItemId != item.Id) throw new BusinessRuleException($"{prefix}: mặt hàng không khớp.");
        if (!item.IsActive && item.Id != ItemId) throw new BusinessRuleException($"{prefix}: mặt hàng đã ngừng dùng.");
        var source = string.IsNullOrWhiteSpace(data.SourceName) ? null : data.SourceName.Trim();
        // Giá khảo sát phải truy được nguồn – không có NCC thì phải ghi tên nguồn (cửa hàng, báo giá, trang web…).
        if (data.SupplierId is null && source is null) throw new BusinessRuleException($"{prefix}: hãy chọn NCC hoặc nhập tên nguồn giá.");
        if (source is { Length: > SourceMaxLength }) throw new BusinessRuleException($"{prefix}: tên nguồn tối đa {SourceMaxLength} ký tự.");
        if (data.UnitPrice <= 0 || decimal.Round(data.UnitPrice, 6) != data.UnitPrice || data.UnitPrice >= MaxPrice)
            throw new BusinessRuleException($"{prefix}: đơn giá phải > 0, tối đa 6 chữ số thập phân.");
        var note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        if (note is { Length: > 500 }) throw new BusinessRuleException($"{prefix}: ghi chú tối đa 500 ký tự.");
        LineNo = lineNo;
        ItemId = item.Id;
        UomId = data.UomId;
        UomFactor = data.UomId == item.BaseUomId ? 1m
            : item.Conversions.FirstOrDefault(c => c.UomId == data.UomId)?.Factor
              ?? throw new BusinessRuleException($"{prefix}: ĐVT phải là ĐVT cơ sở hoặc ĐVT quy đổi của mặt hàng.");
        SupplierId = data.SupplierId;
        SourceName = source;
        UnitPrice = data.UnitPrice;
        Note = note;
    }
}
