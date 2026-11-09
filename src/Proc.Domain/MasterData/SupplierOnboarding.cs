using System.Text.Json;
using Proc.Domain.Common;

namespace Proc.Domain.MasterData;

/// <summary>Trạng thái hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25).</summary>
public enum SupplierOnboardingStatus
{
    /// <summary>Phòng cung ứng chung đang lập, chấm điểm – còn sửa được.</summary>
    Draft = 1,

    /// <summary>Phòng cung ứng chung đã hoàn thành đánh giá – chờ Văn phòng Ban đề xuất.</summary>
    Evaluated = 2,

    /// <summary>Văn phòng Ban đã đề xuất đưa vào danh sách – đang chờ Hội đồng duyệt (yêu cầu duyệt NCC đi kèm).</summary>
    Submitted = 3,

    /// <summary>Hội đồng đã duyệt – NCC chuyển sang Đã duyệt.</summary>
    Approved = 4,

    /// <summary>Hội đồng (hoặc công ty liên quan) từ chối – kết thúc, muốn xét lại thì lập hồ sơ mới.</summary>
    Rejected = 5,

    /// <summary>Văn phòng Ban không đề xuất – kết thúc.</summary>
    NotRecommended = 6,

    /// <summary>Hủy (lập nhầm, NCC rút lui…).</summary>
    Cancelled = 7,
}

/// <summary>Kết luận đánh giá của Phòng cung ứng chung.</summary>
public enum SupplierOnboardingResult
{
    /// <summary>Đạt – đủ điều kiện đề xuất đưa vào danh sách NCC.</summary>
    Pass = 1,

    /// <summary>Không đạt.</summary>
    Fail = 2,
}

/// <summary>
/// Điểm 6 tiêu chí đánh giá NCC mới, thang 1–5 (null = chưa chấm).
/// </summary>
/// <param name="Price">Giá chào, tổng chi phí.</param>
/// <param name="SampleQuality">Chất lượng hàng mẫu.</param>
/// <param name="Capacity">Năng lực cung ứng (sản lượng, kho, nhân lực).</param>
/// <param name="Delivery">Khả năng giao hàng (thời gian, địa bàn).</param>
/// <param name="Payment">Điều kiện công nợ, thanh toán.</param>
/// <param name="Legal">Pháp lý, năng lực tài chính.</param>
public sealed record SupplierOnboardingScores(int? Price, int? SampleQuality, int? Capacity, int? Delivery, int? Payment, int? Legal)
{
    /// <summary>Các điểm theo thứ tự tiêu chí.</summary>
    public IReadOnlyList<int?> All => [Price, SampleQuality, Capacity, Delivery, Payment, Legal];
}

/// <summary>
/// Một giấy tờ trong hồ sơ pháp lý của NCC.
/// </summary>
/// <param name="Code">Mã giấy tờ trong <see cref="SupplierOnboardingDocuments.All"/>.</param>
/// <param name="Provided">NCC đã nộp.</param>
/// <param name="ValidTo">Ngày hết hiệu lực (nếu giấy tờ có thời hạn).</param>
/// <param name="Note">Ghi chú (số giấy, nơi cấp…).</param>
public sealed record SupplierOnboardingDocument(string Code, bool Provided, DateOnly? ValidTo, string? Note);

/// <summary>Danh mục giấy tờ cố định của hồ sơ NCC mới (design.md mục 18.25 – G8).</summary>
public static class SupplierOnboardingDocuments
{
    /// <summary>Giấy phép kinh doanh / đăng ký doanh nghiệp, mã số thuế – bắt buộc khi kết luận Đạt.</summary>
    public const string License = "LICENSE";

    /// <summary>Chứng nhận chất lượng (ISO, ATTP, HACCP…).</summary>
    public const string Quality = "QUALITY";

    /// <summary>Báo giá chính thức.</summary>
    public const string Quote = "QUOTE";

    /// <summary>Hàng mẫu, kết quả thử mẫu.</summary>
    public const string Sample = "SAMPLE";

    /// <summary>Báo cáo tài chính, năng lực tài chính.</summary>
    public const string Finance = "FINANCE";

    /// <summary>Mọi giấy tờ theo thứ tự hiển thị, kèm tên tiếng Việt.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        (License, "Giấy phép kinh doanh, mã số thuế"),
        (Quality, "Chứng nhận chất lượng (ISO, ATTP, HACCP…)"),
        (Quote, "Báo giá chính thức"),
        (Sample, "Hàng mẫu, kết quả thử mẫu"),
        (Finance, "Báo cáo / năng lực tài chính"),
    ];
}

