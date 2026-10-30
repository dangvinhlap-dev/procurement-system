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

/// <summary>Kiểm tra lưu phiếu nhập mua NCC: độ dài khớp cột; quy tắc nghiệp vụ dòng do Domain kiểm tra.</summary>
public sealed class SaveReceiptRequestValidator : AbstractValidator<SaveReceiptRequest>
{
    /// <summary>Số dòng tối đa mỗi phiếu – phiếu dài hơn nên tách để in, đối chiếu được.</summary>
    public const int MaxLines = 500;

    /// <summary>Khai báo các quy tắc.</summary>
    public SaveReceiptRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Hãy chọn kho nhập.");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Hãy chọn nhà cung cấp.");
        RuleFor(x => x.InvoiceNo).MaximumLength(50);
        RuleFor(x => x.InvoiceSeries).MaximumLength(20);
        RuleFor(x => x.DeliveryNoteNo).MaximumLength(50);
        RuleFor(x => x.Deliverer).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= MaxLines).WithMessage($"Mỗi phiếu tối đa {MaxLines} dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Dòng hàng chưa chọn ĐVT.");
            l.RuleFor(x => x.LotNo).MaximumLength(50);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra lưu phiếu xuất trả NCC.</summary>
public sealed class SaveReturnRequestValidator : AbstractValidator<SaveReturnRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveReturnRequestValidator()
    {
        RuleFor(x => x.SourceDocumentId).NotEmpty().WithMessage("Hãy chọn phiếu nhập gốc.");
        RuleFor(x => x.InvoiceNo).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= SaveReceiptRequestValidator.MaxLines).WithMessage($"Mỗi phiếu tối đa {SaveReceiptRequestValidator.MaxLines} dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.SourceLineId).NotEmpty().WithMessage("Dòng hàng chưa chọn dòng của phiếu nhập gốc.");
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra lưu phiếu chuyển kho.</summary>
public sealed class SaveTransferRequestValidator : AbstractValidator<SaveTransferRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveTransferRequestValidator()
    {
        RuleFor(x => x.FromWarehouseId).NotEmpty().WithMessage("Hãy chọn kho xuất.");
        RuleFor(x => x.ToWarehouseId).NotEmpty().WithMessage("Hãy chọn kho nhập.");
        RuleFor(x => x.Deliverer).MaximumLength(200);
        RuleFor(x => x.Receiver).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= SaveReceiptRequestValidator.MaxLines).WithMessage($"Mỗi phiếu tối đa {SaveReceiptRequestValidator.MaxLines} dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Dòng hàng chưa chọn ĐVT.");
            l.RuleFor(x => x.LotNo).MaximumLength(50);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra lưu phiếu xuất bán nội bộ.</summary>
public sealed class SaveSaleRequestValidator : AbstractValidator<SaveSaleRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public SaveSaleRequestValidator()
    {
        RuleFor(x => x.FromWarehouseId).NotEmpty().WithMessage("Hãy chọn kho xuất.");
        RuleFor(x => x.BuyerCompanyId).NotEmpty().WithMessage("Hãy chọn công ty mua.");
        RuleFor(x => x.InvoiceNo).MaximumLength(50);
        RuleFor(x => x.Deliverer).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.")
            .Must(l => l.Count <= SaveReceiptRequestValidator.MaxLines).WithMessage($"Mỗi phiếu tối đa {SaveReceiptRequestValidator.MaxLines} dòng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty().WithMessage("Dòng hàng chưa chọn mặt hàng.");
            l.RuleFor(x => x.UomId).NotEmpty().WithMessage("Dòng hàng chưa chọn ĐVT.");
            l.RuleFor(x => x.LotNo).MaximumLength(50);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

/// <summary>Kiểm tra xác nhận phiếu nhập mua nội bộ.</summary>
public sealed class ConfirmIntercompanyReceiptRequestValidator : AbstractValidator<ConfirmIntercompanyReceiptRequest>
{
    /// <summary>Khai báo các quy tắc.</summary>
    public ConfirmIntercompanyReceiptRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phiếu phải có ít nhất một dòng hàng.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.LineId).NotEmpty();
            l.RuleFor(x => x.ReceivedQty).GreaterThanOrEqualTo(0).WithMessage("SL thực nhận không được âm.");
            l.RuleFor(x => x.LotNo).MaximumLength(50);
            l.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}
