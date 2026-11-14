using System.Net.Http.Json;
using Proc.Application.MasterData;

namespace Proc.IntegrationTests;

/// <summary>
/// Nhóm hàng cho test tạo mặt hàng: mặt hàng chỉ gắn vào nhóm cấp 4 – Chủng loại (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-5),
/// nên test cần nhánh đủ 4 cấp thay cho một nhóm gốc.
/// </summary>
internal static class TestCategories
{
    /// <summary>
    /// Tạo ba nhóm cấp 1–3 rồi gửi yêu cầu tạo nhóm cấp 4 theo <paramref name="request"/> (nhóm cha đặt là nhóm cấp 3). Trả về response của
    /// lần tạo nhóm cấp 4 để test đọc như khi gọi thẳng <c>POST /api/item-categories</c>.
    /// </summary>
    /// <param name="client">Client đã đăng nhập ở tổng công ty, có quyền quản lý nhóm hàng.</param>
    /// <param name="request">Nhóm cấp 4 cần tạo (mã, tên, tài khoản kho); nhóm cha bỏ qua.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Response tạo nhóm cấp 4.</returns>
    /// <exception cref="HttpRequestException">Khi không tạo được nhóm cấp 1–3.</exception>
    public static Task<HttpResponseMessage> PostLeafAsync(HttpClient client, CreateItemCategoryRequest request, CancellationToken ct) =>
        PostAtLevelAsync(client, request, 4, ct);

    /// <summary>
    /// Tạo các nhóm phía trên rồi gửi yêu cầu tạo nhóm theo <paramref name="request"/> ở đúng cấp <paramref name="level"/> – dùng khi test cần
    /// nhóm cha (cấp 3) có nhóm con cấp 4 gắn mặt hàng.
    /// </summary>
    /// <param name="client">Client đã đăng nhập ở tổng công ty, có quyền quản lý nhóm hàng.</param>
    /// <param name="request">Nhóm cần tạo; nhóm cha bỏ qua.</param>
    /// <param name="level">Cấp của nhóm cần tạo, 1–4.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Response tạo nhóm.</returns>
    /// <exception cref="HttpRequestException">Khi không tạo được nhóm phía trên.</exception>
    public static async Task<HttpResponseMessage> PostAtLevelAsync(HttpClient client, CreateItemCategoryRequest request, int level, CancellationToken ct)
    {
        Guid? parentId = null;
        for (var current = 1; current < level; current++)
        {
            var response = await client.PostAsJsonAsync("/api/item-categories",
                new CreateItemCategoryRequest(TestCodes.Category(), $"{request.Name} – cấp {current}", parentId, null), ct);
            response.EnsureSuccessStatusCode();
            parentId = (await response.Content.ReadFromJsonAsync<ItemCategoryDetailDto>(ct))!.Id;
        }
        return await client.PostAsJsonAsync("/api/item-categories", request with { ParentId = parentId }, ct);
    }
}
