namespace Proc.IntegrationTests;

/// <summary>
/// Mã danh mục cho test. Từ 14/11/2026 mã nhóm hàng do người dùng gõ (design.md mục 5.7, N4-7) nên test phải tự đặt mã không trùng.
/// </summary>
internal static class TestCodes
{
    /// <summary>Mã nhóm hàng ngẫu nhiên, 10 ký tự chữ hoa/số (quy tắc mã nhóm 2–20 chữ không dấu hoặc số).</summary>
    /// <returns>Mã chưa dùng, ví dụ N3F9A0B21C.</returns>
    public static string Category() => $"N{Guid.NewGuid():N}"[..10].ToUpperInvariant();
}
