using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>
/// Một phiên bản chữ ký của người dùng để in vào ô ký của phiếu đã duyệt (lát IN1 – design.md mục 20.3, người dùng chốt 18/11/2026):
/// <list type="bullet">
/// <item>Ảnh PNG nền trong suốt – giao diện đã tách nền, cắt sát nét, đổi màu mực trước khi gửi; máy chủ chỉ kiểm định dạng, cỡ.</item>
/// <item><b>Theo phiên bản</b>: thay chữ ký mới thì phiên bản cũ kết thúc hiệu lực (<see cref="ValidTo"/>) nhưng vẫn giữ; phiếu duyệt trước lúc
/// thay vẫn in chữ ký cũ, chữ ký mới chỉ tính từ lúc thay (người dùng chốt câu 1).</item>
/// <item>Người dùng tự tải; người có quyền quản trị chữ ký tải thay (bắt buộc lý do, người được tải thay nhận thông báo).</item>
/// <item>Không xóa: "ngừng dùng" chỉ đóng hiệu lực phiên bản đang dùng; ảnh ghi nhật ký dạng "***" (không đưa ảnh vào AuditLog).</item>
/// </list>
/// </summary>
[Auditable("Chữ ký người dùng", LabelProperty = nameof(VersionNo), Redact = [nameof(Image)])]
public sealed class UserSignature : AuditableEntity
{
    /// <summary>Dung lượng ảnh tối đa sau xử lý (512 KB) – ảnh rộng tối đa 600 điểm thường dưới 60 KB.</summary>
    public const int MaxImageBytes = 512 * 1024;

    /// <summary>Độ dài lý do tối đa.</summary>
    public const int ReasonMaxLength = 500;

    private UserSignature()
    {
    }

    /// <summary>Người dùng sở hữu chữ ký.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Số phiên bản của người dùng, từ 1.</summary>
    public int VersionNo { get; private set; }

    /// <summary>Ảnh PNG (bytea).</summary>
    public byte[] Image { get; private set; } = [];

    /// <summary>Rộng ảnh (điểm ảnh).</summary>
    public int Width { get; private set; }

    /// <summary>Cao ảnh (điểm ảnh).</summary>
    public int Height { get; private set; }

    /// <summary>Bắt đầu hiệu lực (UTC) – lúc tải lên.</summary>
    public DateTimeOffset ValidFrom { get; private set; }

    /// <summary>Hết hiệu lực (UTC) – lúc thay chữ ký mới hoặc ngừng dùng; null = đang dùng.</summary>
    public DateTimeOffset? ValidTo { get; private set; }

    /// <summary>Người tải lên.</summary>
    public Guid UploadedBy { get; private set; }

    /// <summary>Người quản trị tải thay (không phải chính chủ).</summary>
    public bool ByAdmin { get; private set; }

    /// <summary>Lý do (bắt buộc khi tải thay).</summary>
    public string? Reason { get; private set; }

    /// <summary>Người kết thúc hiệu lực (thay mới / ngừng dùng).</summary>
    public Guid? RetiredBy { get; private set; }

    /// <summary>Lý do ngừng dùng (bắt buộc khi người quản trị ngừng dùng chữ ký người khác).</summary>
    public string? RetireReason { get; private set; }

    /// <summary>Chữ ký có hiệu lực tại thời điểm <paramref name="at"/>: ValidFrom ≤ at &lt; ValidTo.</summary>
    /// <param name="at">Thời điểm duyệt / gửi duyệt.</param>
    /// <returns>true nếu có hiệu lực.</returns>
    public bool IsValidAt(DateTimeOffset at) => ValidFrom <= at && (ValidTo is null || at < ValidTo);

    /// <summary>Tạo phiên bản mới.</summary>
    /// <param name="userId">Chủ chữ ký.</param>
    /// <param name="versionNo">Số phiên bản (lớn nhất hiện có + 1).</param>
    /// <param name="image">Ảnh PNG.</param>
    /// <param name="uploadedBy">Người tải.</param>
    /// <param name="at">Thời điểm tải (UTC).</param>
    /// <param name="reason">Lý do – bắt buộc khi người tải không phải chủ.</param>
    /// <returns>Phiên bản mới, có hiệu lực từ <paramref name="at"/>.</returns>
    /// <exception cref="BusinessRuleException">Ảnh không hợp lệ, thiếu lý do khi tải thay, lý do quá dài.</exception>
    public static UserSignature Create(Guid userId, int versionNo, byte[] image, Guid uploadedBy, DateTimeOffset at, string? reason)
    {
        var info = PrintImages.Inspect(image, "Ảnh chữ ký", MaxImageBytes, allowJpeg: false);
        var byAdmin = uploadedBy != userId;
        return new UserSignature
        {
            UserId = userId,
            VersionNo = versionNo,
            Image = image,
            Width = info.Width,
            Height = info.Height,
            ValidFrom = at,
            UploadedBy = uploadedBy,
            ByAdmin = byAdmin,
            Reason = CleanReason(reason, byAdmin, "tải chữ ký thay người khác"),
        };
    }

    /// <summary>Kết thúc hiệu lực (thay chữ ký mới hoặc ngừng dùng). Phiên bản giữ lại để in phiếu đã duyệt trước đó.</summary>
    /// <param name="at">Thời điểm (UTC).</param>
    /// <param name="by">Người thao tác.</param>
    /// <param name="reason">Lý do – bắt buộc khi người thao tác không phải chủ và không phải do thay mới.</param>
    /// <param name="replaced">Do tải chữ ký mới (lý do nằm ở phiên bản mới).</param>
    /// <exception cref="BusinessRuleException">Đã hết hiệu lực; thiếu lý do.</exception>
    public void Retire(DateTimeOffset at, Guid by, string? reason, bool replaced)
    {
        if (ValidTo is not null) throw new BusinessRuleException("Chữ ký này đã ngừng dùng.");
        RetireReason = replaced ? null : CleanReason(reason, by != UserId, "ngừng dùng chữ ký của người khác");
        // Không để ValidTo trước ValidFrom khi thay hai lần trong cùng một tích tắc đồng hồ.
        ValidTo = at < ValidFrom ? ValidFrom : at;
        RetiredBy = by;
    }

    private static string? CleanReason(string? reason, bool required, string action)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (required && text is null) throw new BusinessRuleException($"Phải nhập lý do khi {action}.");
        if (text is { Length: > ReasonMaxLength }) throw new BusinessRuleException($"Lý do tối đa {ReasonMaxLength} ký tự.");
        return text;
    }
}
