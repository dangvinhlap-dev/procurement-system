namespace Proc.Domain.Organization;

/// <summary>9 chỉ số KPI của Ban Cung ứng (design.md mục 18.18; đề xuất Ban CUVT "Đánh giá KPI"). Giá trị lưu vào tên tham số – không đổi số.</summary>
public enum KpiCode
{
    /// <summary>Tiết kiệm mua hàng (%): tiết kiệm đã xác minh ÷ giá trị mua thuộc phạm vi tối ưu.</summary>
    Saving = 1,

    /// <summary>Mua theo giá/hợp đồng đã duyệt (%).</summary>
    ContractCompliance = 2,

    /// <summary>Đơn hàng xử lý đúng SLA (%).</summary>
    PurchaseSla = 3,

    /// <summary>NCC giao đúng hạn (%).</summary>
    OnTimeDelivery = 4,

    /// <summary>Hàng đạt chất lượng (%).</summary>
    Quality = 5,

    /// <summary>Tỷ lệ mua khẩn cấp (%) – càng thấp càng tốt.</summary>
    Emergency = 6,

    /// <summary>Tồn kho chậm luân chuyển (%) – càng thấp càng tốt.</summary>
    SlowMoving = 7,

    /// <summary>Hoàn thành đánh giá NCC (%) – đo từ lát F4.</summary>
    SupplierReview = 8,

    /// <summary>Vi phạm nghiêm trọng (số vụ) – càng thấp càng tốt; đo từ lát F3.</summary>
    Violation = 9,
}

/// <summary>Định nghĩa một KPI: tên, chiều đánh giá, mục tiêu mặc định.</summary>
/// <param name="Code">Mã chỉ số.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Formula">Cách đo (hiển thị cho người xem).</param>
/// <param name="HigherIsBetter">true: đạt khi kết quả ≥ mục tiêu; false: đạt khi kết quả ≤ mục tiêu.</param>
/// <param name="DefaultTarget">Mục tiêu mặc định khi công ty chưa khai báo.</param>
/// <param name="IsCount">true: chỉ số đếm số vụ (số nguyên 0–1.000); false: tỷ lệ % (0–100).</param>
public sealed record KpiDefinition(KpiCode Code, string Name, string Formula, bool HigherIsBetter, decimal DefaultTarget, bool IsCount);

/// <summary>
/// Danh mục 9 KPI theo đề xuất Ban CUVT. Mục tiêu mặc định lấy từ cột "Mục tiêu tham khảo" của đề xuất; chỉ số đề xuất ghi "giảm dần" (mua khẩn,
/// tồn chậm) dùng mức trần 5% / 10% để có ngưỡng đánh giá – công ty tự sửa ở Tham số hệ thống.
/// </summary>
public static class KpiCatalog
{
    /// <summary>Số ngày không xuất mặc định để tính tồn chậm luân chuyển.</summary>
    public const int DefaultSlowMovingDays = 90;

    /// <summary>Số ngày không xuất tối đa (10 năm) – như báo cáo hàng chậm luân chuyển.</summary>
    public const int MaxSlowMovingDays = 3650;

    /// <summary>Số vụ vi phạm tối đa nhập được làm mục tiêu – lớn hơn gần như chắc chắn là gõ nhầm.</summary>
    public const int MaxViolationTarget = 1000;

    /// <summary>9 chỉ số theo thứ tự hiển thị.</summary>
    public static readonly IReadOnlyList<KpiDefinition> All =
    [
        new(KpiCode.Saving, "Tiết kiệm mua hàng",
            "Tiết kiệm chọn thầu so giá chào bình quân ÷ giá trị theo giá chào bình quân", true, 3m, false),
        new(KpiCode.ContractCompliance, "Mua theo giá/hợp đồng đã duyệt",
            "Giá trị PO theo giá đã duyệt ÷ giá trị PO xác định được nguồn giá", true, 95m, false),
        new(KpiCode.PurchaseSla, "Đơn hàng xử lý đúng SLA", "Dòng PR có PO đúng hạn ÷ dòng PR đã đến hạn hoặc đã có PO", true, 95m, false),
        new(KpiCode.OnTimeDelivery, "NCC giao đúng hạn", "Dòng nhận đúng hoặc trước ngày giao hẹn ÷ dòng nhận có ngày hẹn", true, 95m, false),
        new(KpiCode.Quality, "Hàng đạt chất lượng", "Dòng nhận kết quả Đạt ÷ dòng nhận đã kiểm tra", true, 98m, false),
        new(KpiCode.Emergency, "Tỷ lệ mua khẩn cấp", "Giá trị PO khẩn cấp ÷ tổng giá trị PO", false, 5m, false),
        new(KpiCode.SlowMoving, "Tồn kho chậm luân chuyển", "Giá trị tồn lâu không xuất ÷ tổng giá trị tồn (tại hôm nay)", false, 10m, false),
        new(KpiCode.SupplierReview, "Hoàn thành đánh giá NCC", "NCC đã đánh giá ÷ NCC phải đánh giá", true, 100m, false),
        new(KpiCode.Violation, "Vi phạm nghiêm trọng", "Số vụ được xác minh", false, 0m, true),
    ];

    /// <summary>Định nghĩa của một chỉ số.</summary>
    /// <param name="code">Mã chỉ số.</param>
    /// <returns>Định nghĩa.</returns>
    public static KpiDefinition Get(KpiCode code) => All.Single(k => k.Code == code);

    /// <summary>
    /// Kết quả có đạt mục tiêu không. Ví dụ mua theo hợp đồng 96,2% với mục tiêu ≥ 95% → đạt; mua khẩn 7% với mục tiêu ≤ 5% → không đạt.
    /// </summary>
    /// <param name="code">Mã chỉ số.</param>
    /// <param name="value">Kết quả; null = chưa có dữ liệu.</param>
    /// <param name="target">Mục tiêu.</param>
    /// <returns>null khi chưa có dữ liệu.</returns>
    public static bool? IsMet(KpiCode code, decimal? value, decimal target)
    {
        if (value is not { } v) return null;
        return Get(code).HigherIsBetter ? v >= target : v <= target;
    }
}
