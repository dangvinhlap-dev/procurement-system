using System.Net.Http.Json;
using Proc.Application.Organization;

namespace Proc.IntegrationTests;

/// <summary>Tạo bộ phận thử – PR bắt buộc chọn bộ phận của công ty (design.md mục 18.5).</summary>
internal static class TestDepartments
{
    /// <summary>
    /// Thêm một bộ phận mã ngẫu nhiên ở công ty đang làm việc của client.
    /// </summary>
    /// <param name="client">Client đã chọn công ty, có quyền thêm bộ phận.</param>
    /// <param name="name">Tên bộ phận.</param>
    /// <returns>Id bộ phận.</returns>
    public static async Task<Guid> CreateAsync(HttpClient client, string name = "Phân xưởng thử")
    {
        var code = $"T{Guid.NewGuid():N}".ToUpperInvariant()[..10];
        var response = await client.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest(code, name, null), TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<DepartmentDto>(TestContext.Current.CancellationToken))!.Id;
    }
}
