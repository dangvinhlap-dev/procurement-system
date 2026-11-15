using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Proc.Domain.Common;
using Proc.Domain.Operations;

namespace Proc.Infrastructure.Operations;

/// <summary>
/// Đọc gói phát hành (zip do <c>deploy\windows\publish.ps1</c> tạo – lát QT8): kiểm có <c>app\Proc.Api.dll</c>, đọc <c>rollback.json</c> (phiên bản,
/// mức quay về tối thiểu, migration của bản) và mục của bản trong <c>CHANGELOG.md</c> → mục kiểm thử (mỗi gạch đầu dòng của nhóm Thêm mới / Thay
/// đổi / Sửa lỗi… một mục), thay đổi CSDL, cách quay lại.
/// </summary>
public static class ReleasePackageReader
{
    /// <summary>Tên file khai mức quay về trong gói.</summary>
    public const string RollbackFile = "rollback.json";

    /// <summary>Tên file nhật ký thay đổi trong gói.</summary>
    public const string ChangelogFile = "CHANGELOG.md";

    /// <summary>Nhóm CHANGELOG không thành mục kiểm thử (đọc vào ô riêng hoặc chỉ là hướng dẫn cài).</summary>
    private static readonly string[] NonTestSections = ["Thay đổi CSDL", "Lưu ý khi nâng cấp", "Cách quay lại bản trước"];

    private static readonly Regex VersionHeading = new(@"^##\s*\[(?<v>[^\]]+)\]", RegexOptions.CultureInvariant);

    /// <summary>Nội dung đọc được từ gói.</summary>
    /// <param name="Version">Phiên bản (rollback.json).</param>
    /// <param name="MinRollbackLevel">Mức quay về tối thiểu.</param>
    /// <param name="Migrations">Migration của bản (rollback.json).</param>
    /// <param name="Changelog">Mục CHANGELOG của bản (cả tiêu đề).</param>
    /// <param name="DatabaseChanges">Nhóm "Thay đổi CSDL" (kèm danh sách migration nếu nhóm trống).</param>
    /// <param name="RollbackPlan">Nhóm "Cách quay lại bản trước".</param>
    /// <param name="TestItems">Mục kiểm thử (nhóm, nội dung).</param>
    public sealed record Content(
        string Version, RollbackLevel MinRollbackLevel, IReadOnlyList<string> Migrations, string? Changelog, string? DatabaseChanges,
        string? RollbackPlan, IReadOnlyList<(string Section, string Content)> TestItems);

