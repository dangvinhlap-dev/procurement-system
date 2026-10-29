using Proc.Api.Security;
using Proc.Api.Validation;
using Proc.Application.Administration;
using Proc.Domain.Organization;

namespace Proc.Api.Endpoints;

/// <summary>
/// Endpoint quản lý người dùng, vai trò và danh mục quyền (design.md mục 9.1, 9.5).
/// Mọi endpoint chạy tại công ty đang làm việc (header X-Company-Id); quy tắc chống tự nâng quyền nằm ở dịch vụ.
/// Kiểm tra quyền chạy trước kiểm tra dữ liệu, để người không có quyền không dò được quy tắc dữ liệu.
/// </summary>
public static class AdministrationEndpoints
{
    /// <summary>
    /// Khai báo các endpoint.
    /// </summary>
    /// <param name="app">Bộ định tuyến endpoint.</param>
    /// <returns>Chính <paramref name="app"/>.</returns>
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Người dùng");
        users.MapGet("/", (IUserAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.UserView);
        users.MapGet("/access-options", (IUserAdministration s, CancellationToken ct) => s.GetAccessOptionsAsync(ct))
            .RequirePermission(Permissions.System.UserManage);
        users.MapGet("/{id:guid}", (Guid id, IUserAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.UserView);
        users.MapPost("/", async (CreateUserRequest request, IUserAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/users/{created.Id}", created);
            })
            .RequirePermission(Permissions.System.UserManage)
            .Validate<CreateUserRequest>();
        users.MapPut("/{id:guid}", (Guid id, UpdateUserRequest request, IUserAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.System.UserManage)
            .Validate<UpdateUserRequest>();
        users.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, IUserAdministration s, CancellationToken ct) =>
            {
                await s.ResetPasswordAsync(id, request, ct);
                return Results.NoContent();
            })
            .RequirePermission(Permissions.System.UserManage)
            .Validate<ResetPasswordRequest>();
        users.MapPut("/{id:guid}/access", (Guid id, UserAccessDto access, IUserAdministration s, CancellationToken ct) =>
                s.SetAccessAsync(id, access, ct))
            .RequirePermission(Permissions.System.UserManage)
            .Validate<UserAccessDto>();

        var roles = app.MapGroup("/api/roles").WithTags("Vai trò");
        roles.MapGet("/", (IRoleAdministration s, CancellationToken ct) => s.ListAsync(ct))
            .RequirePermission(Permissions.System.RoleView);
        roles.MapGet("/{id:guid}", (Guid id, IRoleAdministration s, CancellationToken ct) => s.GetAsync(id, ct))
            .RequirePermission(Permissions.System.RoleView);
        roles.MapPost("/", async (CreateRoleRequest request, IRoleAdministration s, CancellationToken ct) =>
            {
                var created = await s.CreateAsync(request, ct);
                return Results.Created($"/api/roles/{created.Id}", created);
            })
            .RequirePermission(Permissions.System.RoleManage)
            .Validate<CreateRoleRequest>();
        roles.MapPut("/{id:guid}", (Guid id, UpdateRoleRequest request, IRoleAdministration s, CancellationToken ct) =>
                s.UpdateAsync(id, request, ct))
            .RequirePermission(Permissions.System.RoleManage)
            .Validate<UpdateRoleRequest>();

        app.MapGet("/api/permissions", (IRoleAdministration s) => s.GetPermissionCatalog())
            .WithTags("Vai trò")
            .RequirePermission(Permissions.System.RoleView);

        return app;
    }
}
