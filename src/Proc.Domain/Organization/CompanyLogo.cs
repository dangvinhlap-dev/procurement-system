using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Organization;

/// <summary>
/// Logo của một công ty, in góc trên trái phiếu theo biểu mẫu CUVT (lát IN1 – design.md mục 20.3, người dùng chốt 18/11/2026: làm chứng từ ở
/// công ty nào thì in logo công ty đó). Bảng riêng, không gắn vào <see cref="Company"/>, để đọc danh mục công ty không phải kéo theo ảnh.
/// Mỗi công ty tối đa một logo; thay logo là ghi đè (phiếu in lại dùng logo mới – logo không phải chữ ký nên không giữ phiên bản).
/// </summary>
[Auditable("Logo công ty", Redact = [nameof(Image)])]
public sealed class CompanyLogo : AuditableEntity
{
    /// <summary>Dung lượng tối đa (1 MB) – giao diện thu ảnh về rộng tối đa 600 điểm trước khi gửi.</summary>
    public const int MaxImageBytes = 1024 * 1024;

    private CompanyLogo()
    {
    }

    /// <summary>Công ty.</summary>
    public Guid CompanyId { get; private set; }

    /// <summary>Ảnh PNG hoặc JPEG.</summary>
    public byte[] Image { get; private set; } = [];

    /// <summary>"image/png" hoặc "image/jpeg".</summary>
    public string ContentType { get; private set; } = PrintImages.Png;

    /// <summary>Rộng (điểm ảnh).</summary>
    public int Width { get; private set; }

    /// <summary>Cao (điểm ảnh).</summary>
    public int Height { get; private set; }

    /// <summary>Tạo logo cho công ty.</summary>
    /// <param name="companyId">Công ty.</param>
    /// <param name="image">Ảnh PNG/JPEG.</param>
    /// <returns>Logo.</returns>
    /// <exception cref="BusinessRuleException">Ảnh không hợp lệ.</exception>
    public static CompanyLogo Create(Guid companyId, byte[] image)
    {
        var logo = new CompanyLogo { CompanyId = companyId };
        logo.Replace(image);
        return logo;
    }

    /// <summary>Thay ảnh.</summary>
    /// <param name="image">Ảnh PNG/JPEG.</param>
    /// <exception cref="BusinessRuleException">Ảnh không hợp lệ.</exception>
    public void Replace(byte[] image)
    {
        var info = PrintImages.Inspect(image, "Logo", MaxImageBytes, allowJpeg: true);
        Image = image;
        ContentType = info.ContentType;
        Width = info.Width;
        Height = info.Height;
    }
}