/// <summary>Nội dung Phòng cung ứng chung nhập trên hồ sơ.</summary>
/// <param name="Date">Ngày đánh giá.</param>
/// <param name="CategoryId">Nhóm hàng đề xuất NCC cung cấp (thành ngành hàng của NCC khi được duyệt).</param>
/// <param name="Source">Nguồn tìm thấy NCC (giới thiệu, hội chợ, website…).</param>
/// <param name="Documents">Hồ sơ pháp lý – giấy tờ không gửi lên coi như chưa nộp.</param>
/// <param name="Scores">Điểm 6 tiêu chí.</param>
/// <param name="Result">Kết luận.</param>
/// <param name="Comment">Nhận xét đánh giá.</param>
/// <param name="Note">Ghi chú chung của phiếu.</param>
public sealed record SupplierOnboardingData(
    DateOnly Date, Guid CategoryId, string? Source, IReadOnlyList<SupplierOnboardingDocument> Documents, SupplierOnboardingScores Scores,
    SupplierOnboardingResult? Result, string? Comment, string? Note);

/// <summary>
/// Hồ sơ đánh giá NCC mới (lát G8, design.md mục 18.25; quyết định Q2): Phòng cung ứng chung tìm kiếm, lập hồ sơ pháp lý và chấm điểm NCC
/// Tiềm năng → Văn phòng Ban đề xuất (hoặc không đề xuất) → Hội đồng duyệt qua yêu cầu duyệt NCC (<see cref="SupplierChangeRequest"/>, hai
/// chặng như mục 18.6) → NCC chuyển Đã duyệt và có ngành hàng là nhóm đề xuất. Phiếu thuộc tổng công ty (danh mục NCC dùng chung).
/// </summary>
[Auditable("Hồ sơ đánh giá NCC mới", LabelProperty = nameof(No))]
public sealed class SupplierOnboarding : AuditableEntity, ICompanyOwned, IVersioned
{
    /// <summary>Độ dài nhận xét, ý kiến, lý do tối đa.</summary>
    public const int TextMaxLength = 2000;

    /// <summary>Độ dài nguồn tìm thấy NCC, ghi chú từng giấy tờ tối đa.</summary>
    public const int ShortTextMaxLength = 500;

    /// <summary>Điểm thấp nhất của mỗi tiêu chí.</summary>
    public const int MinScore = 1;

    /// <summary>Điểm cao nhất của mỗi tiêu chí.</summary>
    public const int MaxScore = 5;

    /// <summary>Điểm tiêu chí từ mức này trở xuống là "kém" – bắt buộc nhận xét.</summary>
    public const int LowScore = 2;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SupplierOnboarding()
    {
    }

    /// <summary>Phiên bản dòng (cột hệ thống xmin) – chống ghi đè khi hai người cùng sửa (<see cref="IVersioned"/>).</summary>
    public uint Version { get; private set; }

    /// <inheritdoc />
    public Guid CompanyId { get; private set; }

    /// <summary>Số phiếu, ví dụ HSN-TCT-2611-0001.</summary>
    public string No { get; private set; } = string.Empty;

    /// <summary>NCC được đánh giá (đang Tiềm năng khi lập).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Nhóm hàng đề xuất NCC cung cấp.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Ngày đánh giá.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Nguồn tìm thấy NCC.</summary>
    public string? Source { get; private set; }

    /// <summary>Hồ sơ pháp lý (JSON, đủ mọi giấy tờ trong danh mục theo đúng thứ tự).</summary>
    public string DocumentsJson { get; private set; } = "[]";

    /// <summary>Điểm giá.</summary>
    public int? PriceScore { get; private set; }

    /// <summary>Điểm chất lượng hàng mẫu.</summary>
    public int? SampleQualityScore { get; private set; }

    /// <summary>Điểm năng lực cung ứng.</summary>
    public int? CapacityScore { get; private set; }

    /// <summary>Điểm giao hàng.</summary>
    public int? DeliveryScore { get; private set; }

    /// <summary>Điểm công nợ, thanh toán.</summary>
    public int? PaymentScore { get; private set; }

    /// <summary>Điểm pháp lý, tài chính.</summary>
    public int? LegalScore { get; private set; }

