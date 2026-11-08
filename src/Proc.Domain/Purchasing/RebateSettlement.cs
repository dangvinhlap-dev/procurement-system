namespace Proc.Domain.Purchasing;

/// <summary>
/// Quy tắc quyết toán chiết khấu/tài trợ dùng chung cho cam kết trên hợp đồng (lát F4b – design.md mục 18.21) và thỏa thuận chiết khấu sản
/// lượng gộp (lát G6b – mục 18.25): hạn ghi nhận sau cuối kỳ và tình trạng kỳ, để màn hình và dấu hiệu KSNB (mục 18.19) ra cùng một cách hiểu.
/// </summary>
public static class RebateSettlement
{
    /// <summary>Số ngày sau cuối kỳ để NCC trả và người mua ghi nhận; quá hạn mà thiếu thì là dấu hiệu KSNB.</summary>
    public const int GraceDays = 30;

    /// <summary>Đã nhận đủ (đã nhận ≥ phải nhận &gt; 0).</summary>
    public const string Received = "Received";

    /// <summary>Chưa hết kỳ.</summary>
    public const string NotDue = "NotDue";

    /// <summary>Hết kỳ, phải nhận 0 (chưa đạt doanh số tối thiểu/bậc nào, không phát sinh mua).</summary>
    public const string NotReached = "NotReached";

    /// <summary>Hết kỳ, còn trong hạn ghi nhận.</summary>
    public const string Waiting = "Waiting";

    /// <summary>Quá hạn ghi nhận mà còn thiếu – dấu hiệu KSNB.</summary>
    public const string Short = "Short";

    /// <summary>Hạn ghi nhận của kỳ kết thúc ngày <paramref name="periodTo"/>: cuối kỳ + <see cref="GraceDays"/> ngày.</summary>
    /// <param name="periodTo">Ngày cuối kỳ.</param>
    /// <returns>Hạn ghi nhận.</returns>
    public static DateOnly DueDate(DateOnly periodTo) => periodTo.AddDays(GraceDays);

    /// <summary>
    /// Tình trạng kỳ: đủ khi đã nhận ≥ phải nhận (&gt; 0); chưa hết kỳ → NotDue; hết kỳ mà phải nhận 0 → NotReached; trong hạn ghi nhận →
    /// Waiting; quá hạn mà thiếu → Short. Ví dụ kỳ quý 3 kết thúc 30/09, hạn 30/10, hôm nay 02/11, phải nhận 25.000.000, đã nhận 10.000.000 →
    /// Short, thiếu 15.000.000.
    /// </summary>
    /// <param name="today">Hôm nay theo giờ Việt Nam.</param>
    /// <param name="periodTo">Ngày cuối kỳ.</param>
    /// <param name="expected">Phải nhận.</param>
    /// <param name="received">Đã nhận (không tính bản ghi đã hủy).</param>
    /// <returns>Một trong các hằng của lớp này.</returns>
    public static string StatusOf(DateOnly today, DateOnly periodTo, decimal expected, decimal received)
    {
        if (expected > 0 && received >= expected) return Received;
        if (today <= periodTo) return NotDue;
        if (expected == 0) return NotReached;
        return today <= DueDate(periodTo) ? Waiting : Short;
    }
}
