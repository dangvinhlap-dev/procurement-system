using System.Buffers.Binary;
using Proc.Domain.Common;

namespace Proc.Domain.Documents;

/// <summary>Định dạng và kích thước một ảnh in lên phiếu (chữ ký, logo).</summary>
/// <param name="ContentType">"image/png" hoặc "image/jpeg".</param>
/// <param name="Width">Rộng (điểm ảnh).</param>
/// <param name="Height">Cao (điểm ảnh).</param>
public sealed record PrintImageInfo(string ContentType, int Width, int Height);

/// <summary>
/// Kiểm ảnh chữ ký / logo trước khi lưu (lát IN1 – design.md mục 20.3): đọc byte đầu file để biết đúng PNG hay JPEG (không tin đuôi file hay
/// Content-Type của trình duyệt), lấy rộng – cao từ phần đầu ảnh. Không giải mã cả ảnh – PDFsharp tự đọc khi in.
/// </summary>
public static class PrintImages
{
    /// <summary>Kiểu PNG.</summary>
    public const string Png = "image/png";

    /// <summary>Kiểu JPEG.</summary>
    public const string Jpeg = "image/jpeg";

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Đọc định dạng, kích thước ảnh và kiểm giới hạn.</summary>
    /// <param name="image">Nội dung file.</param>
    /// <param name="what">Tên ảnh trong thông báo lỗi ("Ảnh chữ ký", "Logo").</param>
    /// <param name="maxBytes">Dung lượng tối đa (byte).</param>
    /// <param name="allowJpeg">Cho phép JPEG (logo); chữ ký chỉ nhận PNG vì cần nền trong suốt.</param>
    /// <returns>Định dạng và kích thước.</returns>
    /// <exception cref="BusinessRuleException">File rỗng, quá lớn, không phải PNG/JPEG, kích thước bất thường.</exception>
    public static PrintImageInfo Inspect(byte[] image, string what, int maxBytes, bool allowJpeg)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length == 0) throw new BusinessRuleException($"{what}: file rỗng.");
        if (image.Length > maxBytes)
            throw new BusinessRuleException($"{what}: dung lượng {image.Length / 1024} KB vượt mức tối đa {maxBytes / 1024} KB.");

        PrintImageInfo? info = null;
        if (image.Length >= 24 && image.AsSpan(0, 8).SequenceEqual(PngMagic))
            info = new PrintImageInfo(Png, BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20, 4)));
        else if (allowJpeg && image.Length >= 4 && image[0] == 0xFF && image[1] == 0xD8) info = JpegSize(image);

        if (info is null) throw new BusinessRuleException(allowJpeg ? $"{what}: chỉ nhận ảnh PNG hoặc JPG." : $"{what}: chỉ nhận ảnh PNG.");
        // 4000 điểm ảnh mỗi chiều là đủ cho mọi ảnh chụp điện thoại; lớn hơn là file hỏng hoặc cố ý làm tràn bộ nhớ khi in.
        if (info.Width is < 1 or > 4000 || info.Height is < 1 or > 4000)
            throw new BusinessRuleException($"{what}: kích thước ảnh {info.Width}×{info.Height} không hợp lệ (tối đa 4000×4000).");
        return info;
    }

    /// <summary>
    /// Tìm khung SOF (0xC0–0xCF trừ C4, C8, CC) để lấy kích thước JPEG: SOF = FF Cx, độ dài 2 byte, độ chính xác 1 byte, cao 2 byte, rộng 2 byte.
    /// </summary>
    private static PrintImageInfo? JpegSize(byte[] image)
    {
        var i = 2;
        while (i + 9 < image.Length)
        {
            if (image[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = image[i + 1];
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                i += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(i + 2, 2));
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return new PrintImageInfo(Jpeg, BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(i + 7, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(i + 5, 2)));
            if (length < 2) return null;
            i += 2 + length;
        }

        return null;
    }
}
