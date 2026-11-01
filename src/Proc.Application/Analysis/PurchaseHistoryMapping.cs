namespace Proc.Application.Analysis;

/// <summary>Loại ánh xạ dữ liệu mua lịch sử.</summary>
public static class PurchaseHistoryMappingKinds
{
    /// <summary>Mã vật tư cũ → mã hàng hóa chung.</summary>
    public const string Item = "item";

    /// <summary>NCC cũ (MST hoặc tên) → NCC trong danh mục.</summary>
    public const string Supplier = "supplier";
}

/// <summary>
/// Một mã vật tư cũ hoặc một NCC cũ trên dữ liệu mua lịch sử của pháp nhân (gom mọi dòng cùng khóa).
/// </summary>
/// <param name="Key">Khóa: mã vật tư cũ, hoặc khóa NCC "MST:…"/"TEN:…".</param>
/// <param name="Code">Mã vật tư cũ, hoặc MST NCC (null khi NCC không có MST).</param>
/// <param name="Name">Tên hay gặp nhất trên các dòng.</param>
/// <param name="Detail">Mặt hàng: quy cách · ĐVT; NCC: null.</param>
/// <param name="RecordCount">Số dòng mua.</param>
/// <param name="MatchedCount">Số dòng đã gán mặt hàng/NCC chung.</param>
/// <param name="Amount">Tổng thành tiền trước VAT (cộng thẳng, chưa quy đổi ngoại tệ).</param>
/// <param name="LastPurchaseDate">Ngày mua gần nhất.</param>
/// <param name="TargetId">Mặt hàng/NCC chung đang gán (khi mọi dòng đã gán cùng một đích).</param>
/// <param name="TargetCode">Mã của đích.</param>
/// <param name="TargetName">Tên của đích.</param>
/// <param name="HasMapping">true khi gán bằng ánh xạ người dùng lưu; false khi tự khớp theo mã/MST hoặc chưa gán.</param>
public sealed record PurchaseHistorySourceDto(
    string Key, string? Code, string Name, string? Detail, int RecordCount, int MatchedCount, decimal Amount, DateOnly LastPurchaseDate,
    Guid? TargetId, string? TargetCode, string? TargetName, bool HasMapping);

/// <summary>
/// Một mặt hàng/NCC trong danh mục được gợi ý làm đích ánh xạ.
/// </summary>
/// <param name="Id">Id mặt hàng/NCC.</param>
/// <param name="Code">Mã.</param>
/// <param name="Name">Tên.</param>
/// <param name="Detail">Mặt hàng: quy cách; NCC: MST.</param>
/// <param name="Score">Điểm giống tên (0–100) so với mã cũ.</param>
public sealed record PurchaseHistoryCandidateDto(Guid Id, string Code, string Name, string? Detail, int Score);

/// <summary>
/// Lưu ánh xạ.
/// </summary>
/// <param name="Kind">Loại (<see cref="PurchaseHistoryMappingKinds"/>).</param>
/// <param name="Key">Khóa nguồn (như <see cref="PurchaseHistorySourceDto.Key"/>).</param>
/// <param name="TargetId">Mặt hàng/NCC chung.</param>
public sealed record SavePurchaseHistoryMappingRequest(string Kind, string Key, Guid TargetId);

/// <summary>Kết quả lưu/bỏ ánh xạ.</summary>
/// <param name="UpdatedRecords">Số dòng mua lịch sử được gán lại.</param>
public sealed record PurchaseHistoryMappingResult(int UpdatedRecords);

/// <summary>
/// Chuẩn hóa dữ liệu mua lịch sử (design.md mục 18.2, lát E2): gán mã vật tư cũ của từng pháp nhân về mã hàng chung, NCC cũ về NCC
/// trong danh mục, có gợi ý trùng gần đúng. Mọi thao tác ở công ty đang làm việc; mặt hàng chỉ chọn trong nhóm hàng được xem.
/// </summary>
public interface IPurchaseHistoryMappingService
{
    /// <summary>
    /// Các mã vật tư cũ / NCC cũ trên dữ liệu mua lịch sử của công ty, nhiều tiền trước.
    /// </summary>
    /// <param name="kind">Loại (<see cref="PurchaseHistoryMappingKinds"/>).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các nguồn.</returns>
    /// <exception cref="Proc.Domain.Common.BusinessRuleException">Khi loại không hợp lệ.</exception>
    Task<IReadOnlyList<PurchaseHistorySourceDto>> ListSourcesAsync(string kind, CancellationToken cancellationToken);

    /// <summary>
    /// Gợi ý đích ánh xạ: không có từ khóa thì 5 mặt hàng/NCC tên giống nhất; có từ khóa thì tới 20 kết quả khớp từ khóa (mã, tên, có dấu
    /// hay không), giống nhất trước.
    /// </summary>
    /// <param name="kind">Loại.</param>
    /// <param name="key">Khóa nguồn.</param>
    /// <param name="search">Từ khóa; null = chỉ gợi ý theo tên.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Gợi ý, điểm cao trước.</returns>
    /// <exception cref="Common.NotFoundException">Khi công ty không có dòng nào mang khóa này.</exception>
    Task<IReadOnlyList<PurchaseHistoryCandidateDto>> ListCandidatesAsync(string kind, string key, string? search, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu (hoặc đổi) ánh xạ và gán đích cho mọi dòng mua lịch sử cùng khóa của công ty; lần nhập sau tự áp. Ghi nhật ký.
    /// </summary>
    /// <param name="request">Loại, khóa, đích.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số dòng được gán.</returns>
    /// <exception cref="Common.NotFoundException">Khi không có dòng mang khóa này, hoặc đích không có/không được xem.</exception>
    /// <exception cref="Common.ForbiddenAccessException">Khi không có quyền nhập dữ liệu mua lịch sử.</exception>
    Task<PurchaseHistoryMappingResult> SaveAsync(SavePurchaseHistoryMappingRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Bỏ ánh xạ: các dòng cùng khóa quay về kết quả tự khớp (mã vật tư trùng mã hàng chung, MST trùng NCC), không khớp thì để trống.
    /// </summary>
    /// <param name="kind">Loại.</param>
    /// <param name="key">Khóa nguồn.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số dòng được gán lại.</returns>
    /// <exception cref="Common.NotFoundException">Khi khóa chưa có ánh xạ.</exception>
    Task<PurchaseHistoryMappingResult> DeleteAsync(string kind, string key, CancellationToken cancellationToken);
}