    /// <summary>Điểm bình quân 6 tiêu chí (2 số lẻ), tính lúc hoàn thành đánh giá.</summary>
    public decimal? Score { get; private set; }

    /// <summary>Kết luận đánh giá.</summary>
    public SupplierOnboardingResult? Result { get; private set; }

    /// <summary>Nhận xét đánh giá.</summary>
    public string? Comment { get; private set; }

    /// <summary>Ghi chú chung (CLAUDE.md mục 4 – mọi form phiếu có ô Ghi chú).</summary>
    public string? Note { get; private set; }

    /// <summary>Trạng thái.</summary>
    public SupplierOnboardingStatus Status { get; private set; } = SupplierOnboardingStatus.Draft;

    /// <summary>Thời điểm hoàn thành đánh giá (UTC).</summary>
    public DateTimeOffset? EvaluatedAt { get; private set; }

    /// <summary>Người hoàn thành đánh giá.</summary>
    public Guid? EvaluatedBy { get; private set; }

    /// <summary>Văn phòng Ban đề xuất đưa vào danh sách (true) hay không (false); null = chưa có ý kiến.</summary>
    public bool? Recommended { get; private set; }

    /// <summary>Ý kiến đề xuất của Văn phòng Ban.</summary>
    public string? ProposalComment { get; private set; }

    /// <summary>Thời điểm đề xuất (UTC).</summary>
    public DateTimeOffset? ProposedAt { get; private set; }

    /// <summary>Người đề xuất (Văn phòng Ban).</summary>
    public Guid? ProposedBy { get; private set; }

    /// <summary>Yêu cầu duyệt NCC lập khi đề xuất (lượt gần nhất).</summary>
    public Guid? ChangeRequestId { get; private set; }

    /// <summary>Thời điểm Hội đồng duyệt xong (UTC).</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Lý do từ chối (của người duyệt).</summary>
    public string? RejectReason { get; private set; }

    /// <summary>Lý do hủy.</summary>
    public string? CancelReason { get; private set; }

    /// <summary>Điểm hiện tại theo thứ tự tiêu chí.</summary>
    public SupplierOnboardingScores Scores => new(PriceScore, SampleQualityScore, CapacityScore, DeliveryScore, PaymentScore, LegalScore);

    /// <summary>Hồ sơ pháp lý, đủ mọi giấy tờ trong danh mục theo thứ tự.</summary>
    public IReadOnlyList<SupplierOnboardingDocument> Documents =>
        JsonSerializer.Deserialize<List<SupplierOnboardingDocument>>(DocumentsJson, Json) ?? [];

    /// <summary>Hồ sơ còn mở (chặn lập thêm hồ sơ cho cùng NCC): Nháp, Đã đánh giá, Chờ duyệt.</summary>
    public bool IsOpen => Status is SupplierOnboardingStatus.Draft or SupplierOnboardingStatus.Evaluated or SupplierOnboardingStatus.Submitted;

    /// <summary>
    /// Lập hồ sơ (Nháp).
    /// </summary>
    /// <param name="companyId">Tổng công ty.</param>
    /// <param name="no">Số phiếu đã cấp.</param>
    /// <param name="supplier">NCC – phải đang Tiềm năng.</param>
    /// <param name="data">Nội dung ban đầu.</param>
    /// <param name="today">Hôm nay (giờ Việt Nam).</param>
    /// <returns>Hồ sơ nháp.</returns>
    /// <exception cref="BusinessRuleException">Khi NCC không ở Tiềm năng hoặc dữ liệu sai.</exception>
    public static SupplierOnboarding Create(Guid companyId, string no, Supplier supplier, SupplierOnboardingData data, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        if (companyId == Guid.Empty) throw new BusinessRuleException("Hồ sơ phải thuộc một công ty.");
        if (string.IsNullOrWhiteSpace(no)) throw new BusinessRuleException("Thiếu số hồ sơ.");
        EnsurePotential(supplier);
        var onboarding = new SupplierOnboarding { CompanyId = companyId, No = no.Trim(), SupplierId = supplier.Id };
        onboarding.Apply(data, today);
        return onboarding;
    }