    /// <summary>Đọc gói zip.</summary>
    /// <param name="zipPath">Đường dẫn file zip.</param>
    /// <returns>Nội dung.</returns>
    /// <exception cref="BusinessRuleException">Không phải zip, thiếu app\Proc.Api.dll / rollback.json, CHANGELOG không có mục của bản.</exception>
    public static Content Read(string zipPath)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(zipPath);
        }
        catch (InvalidDataException)
        {
            throw new BusinessRuleException("File tải lên không phải gói zip.");
        }

        using (archive)
        {
            if (Find(archive, "app/Proc.Api.dll") is null)
                throw new BusinessRuleException("Gói không có app\\Proc.Api.dll – không phải gói do deploy\\windows\\publish.ps1 tạo.");
            var rollbackEntry = Find(archive, RollbackFile)
                ?? throw new BusinessRuleException("Gói không có rollback.json – gói đóng bằng publish.ps1 từ bản 0.36.0 mới nhận được ở trung tâm phát hành.");
            var (version, level, migrations) = ParseRollback(ReadText(rollbackEntry));
            var changelog = Find(archive, ChangelogFile) is { } entry ? ReadText(entry) : null;
            return FromChangelog(version, level, migrations, changelog);
        }
    }

    /// <summary>Đọc <c>rollback.json</c>: <c>{ "version": "0.36.0", "minLevel": 1, "migrations": ["V0_36_0_…"] }</c>.</summary>
    /// <param name="json">Nội dung.</param>
    /// <returns>Phiên bản, mức tối thiểu, migration.</returns>
    /// <exception cref="BusinessRuleException">JSON sai, thiếu phiên bản, mức ngoài 1–3.</exception>
    public static (string Version, RollbackLevel Level, IReadOnlyList<string> Migrations) ParseRollback(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var version = root.TryGetProperty("version", out var v) ? v.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(version)) throw new BusinessRuleException("rollback.json thiếu \"version\".");
            var level = root.TryGetProperty("minLevel", out var l) && l.TryGetInt32(out var n) ? n : 0;
            if (level is < 1 or > 3) throw new BusinessRuleException("rollback.json: \"minLevel\" phải là 1, 2 hoặc 3 (design.md 21.6a).");
            var migrations = root.TryGetProperty("migrations", out var m) && m.ValueKind == JsonValueKind.Array
                ? m.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : [];
            return (version, (RollbackLevel)level, migrations);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("rollback.json không đúng dạng JSON.");
        }
    }

    /// <summary>
    /// Lấy mục của bản trong CHANGELOG (từ <c>## [x.y.z]</c> tới tiêu đề bản kế tiếp) và tách nhóm <c>### …</c>: mỗi dòng <c>- …</c> (gộp các dòng
    /// thụt lề nối tiếp) của nhóm thường là một mục kiểm thử; bỏ dòng "Không có."; nhóm Thay đổi CSDL / Cách quay lại vào ô riêng.
    /// </summary>
    /// <param name="version">Phiên bản.</param>
    /// <param name="level">Mức quay về.</param>
    /// <param name="migrations">Migration của bản.</param>
    /// <param name="changelog">Toàn văn CHANGELOG; null = gói không có.</param>
    /// <returns>Nội dung.</returns>
    /// <exception cref="BusinessRuleException">CHANGELOG có nhưng không có mục của bản.</exception>
    public static Content FromChangelog(string version, RollbackLevel level, IReadOnlyList<string> migrations, string? changelog)
    {
        var migrationText = migrations.Count == 0 ? "Không có migration." : "Migration: " + string.Join(", ", migrations);
        if (changelog is null) return new Content(version, level, migrations, null, migrationText, null, []);

        var lines = changelog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, l => VersionHeading.Match(l) is { Success: true } m && m.Groups["v"].Value.Trim() == version);
        if (start < 0) throw new BusinessRuleException($"CHANGELOG.md trong gói không có mục \"## [{version}]\".");
        var end = Array.FindIndex(lines, start + 1, l => VersionHeading.IsMatch(l));
        var section = lines[start..(end < 0 ? lines.Length : end)];

        var items = new List<(string, string)>();
        var groups = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        StringBuilder? bullet = null;
        void Flush()
        {
            if (bullet is null || current is null) return;
            var text = bullet.ToString().Trim();
            bullet = null;
            if (text.Length == 0 || text.StartsWith("Không có", StringComparison.OrdinalIgnoreCase)) return;
            if (!NonTestSections.Contains(current, StringComparer.OrdinalIgnoreCase)) items.Add((current, text));
        }

        foreach (var line in section.Skip(1))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Flush();
                current = line[4..].Trim();
                groups[current] = new StringBuilder();
                continue;
            }

            if (current is null) continue;
            groups[current].AppendLine(line);
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                Flush();
                bullet = new StringBuilder(line[2..].Trim());
            }
            else if (bullet is not null && line.StartsWith(' ') && line.Trim().Length > 0)
            {
                bullet.Append(' ').Append(line.Trim());
            }
            else
            {
                Flush();
            }
        }

        Flush();
        var db = groups.TryGetValue("Thay đổi CSDL", out var dbText) ? dbText.ToString().Trim() : null;
        var plan = groups.TryGetValue("Cách quay lại bản trước", out var planText) ? planText.ToString().Trim() : null;
        return new Content(
            version, level, migrations, string.Join('\n', section).Trim(),
            string.IsNullOrEmpty(db) ? migrationText : db + "\n" + migrationText,
            string.IsNullOrEmpty(plan) ? null : plan,
            items);
    }

    private static ZipArchiveEntry? Find(ZipArchive archive, string path) =>
        archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
