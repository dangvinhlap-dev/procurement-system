using System.Security.Cryptography;

namespace Proc.Infrastructure.Operations;

/// <summary>Kết quả chép bổ sung một thư mục.</summary>
/// <param name="CopiedFiles">Số file mới / đổi đã chép.</param>
/// <param name="CopiedBytes">Dung lượng đã chép.</param>
/// <param name="TotalFiles">Số file trong thư mục nguồn.</param>
/// <param name="MissingAfterCopy">Số file nguồn vẫn chưa có (hoặc khác dung lượng) ở đích sau khi chép – 0 là đạt.</param>
public sealed record MirrorResult(int CopiedFiles, long CopiedBytes, int TotalFiles, int MissingAfterCopy);

/// <summary>
/// Thao tác file cho sao lưu (lát QT3b) – chỉ dùng API .NET đa nền tảng (Windows / Linux container), ghép đường dẫn bằng <see cref="Path"/>.
/// </summary>
public static class BackupFiles
{
    /// <summary>Đuôi file / thư mục đang chép dở – chép xong mới đổi tên, mất điện giữa chừng không để lại bản thiếu mang tên thật.</summary>
    public const string PartialSuffix = ".partial";

    /// <summary>SHA256 của file (hex thường).</summary>
    /// <param name="file">File.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Mã băm.</returns>
    public static async Task<string> Sha256Async(string file, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Tổng dung lượng và số file của thư mục (kể cả thư mục con).</summary>
    /// <param name="dir">Thư mục.</param>
    /// <returns>(byte, số file).</returns>
    public static (long Bytes, int Files) DirectoryStats(string dir)
    {
        long bytes = 0;
        var files = 0;
        foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            bytes += f.Length;
            files++;
        }

        return (bytes, files);
    }

    /// <summary>
    /// Chép bổ sung <paramref name="source"/> sang <paramref name="target"/>: file chưa có, khác dung lượng hoặc mới sửa hơn thì chép (ra file
    /// <c>.partial</c> rồi đổi tên), giữ giờ sửa của nguồn để lần sau so được. Không xóa file ở đích khi nguồn đã xóa – bản sao lưu giữ lại file
    /// đính kèm đã xóa nhầm. Sau khi chép đối chiếu lại: mọi file nguồn phải có ở đích cùng dung lượng.
    /// </summary>
    /// <param name="source">Thư mục nguồn (chưa có thì coi như rỗng).</param>
    /// <param name="target">Thư mục đích.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Số file đã chép, đối chiếu.</returns>
    public static async Task<MirrorResult> MirrorAsync(string source, string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(target);
        if (!Directory.Exists(source)) return new MirrorResult(0, 0, 0, 0);

        var copied = 0;
        long bytes = 0;
        var total = 0;
        var missing = 0;
        var sourceRoot = new DirectoryInfo(source);
        foreach (var file in sourceRoot.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.Name.EndsWith(PartialSuffix, StringComparison.Ordinal)) continue;
            total++;
            var rel = Path.GetRelativePath(sourceRoot.FullName, file.FullName);
            var dest = new FileInfo(Path.Combine(target, rel));
            if (dest.Exists && dest.Length == file.Length && dest.LastWriteTimeUtc >= file.LastWriteTimeUtc) continue;

            try
            {
                await CopyFileAsync(file.FullName, dest.FullName, cancellationToken);
                File.SetLastWriteTimeUtc(dest.FullName, file.LastWriteTimeUtc);
                copied++;
                bytes += file.Length;
            }
            catch (FileNotFoundException)
            {
                // File nguồn vừa bị xóa trong lúc chép – không tính.
                total--;
            }
        }

        // Đối chiếu sau khi chép (bước "kiểm tra" của bản đính kèm).
        foreach (var file in sourceRoot.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (file.Name.EndsWith(PartialSuffix, StringComparison.Ordinal)) continue;
            var dest = new FileInfo(Path.Combine(target, Path.GetRelativePath(sourceRoot.FullName, file.FullName)));
            if (!dest.Exists || dest.Length != file.Length) missing++;
        }

        return new MirrorResult(copied, bytes, total, missing);
    }

    /// <summary>
    /// Chép nguyên một bản sao lưu (file hoặc thư mục) sang đích: chép vào tên <c>.partial</c> rồi đổi tên; đích đã có thì chép đè. Trả về dung
    /// lượng và số file ở đích để đối chiếu với bản gốc.
    /// </summary>
    /// <param name="source">File hoặc thư mục nguồn.</param>
    /// <param name="target">Đường dẫn đích (cùng kiểu với nguồn).</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>(byte, số file) đã có ở đích.</returns>
    /// <exception cref="FileNotFoundException">Khi nguồn không còn.</exception>
    public static async Task<(long Bytes, int Files)> CopyBackupAsync(string source, string target, CancellationToken cancellationToken)
    {
        if (File.Exists(source))
        {
            await CopyFileAsync(source, target, cancellationToken);
            return (new FileInfo(target).Length, 1);
        }

        if (!Directory.Exists(source)) throw new FileNotFoundException("Không còn bản sao lưu để chép.", source);
        var staging = target + PartialSuffix;
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        foreach (var file in new DirectoryInfo(source).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dest = Path.Combine(staging, Path.GetRelativePath(source, file.FullName));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
            await using var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
        }

        Directory.CreateDirectory(staging);
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        Directory.Move(staging, target);
        return DirectoryStats(target);
    }

    /// <summary>Xóa file / thư mục nếu có (bỏ qua khi không còn).</summary>
    /// <param name="path">Đường dẫn.</param>
    public static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    /// <summary>Chép một file qua tên <c>.partial</c> rồi đổi tên (ghi đè đích).</summary>
    private static async Task CopyFileAsync(string source, string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        var partial = target + PartialSuffix;
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, useAsync: true))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        File.Move(partial, target, overwrite: true);
    }
}
