using System.Text;
using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Tests.Documents;

/// <summary>
/// Kiểm nội dung file đính kèm khớp đuôi (chữ ký đầu file) và chặn file chạy được dù đổi đuôi – lát P1b, design.md 19.5 (P1b-3).
/// </summary>
public class FileSignatureTests
{
    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static TheoryData<string, byte[]> HopLe => new()
    {
        { ".pdf", Ascii("%PDF-1.7\n...") },
        // Một số máy quét chèn vài byte rác trước "%PDF-" – chuẩn PDF cho phép trong 1.024 byte đầu.
        { ".pdf", [.. Bytes(0xEF, 0xBB, 0xBF), .. Ascii("%PDF-1.4")] },
        { ".docx", Bytes(0x50, 0x4B, 0x03, 0x04, 0x14, 0x00) },
        { ".xlsx", Bytes(0x50, 0x4B, 0x03, 0x04) },
        { ".zip", Bytes(0x50, 0x4B, 0x05, 0x06) },
        { ".doc", Bytes(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1) },
        { ".msg", Bytes(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1) },
        { ".jpg", Bytes(0xFF, 0xD8, 0xFF, 0xE0) },
        { ".png", Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) },
        { ".gif", Ascii("GIF89a") },
        { ".bmp", Ascii("BM6") },
        { ".tif", Bytes(0x49, 0x49, 0x2A, 0x00) },
        { ".tiff", Bytes(0x4D, 0x4D, 0x00, 0x2A) },
        { ".rar", Bytes(0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00) },
        { ".7z", Bytes(0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C) },
        { ".txt", Encoding.UTF8.GetBytes("Biên bản giao hàng") },
        { ".csv", Ascii("ma;ten\nA;B") },
        { ".xml", Ascii("<?xml version=\"1.0\"?><HDon/>") },
        { ".eml", Ascii("From: a@b.vn\r\nSubject: x") },
    };

    [Theory]
    [MemberData(nameof(HopLe))]
    public void NoiDungKhopDuoi_DuocNhan(string extension, byte[] header) =>
        FileSignature.EnsureMatches(extension, "tep" + extension, header);

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".xlsx")]
    [InlineData(".doc")]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".zip")]
    public void DoiDuoi_NoiDungKhongKhop_BiChan(string extension)
    {
        // Trang HTML / văn bản đổi đuôi thành tài liệu: không đúng định dạng của đuôi.
        var ex = Assert.Throws<BusinessRuleException>(() =>
            FileSignature.EnsureMatches(extension, "bao-gia" + extension, Ascii("<html><script>alert(1)</script></html>")));

        Assert.Contains("không đúng định dạng", ex.Message);
        Assert.Contains("bao-gia" + extension, ex.Message);
    }

    public static TheoryData<string, byte[]> FileChay => new()
    {
        { ".txt", Ascii("MZ\x90\0") },
        { ".csv", Bytes(0x7F, 0x45, 0x4C, 0x46, 0x02) },
        { ".xml", Bytes(0xCF, 0xFA, 0xED, 0xFE) },
        { ".txt", Ascii("#!/bin/sh\nrm -rf /") },
    };

    [Theory]
    [MemberData(nameof(FileChay))]
    public void FileChayDuoc_DoiDuoiVanBan_BiChan(string extension, byte[] header)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => FileSignature.EnsureMatches(extension, "huong-dan" + extension, header));

        Assert.Contains("chương trình chạy được", ex.Message);
    }

    [Fact]
    public void FileRong_BiChan() =>
        Assert.Throws<BusinessRuleException>(() => FileSignature.EnsureMatches(".pdf", "a.pdf", []));

    [Fact]
    public void MoiDuoiDuocNhan_DeuCoQuyTacKiem() =>
        Assert.All(Attachment.AllowedExtensions.Keys, e => Assert.True(FileSignature.HasRule(e), e));
}
