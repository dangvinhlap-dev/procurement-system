using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Proc.IntegrationTests;

/// <summary>
/// Tạo ảnh PNG RGBA thật (nền trong suốt, một nét chéo xanh) cho test chữ ký / logo (lát IN1) – không cần thư viện ảnh: tự ghép khung IHDR, IDAT
/// (zlib), IEND kèm CRC32.
/// </summary>
internal static class TestPng
{
    /// <summary>Ảnh <paramref name="width"/> × <paramref name="height"/>, nét chéo mực xanh, phần còn lại trong suốt.</summary>
    public static byte[] Create(int width, int height)
    {
        var raw = new byte[height * (1 + width * 4)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 4);
            raw[row] = 0; // bộ lọc "None"
            var x0 = y * width / height;
            for (var x = Math.Max(0, x0 - 2); x < Math.Min(width, x0 + 3); x++)
            {
                var p = row + 1 + x * 4;
                (raw[p], raw[p + 1], raw[p + 2], raw[p + 3]) = ((byte)0x1A, (byte)0x3A, (byte)0x9C, (byte)0xFF);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // 8 bit mỗi kênh
        header[9] = 6; // RGBA
        Chunk(png, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
            Chunk(png, "IDAT", compressed.ToArray());
        }

        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typed);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typed));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return ~crc;
    }
}