    /// <summary>
    /// NCC phải đang Tiềm năng – hồ sơ chỉ dành cho NCC chưa vào danh sách (Đã duyệt thì không cần, Tạm ngưng/Danh sách đen thì không xét).
    /// </summary>
    /// <param name="supplier">NCC.</param>
    /// <exception cref="BusinessRuleException">Khi NCC không ở Tiềm năng.</exception>
    public static void EnsurePotential(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        if (supplier.Status != SupplierStatus.Potential)
            throw new BusinessRuleException($"NCC {supplier.Code} đang {SupplierChangeRequest.StatusName(supplier.Status)} – hồ sơ đánh giá NCC mới chỉ lập cho NCC Tiềm năng.");
    }

    /// <summary>
    /// Sửa nội dung khi còn Nháp.
    /// </summary>
    /// <param name="data">Nội dung mới.</param>
    /// <param name="today">Hôm nay.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp hoặc dữ liệu sai.</exception>
    public void Update(SupplierOnboardingData data, DateOnly today)
    {
        EnsureStatus(SupplierOnboardingStatus.Draft, "Nháp");
        Apply(data, today);
    }

    /// <summary>
    /// Hoàn thành đánh giá: đủ 6 điểm, có kết luận; kết luận Không đạt hoặc có tiêu chí ≤ 2 điểm thì bắt buộc nhận xét. Kết luận Đạt cần đã nộp
    /// giấy phép kinh doanh và mọi giấy tờ đã nộp còn hiệu lực ở ngày đánh giá. Điểm = bình quân 6 tiêu chí, 2 số lẻ – ví dụ 4, 5, 4, 3, 4, 3
    /// → 23 ÷ 6 = 3,83.
    /// </summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người đánh giá.</param>
    /// <exception cref="BusinessRuleException">Khi không còn Nháp hoặc thiếu điểm, kết luận, nhận xét, giấy tờ.</exception>
    public void Evaluate(DateTimeOffset at, Guid by)
    {
        EnsureStatus(SupplierOnboardingStatus.Draft, "Nháp");
        var scores = Scores.All;
        if (scores.Any(s => s is null)) throw new BusinessRuleException("Hãy chấm đủ 6 tiêu chí trước khi hoàn thành đánh giá.");
        if (Result is null) throw new BusinessRuleException("Hãy chọn kết luận đánh giá (Đạt / Không đạt).");
        if (Comment is null && (Result == SupplierOnboardingResult.Fail || scores.Any(s => s <= LowScore)))
            throw new BusinessRuleException($"Kết luận Không đạt hoặc có tiêu chí từ {LowScore} điểm trở xuống – hãy nhập nhận xét.");
        if (Result == SupplierOnboardingResult.Pass)
        {
            var documents = Documents;
            // Đạt nghĩa là đủ điều kiện giao dịch: ít nhất phải có tư cách pháp lý (giấy phép kinh doanh, mã số thuế).
            if (!documents.Any(d => d.Code == SupplierOnboardingDocuments.License && d.Provided))
                throw new BusinessRuleException("Kết luận Đạt cần NCC đã nộp giấy phép kinh doanh, mã số thuế.");
            var expired = documents.Where(d => d.Provided && d.ValidTo < Date).Select(d => NameOf(d.Code)).ToList();
            if (expired.Count > 0)
                throw new BusinessRuleException($"Giấy tờ đã hết hiệu lực trước ngày đánh giá: {string.Join(", ", expired)}.");
        }

        Score = Math.Round((decimal)scores.Sum(s => s!.Value) / scores.Count, 2, MidpointRounding.AwayFromZero);
        Status = SupplierOnboardingStatus.Evaluated;
        EvaluatedAt = at;
        EvaluatedBy = by;
    }

    /// <summary>
    /// Mở lại đánh giá để sửa (khi Văn phòng Ban chưa đề xuất).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi không ở Đã đánh giá.</exception>
    public void Reopen()
    {
        EnsureStatus(SupplierOnboardingStatus.Evaluated, "Đã đánh giá");
        Status = SupplierOnboardingStatus.Draft;
        (Score, EvaluatedAt, EvaluatedBy) = (null, null, null);
    }

