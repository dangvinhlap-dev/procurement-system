using FluentValidation;

namespace Proc.Api.Validation;

/// <summary>
/// Kiểm tra dữ liệu gửi lên bằng FluentValidation trước khi vào endpoint; sai thì trả 400 kèm lỗi tiếng Việt từng trường,
/// tiêu đề là lỗi đầu tiên để giao diện hiện ngay.
/// </summary>
public static class ValidationEndpointExtensions
{
    /// <summary>
    /// Gắn bước kiểm tra cho tham số kiểu <typeparamref name="TRequest"/> của endpoint.
    /// </summary>
    /// <typeparam name="TRequest">Kiểu dữ liệu gửi lên; phải có validator đăng ký trong DI.</typeparam>
    /// <param name="builder">Endpoint.</param>
    /// <returns>Chính builder để gọi nối tiếp.</returns>
    public static RouteHandlerBuilder Validate<TRequest>(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
            if (request is null)
                return Results.Problem(title: "Thiếu dữ liệu gửi lên.", statusCode: StatusCodes.Status400BadRequest);

            var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary(), title: result.Errors[0].ErrorMessage);

            return await next(context);
        });
        return builder;
    }
}
