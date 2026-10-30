using FluentValidation;
using Proc.Domain.Inventory;

namespace Proc.Application.Inventory;

/// <summary>Kiểm tra tạo lịch kỳ năm.</summary>
public sealed class CreateInventoryYearRequestValidator : AbstractValidator<CreateInventoryYearRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public CreateInventoryYearRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(InventoryPeriod.MinYear, InventoryPeriod.MaxYear)
            .WithMessage($"Năm kỳ kho phải từ {InventoryPeriod.MinYear} đến {InventoryPeriod.MaxYear}.");
    }
}