    /// <summary>
    /// Văn phòng Ban đề xuất đưa NCC vào danh sách: hồ sơ chờ Hội đồng duyệt theo yêu cầu duyệt NCC vừa lập. Người đề xuất phải khác người
    /// đánh giá (tách Phòng cung ứng tìm, đánh giá – Văn phòng Ban đề xuất, sơ đồ Q2).
    /// </summary>
    /// <param name="comment">Ý kiến đề xuất (không bắt buộc).</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người đề xuất.</param>
    /// <param name="changeRequestId">Yêu cầu duyệt NCC đã lập.</param>
    /// <exception cref="BusinessRuleException">Khi chưa đánh giá, kết luận Không đạt hoặc người đề xuất là người đánh giá.</exception>
    public void Recommend(string? comment, DateTimeOffset at, Guid by, Guid changeRequestId)
    {
        EnsureCanRecommend(by);
        if (changeRequestId == Guid.Empty) throw new BusinessRuleException("Thiếu yêu cầu duyệt NCC đi kèm đề xuất.");
        SetProposal(true, comment, at, by);
        ChangeRequestId = changeRequestId;
        RejectReason = null;
        Status = SupplierOnboardingStatus.Submitted;
    }

    /// <summary>
    /// Kiểm tra đề xuất được không (gọi trước khi lập yêu cầu duyệt NCC để không lập yêu cầu thừa).
    /// </summary>
    /// <param name="by">Người đề xuất.</param>
    /// <exception cref="BusinessRuleException">Khi chưa đánh giá, kết luận Không đạt hoặc người đề xuất là người đánh giá.</exception>
    public void EnsureCanRecommend(Guid by)
    {
        EnsureCanPropose(by);
        if (Result != SupplierOnboardingResult.Pass)
            throw new BusinessRuleException("Kết luận đánh giá Không đạt – chỉ ghi được \"Không đề xuất\".");
    }

    /// <summary>
    /// Văn phòng Ban không đề xuất – hồ sơ kết thúc.
    /// </summary>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người ghi.</param>
    /// <exception cref="BusinessRuleException">Khi chưa đánh giá, thiếu lý do hoặc người ghi là người đánh giá.</exception>
    public void NotRecommend(string? reason, DateTimeOffset at, Guid by)
    {
        EnsureCanPropose(by);
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Không đề xuất phải ghi lý do.");
        SetProposal(false, reason, at, by);
        Status = SupplierOnboardingStatus.NotRecommended;
    }

    /// <summary>Hội đồng duyệt xong yêu cầu duyệt NCC đi kèm.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <exception cref="BusinessRuleException">Khi hồ sơ không chờ duyệt.</exception>
    public void MarkApproved(DateTimeOffset at)
    {
        EnsureStatus(SupplierOnboardingStatus.Submitted, "Chờ duyệt");
        Status = SupplierOnboardingStatus.Approved;
        ApprovedAt = at;
    }

    /// <summary>Yêu cầu duyệt NCC đi kèm bị từ chối – hồ sơ kết thúc.</summary>
    /// <param name="reason">Lý do từ chối.</param>
    /// <exception cref="BusinessRuleException">Khi hồ sơ không chờ duyệt.</exception>
    public void MarkRejected(string? reason)
    {
        EnsureStatus(SupplierOnboardingStatus.Submitted, "Chờ duyệt");
        Status = SupplierOnboardingStatus.Rejected;
        RejectReason = reason;
    }

    /// <summary>
    /// Người đề xuất rút yêu cầu duyệt: hồ sơ về Đã đánh giá, bỏ ý kiến đề xuất để đề xuất lại (hoặc Phòng cung ứng mở lại sửa).
    /// </summary>
    /// <exception cref="BusinessRuleException">Khi hồ sơ không chờ duyệt.</exception>
    public void Withdraw()
    {
        EnsureStatus(SupplierOnboardingStatus.Submitted, "Chờ duyệt");
        Status = SupplierOnboardingStatus.Evaluated;
        (Recommended, ProposalComment, ProposedAt, ProposedBy, ChangeRequestId) = (null, null, null, null, null);
    }

    /// <summary>
    /// Hủy hồ sơ còn Nháp hoặc Đã đánh giá.
    /// </summary>
    /// <param name="reason">Lý do, bắt buộc.</param>
    /// <exception cref="BusinessRuleException">Khi hồ sơ đã gửi/kết thúc hoặc thiếu lý do.</exception>
    public void Cancel(string? reason)
    {
        if (Status is not (SupplierOnboardingStatus.Draft or SupplierOnboardingStatus.Evaluated))
            throw new BusinessRuleException("Chỉ hủy được hồ sơ Nháp hoặc Đã đánh giá (hồ sơ chờ duyệt thì rút đề xuất trước).");
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Hãy nhập lý do hủy.");
        if (reason.Trim().Length > TextMaxLength) throw new BusinessRuleException($"Lý do hủy tối đa {TextMaxLength} ký tự.");
        CancelReason = reason.Trim();
        Status = SupplierOnboardingStatus.Cancelled;
    }

    /// <summary>Tên giấy tờ theo mã.</summary>
    /// <param name="code">Mã giấy tờ.</param>
    /// <returns>Tên tiếng Việt (mã lạ trả về chính mã).</returns>
    public static string NameOf(string code) =>
        SupplierOnboardingDocuments.All.FirstOrDefault(d => d.Code == code).Name ?? code;

    private void EnsureCanPropose(Guid by)
    {
        EnsureStatus(SupplierOnboardingStatus.Evaluated, "Đã đánh giá");
        if (EvaluatedBy == by)
            throw new BusinessRuleException("Người hoàn thành đánh giá không tự đề xuất – Văn phòng Ban đề xuất (tách người đánh giá và người đề xuất).");
    }

    private void SetProposal(bool recommended, string? comment, DateTimeOffset at, Guid by)
    {
        var cleaned = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (cleaned?.Length > TextMaxLength) throw new BusinessRuleException($"Ý kiến đề xuất tối đa {TextMaxLength} ký tự.");
        (Recommended, ProposalComment, ProposedAt, ProposedBy) = (recommended, cleaned, at, by);
    }

    private void EnsureStatus(SupplierOnboardingStatus expected, string name)
    {
        if (Status != expected) throw new BusinessRuleException($"Hồ sơ {No} không ở trạng thái {name}.");
    }

    private void Apply(SupplierOnboardingData data, DateOnly today)
    {
        if (data.CategoryId == Guid.Empty) throw new BusinessRuleException("Hãy chọn nhóm hàng đề xuất NCC cung cấp.");
        if (data.Date > today) throw new BusinessRuleException("Ngày đánh giá không được sau hôm nay.");
        if (data.Scores.All.Any(s => s is < MinScore or > MaxScore))
            throw new BusinessRuleException($"Điểm mỗi tiêu chí từ {MinScore} đến {MaxScore}.");
        if (data.Result is { } result && !Enum.IsDefined(result)) throw new BusinessRuleException("Kết luận đánh giá không hợp lệ.");
        var source = Clean(data.Source, ShortTextMaxLength, "Nguồn tìm thấy NCC");
        var comment = Clean(data.Comment, TextMaxLength, "Nhận xét");
        var note = Clean(data.Note, TextMaxLength, "Ghi chú");

        var unknown = data.Documents.Select(d => d.Code).Except(SupplierOnboardingDocuments.All.Select(d => d.Code)).FirstOrDefault();
        if (unknown is not null) throw new BusinessRuleException($"Giấy tờ {unknown} không có trong danh mục hồ sơ NCC.");
        if (data.Documents.GroupBy(d => d.Code).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi giấy tờ chỉ khai một dòng.");
        // Lưu đủ danh mục theo thứ tự cố định: giấy tờ không gửi lên = chưa nộp; chưa nộp thì bỏ ngày hiệu lực.
        var documents = SupplierOnboardingDocuments.All.Select(def =>
        {
            var d = data.Documents.FirstOrDefault(x => x.Code == def.Code);
            var docNote = Clean(d?.Note, ShortTextMaxLength, $"Ghi chú {def.Name}");
            return d is { Provided: true } ? new SupplierOnboardingDocument(def.Code, true, d.ValidTo, docNote) : new SupplierOnboardingDocument(def.Code, false, null, docNote);
        }).ToList();

        Date = data.Date;
        CategoryId = data.CategoryId;
        Source = source;
        DocumentsJson = JsonSerializer.Serialize(documents, Json);
        (PriceScore, SampleQualityScore, CapacityScore, DeliveryScore, PaymentScore, LegalScore) =
            (data.Scores.Price, data.Scores.SampleQuality, data.Scores.Capacity, data.Scores.Delivery, data.Scores.Payment, data.Scores.Legal);
        Result = data.Result;
        Comment = comment;
        Note = note;
    }

    private static string? Clean(string? text, int max, string label)
    {
        var cleaned = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (cleaned?.Length > max) throw new BusinessRuleException($"{label} tối đa {max} ký tự.");
        return cleaned;
    }
}
