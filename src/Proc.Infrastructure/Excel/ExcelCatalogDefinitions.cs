using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proc.Application.Excel;
using Proc.Application.MasterData;
using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Excel;

/// <summary>
/// Một danh mục nhập/xuất được bằng Excel. Nhập từng dòng qua đúng dịch vụ của màn hình (cùng quy tắc nghiệp vụ, phạm vi,
/// "chỉ ở tổng công ty"), kiểm tra bằng đúng validator của API.
/// </summary>
internal abstract class ExcelCatalog
{
    /// <summary>Mã danh mục trên đường dẫn API.</summary>
    public abstract string Key { get; }

    /// <summary>Tên sheet dữ liệu.</summary>
    public abstract string SheetName { get; }

    /// <summary>Tên file khi tải về (không đuôi).</summary>
    public abstract string FileName { get; }

    /// <summary>Quyền cần để xuất.</summary>
    public abstract string ViewPermission { get; }

    /// <summary>Quyền cần để nhập.</summary>
    public abstract string ManagePermission { get; }

    /// <summary>Các cột theo thứ tự trên file.</summary>
    public abstract IReadOnlyList<ExcelColumn> Columns { get; }

    /// <summary>Hướng dẫn riêng của danh mục (ngoài hướng dẫn chung).</summary>
    public virtual IReadOnlyList<string> Guide => [];

    /// <summary>Các dòng xuất ra (trong phạm vi người dùng), cùng thứ tự <see cref="Columns"/>.</summary>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Các dòng.</returns>
    public abstract Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken);

    /// <summary>Mã bản ghi của dòng (để báo lỗi), null nếu ô mã trống.</summary>
    /// <param name="row">Dòng.</param>
    /// <returns>Mã.</returns>
    public abstract string? CodeOf(ExcelRowReader row);

    /// <summary>Thêm hoặc cập nhật bản ghi của một dòng theo mã.</summary>
    /// <param name="row">Dòng.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>true nếu thêm mới, false nếu cập nhật.</returns>
    public abstract Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken);

    /// <summary>
    /// Kiểm tra yêu cầu bằng validator đã đăng ký cho API (độ dài, email…), báo lỗi đầu tiên.
    /// </summary>
    /// <typeparam name="T">Kiểu yêu cầu.</typeparam>
    /// <param name="services">Service provider của request.</param>
    /// <param name="request">Yêu cầu.</param>
    /// <param name="cancellationToken">Hủy thao tác.</param>
    /// <returns>Task hoàn tất khi hợp lệ.</returns>
    /// <exception cref="ExcelRowException">Khi không hợp lệ.</exception>
    protected static async Task ValidateAsync<T>(IServiceProvider services, T request, CancellationToken cancellationToken)
    {
        var validator = services.GetService<IValidator<T>>();
        if (validator is null) return;
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid) throw new ExcelRowException(result.Errors[0].ErrorMessage);
    }

    /// <summary>Đọc "Còn dùng" (trống = Có).</summary>
    /// <param name="row">Dòng.</param>
    /// <returns>Còn dùng.</returns>
    protected static bool Active(ExcelRowReader row) => row.Bool(CommonHeaders.Active) ?? true;
}

/// <summary>Tiêu đề cột dùng chung.</summary>
internal static class CommonHeaders
{
    /// <summary>Cột Còn dùng.</summary>
    public const string Active = "Còn dùng";

    /// <summary>Mô tả cột Còn dùng.</summary>
    public const string ActiveNote = "Có / Không; để trống = Có.";
}

/// <summary>Đơn vị tính (design.md mục 5.2).</summary>
internal sealed class UomExcelCatalog(ProcDbContext db, IUomAdministration uoms, IServiceProvider services) : ExcelCatalog
{
    private const string Code = "Mã ĐVT";
    private const string Name = "Tên ĐVT";

    /// <inheritdoc />
    public override string Key => ExcelCatalogs.Uoms;

    /// <inheritdoc />
    public override string SheetName => "Đơn vị tính";

    /// <inheritdoc />
    public override string FileName => "don-vi-tinh";

    /// <inheritdoc />
    public override string ViewPermission => Permissions.MasterData.ItemView;

    /// <inheritdoc />
    public override string ManagePermission => Permissions.MasterData.ItemManage;

    /// <inheritdoc />
    public override IReadOnlyList<ExcelColumn> Columns { get; } =
    [
        new(Code, "1–10 chữ không dấu hoặc số, ví dụ THUNG. Đã có thì cập nhật tên.", Required: true, Text: true),
        new(Name, "Tên hiển thị trên chứng từ, ví dụ Thùng.", Required: true),
        new(CommonHeaders.Active, CommonHeaders.ActiveNote),
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken) =>
        (await uoms.ListAsync(cancellationToken)).Select(u => (IReadOnlyList<object?>)[u.Code, u.Name, ExcelHeaders.YesNo(u.IsActive)]).ToList();

    /// <inheritdoc />
    public override string? CodeOf(ExcelRowReader row) => row.Text(Code)?.ToUpperInvariant();

    /// <inheritdoc />
    public override async Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var code = CodeRules.Normalize(row.RequiredText(Code));
        var name = row.RequiredText(Name);
        var active = Active(row);
        var id = await db.Uoms.Where(u => u.Code == code).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            var create = new CreateUomRequest(code, name);
            await ValidateAsync(services, create, cancellationToken);
            var created = await uoms.CreateAsync(create, cancellationToken);
            if (!active) await uoms.UpdateAsync(created.Id, new UpdateUomRequest(name, false), cancellationToken);
            return true;
        }

        var update = new UpdateUomRequest(name, active);
        await ValidateAsync(services, update, cancellationToken);
        await uoms.UpdateAsync(id.Value, update, cancellationToken);
        return false;
    }
}

/// <summary>Nhóm hàng (design.md mục 5.2, 5.7). Nhóm cha phải đứng trên nhóm con trong file (file xuất ra đã theo thứ tự cây).</summary>
internal sealed class ItemCategoryExcelCatalog(ProcDbContext db, IItemCategoryAdministration categories, IServiceProvider services) : ExcelCatalog
{
    private const string Code = "Mã nhóm";
    private const string Name = "Tên nhóm";
    private const string Parent = "Mã nhóm cha";
    private const string Account = "TK kho";

    /// <inheritdoc />
    public override string Key => ExcelCatalogs.ItemCategories;

    /// <inheritdoc />
    public override string SheetName => "Nhóm hàng";

    /// <inheritdoc />
    public override string FileName => "nhom-hang";

    /// <inheritdoc />
    public override string ViewPermission => Permissions.MasterData.ItemCategoryView;

    /// <inheritdoc />
    public override string ManagePermission => Permissions.MasterData.ItemCategoryManage;

    /// <inheritdoc />
    public override IReadOnlyList<ExcelColumn> Columns { get; } =
    [
        new(Code, "2–20 chữ không dấu hoặc số.", Required: true, Text: true),
        new(Name, "Tên nhóm hàng.", Required: true),
        new(Parent, "Để trống là nhóm gốc. Nhóm cha phải có sẵn hoặc nằm ở dòng phía trên.", Text: true),
        new(Account, "Tài khoản kho mặc định, 3–10 chữ số, ví dụ 152.", Text: true),
        new(CommonHeaders.Active, CommonHeaders.ActiveNote),
    ];

    /// <inheritdoc />
    public override IReadOnlyList<string> Guide => ["Nhóm cha phải đứng trên nhóm con. File xuất ra đã xếp theo cây nên sửa rồi nhập lại được ngay."];

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken) =>
        (await categories.ListAsync(cancellationToken))
            .Select(c => (IReadOnlyList<object?>)[c.Code, c.Name, c.ParentCode, c.InventoryAccount, ExcelHeaders.YesNo(c.IsActive)])
            .ToList();

    /// <inheritdoc />
    public override string? CodeOf(ExcelRowReader row) => row.Text(Code)?.ToUpperInvariant();

    /// <inheritdoc />
    public override async Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var code = CodeRules.Normalize(row.RequiredText(Code));
        var name = row.RequiredText(Name);
        var parentCode = row.Text(Parent) is { } p ? CodeRules.Normalize(p) : null;
        Guid? parentId = null;
        if (parentCode is not null)
        {
            parentId = await db.ItemCategories.Where(c => c.Code == parentCode).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ExcelRowException($"Không có nhóm cha {parentCode} (hoặc nằm ngoài phạm vi của bạn, hoặc ở dòng phía dưới).");
        }

        var account = row.Text(Account);
        var active = Active(row);
        var id = await db.ItemCategories.Where(c => c.Code == code).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            var create = new CreateItemCategoryRequest(code, name, parentId, account);
            await ValidateAsync(services, create, cancellationToken);
            var created = await categories.CreateAsync(create, cancellationToken);
            if (!active) await categories.UpdateAsync(created.Id, new UpdateItemCategoryRequest(name, parentId, account, false), cancellationToken);
            return true;
        }

        var update = new UpdateItemCategoryRequest(name, parentId, account, active);
        await ValidateAsync(services, update, cancellationToken);
        await categories.UpdateAsync(id.Value, update, cancellationToken);
        return false;
    }
}

/// <summary>Hàng hóa (design.md mục 5.2, 5.7).</summary>
internal sealed class ItemExcelCatalog(ProcDbContext db, IItemAdministration items, IServiceProvider services) : ExcelCatalog
{
    private const string Code = "Mã hàng";
    private const string Name = "Tên hàng";
    private const string EnglishName = "Tên tiếng Anh";
    private const string Type = "Loại hàng";
    private const string Category = "Mã nhóm hàng";
    private const string BaseUom = "ĐVT cơ sở";
    private const string Specification = "Quy cách";
    private const string Brand = "Thương hiệu";
    private const string Origin = "Xuất xứ";
    private const string Barcode = "Mã vạch";
    private const string Vat = "Thuế suất";
    private const string TrackLot = "Quản lý lô";
    private const string TrackExpiry = "Quản lý hạn dùng";
    private const string MinStock = "Tồn tối thiểu";
    private const string MaxStock = "Tồn tối đa";
    private const string Uom1 = "ĐVT quy đổi 1";
    private const string Factor1 = "Hệ số 1";
    private const string Uom2 = "ĐVT quy đổi 2";
    private const string Factor2 = "Hệ số 2";
    private const string Uom3 = "ĐVT quy đổi 3";
    private const string Factor3 = "Hệ số 3";

    /// <summary>Tên loại hàng trên file ↔ giá trị API.</summary>
    private static readonly Dictionary<string, string> TypeLabels = new()
    {
        ["Inventory"] = "Hàng tồn kho",
        ["Service"] = "Dịch vụ",
        ["Asset"] = "Tài sản",
        ["Consumable"] = "Vật tư tiêu hao",
    };

    private static readonly (string Uom, string Factor)[] ConversionColumns = [(Uom1, Factor1), (Uom2, Factor2), (Uom3, Factor3)];

    /// <inheritdoc />
    public override string Key => ExcelCatalogs.Items;

    /// <inheritdoc />
    public override string SheetName => "Hàng hóa";

    /// <inheritdoc />
    public override string FileName => "hang-hoa";

    /// <inheritdoc />
    public override string ViewPermission => Permissions.MasterData.ItemView;

    /// <inheritdoc />
    public override string ManagePermission => Permissions.MasterData.ItemManage;

    /// <inheritdoc />
    public override IReadOnlyList<ExcelColumn> Columns { get; } =
    [
        new(Code, "2–30 ký tự chữ không dấu, số, - . _ /. Đã có thì cập nhật.", Required: true, Text: true),
        new(Name, "Tên hàng.", Required: true),
        new(EnglishName, ""),
        new(Type, "Hàng tồn kho / Dịch vụ / Tài sản / Vật tư tiêu hao.", Required: true),
        new(Category, "Mã nhóm hàng (đang dùng).", Required: true, Text: true),
        new(BaseUom, "Mã ĐVT cơ sở, ví dụ CAI – đơn vị lưu tồn kho.", Required: true, Text: true),
        new(Specification, ""),
        new(Brand, ""),
        new(Origin, ""),
        new(Barcode, "", Text: true),
        new(Vat, "KCT, 0, 5, 8 hoặc 10 (%).", Text: true),
        new(TrackLot, "Có / Không – chỉ hàng tồn kho."),
        new(TrackExpiry, "Có / Không – chỉ hàng tồn kho."),
        new(MinStock, "Theo ĐVT cơ sở, tối đa 4 chữ số thập phân – chỉ hàng tồn kho."),
        new(MaxStock, "Theo ĐVT cơ sở, ≥ tồn tối thiểu – chỉ hàng tồn kho."),
        new(Uom1, "Mã ĐVT quy đổi, ví dụ THUNG.", Text: true),
        new(Factor1, "Số ĐVT cơ sở trong 1 ĐVT quy đổi, ví dụ 24 (1 thùng = 24 cái)."),
        new(Uom2, "", Text: true),
        new(Factor2, ""),
        new(Uom3, "", Text: true),
        new(Factor3, ""),
        new(CommonHeaders.Active, CommonHeaders.ActiveNote),
    ];

    /// <inheritdoc />
    public override IReadOnlyList<string> Guide =>
    [
        "Quy đổi ĐVT: tối đa 3 ĐVT trên file. Khi cập nhật, để trống cả 3 cột ĐVT quy đổi thì giữ nguyên quy đổi đang có; ghi bất kỳ cột nào thì thay toàn bộ quy đổi theo file.",
        "Thông số kỹ thuật không có trên file, sửa ở màn hình Hàng hóa (nhập Excel không xóa thông số đang có).",
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken)
    {
        var categories = await db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var uomCodes = await db.Uoms.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Code, cancellationToken);
        var list = await db.Items.AsNoTracking().OrderBy(i => i.Code).ToListAsync(cancellationToken);
        return list.Select(i =>
        {
            var conversions = i.Conversions.OrderBy(c => c.Factor).ToList();
            object? ConvUom(int n) => n < conversions.Count ? uomCodes[conversions[n].UomId] : null;
            object? ConvFactor(int n) => n < conversions.Count ? conversions[n].Factor : null;
            return (IReadOnlyList<object?>)
            [
                i.Code, i.Name, i.EnglishName, TypeLabels[i.Type.ToString()], categories.GetValueOrDefault(i.CategoryId),
                uomCodes[i.BaseUomId], i.Specification, i.Brand, i.Origin, i.Barcode, i.VatCode,
                ExcelHeaders.YesNo(i.TrackLot), ExcelHeaders.YesNo(i.TrackExpiry), i.MinStock, i.MaxStock,
                ConvUom(0), ConvFactor(0), ConvUom(1), ConvFactor(1), ConvUom(2), ConvFactor(2), ExcelHeaders.YesNo(i.IsActive),
            ];
        }).ToList();
    }

    /// <inheritdoc />
    public override string? CodeOf(ExcelRowReader row) => row.Text(Code)?.ToUpperInvariant();

    /// <inheritdoc />
    public override async Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var code = CodeRules.Normalize(row.RequiredText(Code));
        var typeText = row.RequiredText(Type);
        var type = TypeLabels.FirstOrDefault(t => SearchText.Normalize(t.Value) == SearchText.Normalize(typeText) || string.Equals(t.Key, typeText, StringComparison.OrdinalIgnoreCase)).Key
            ?? throw new ExcelRowException($"Loại hàng \"{typeText}\" không hợp lệ: ghi Hàng tồn kho, Dịch vụ, Tài sản hoặc Vật tư tiêu hao.");
        var categoryCode = CodeRules.Normalize(row.RequiredText(Category));
        var categoryId = await db.ItemCategories.Where(c => c.Code == categoryCode).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ExcelRowException($"Không có nhóm hàng {categoryCode} (hoặc nằm ngoài phạm vi của bạn).");
        var baseUomId = await UomIdAsync(row.RequiredText(BaseUom), cancellationToken);
        var vat = row.Text(Vat)?.Replace("%", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

        var conversionsGiven = ConversionColumns.Any(c => row.Text(c.Uom) is not null);
        var conversions = new List<ItemUomConversionInput>();
        foreach (var (uomHeader, factorHeader) in ConversionColumns)
        {
            if (row.Text(uomHeader) is not { } uomCode) continue;
            var factor = row.Decimal(factorHeader) ?? throw new ExcelRowException($"Thiếu \"{factorHeader}\" cho ĐVT {uomCode}.");
            conversions.Add(new ItemUomConversionInput(await UomIdAsync(uomCode, cancellationToken), factor));
        }

        var existing = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, cancellationToken);
        if (existing is null)
        {
            var create = new CreateItemRequest(
                code, row.RequiredText(Name), row.Text(EnglishName), type, categoryId, baseUomId, row.Text(Specification), row.Text(Brand),
                row.Text(Origin), row.Text(Barcode), vat, null, row.Bool(TrackLot) ?? false, row.Bool(TrackExpiry) ?? false,
                row.Decimal(MinStock), row.Decimal(MaxStock), conversions);
            await ValidateAsync(services, create, cancellationToken);
            var created = await items.CreateAsync(create, cancellationToken);
            if (!Active(row)) await items.UpdateAsync(created.Id, ToUpdate(created, conversions, false), cancellationToken);
            return true;
        }

        // Không có cột quy đổi nào thì giữ nguyên quy đổi đang có (xem hướng dẫn trên file).
        var keptConversions = conversionsGiven
            ? conversions
            : existing.Conversions.Select(c => new ItemUomConversionInput(c.UomId, c.Factor)).ToList();
        var update = new UpdateItemRequest(
            row.RequiredText(Name), row.Text(EnglishName), type, categoryId, baseUomId, row.Text(Specification), row.Text(Brand),
            row.Text(Origin), row.Text(Barcode), vat, existing.TechnicalSpecs, row.Bool(TrackLot) ?? false, row.Bool(TrackExpiry) ?? false,
            row.Decimal(MinStock), row.Decimal(MaxStock), keptConversions, Active(row));
        await ValidateAsync(services, update, cancellationToken);
        await items.UpdateAsync(existing.Id, update, cancellationToken);
        return false;
    }

    private static UpdateItemRequest ToUpdate(ItemDetailDto d, IReadOnlyList<ItemUomConversionInput> conversions, bool isActive) =>
        new(d.Name, d.EnglishName, d.Type, d.CategoryId, d.BaseUomId, d.Specification, d.Brand, d.Origin, d.Barcode, d.VatCode,
            d.TechnicalSpecs, d.TrackLot, d.TrackExpiry, d.MinStock, d.MaxStock, conversions, isActive);

    private async Task<Guid> UomIdAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = CodeRules.Normalize(code);
        return await db.Uoms.Where(u => u.Code == normalized).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ExcelRowException($"Không có đơn vị tính {normalized}.");
    }
}

/// <summary>Nhóm NCC (design.md mục 5.3, 5.8).</summary>
internal sealed class SupplierGroupExcelCatalog(ProcDbContext db, ISupplierGroupAdministration groups, IServiceProvider services) : ExcelCatalog
{
    private const string Code = "Mã nhóm NCC";
    private const string Name = "Tên nhóm NCC";

    /// <inheritdoc />
    public override string Key => ExcelCatalogs.SupplierGroups;

    /// <inheritdoc />
    public override string SheetName => "Nhóm NCC";

    /// <inheritdoc />
    public override string FileName => "nhom-ncc";

    /// <inheritdoc />
    public override string ViewPermission => Permissions.MasterData.SupplierView;

    /// <inheritdoc />
    public override string ManagePermission => Permissions.MasterData.SupplierManage;

    /// <inheritdoc />
    public override IReadOnlyList<ExcelColumn> Columns { get; } =
    [
        new(Code, "2–20 chữ không dấu hoặc số.", Required: true, Text: true),
        new(Name, "", Required: true),
        new(CommonHeaders.Active, CommonHeaders.ActiveNote),
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken) =>
        (await groups.ListAsync(cancellationToken)).Select(g => (IReadOnlyList<object?>)[g.Code, g.Name, ExcelHeaders.YesNo(g.IsActive)]).ToList();

    /// <inheritdoc />
    public override string? CodeOf(ExcelRowReader row) => row.Text(Code)?.ToUpperInvariant();

    /// <inheritdoc />
    public override async Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var code = CodeRules.Normalize(row.RequiredText(Code));
        var name = row.RequiredText(Name);
        var active = Active(row);
        var id = await db.SupplierGroups.Where(g => g.Code == code).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
        {
            var create = new CreateSupplierGroupRequest(code, name);
            await ValidateAsync(services, create, cancellationToken);
            var created = await groups.CreateAsync(create, cancellationToken);
            if (!active) await groups.UpdateAsync(created.Id, new UpdateSupplierGroupRequest(name, false), cancellationToken);
            return true;
        }

        await groups.UpdateAsync(id.Value, new UpdateSupplierGroupRequest(name, active), cancellationToken);
        return false;
    }
}

/// <summary>Nhà cung cấp (design.md mục 5.3, 5.8).</summary>
internal sealed class SupplierExcelCatalog(ProcDbContext db, ISupplierAdministration suppliers, IServiceProvider services) : ExcelCatalog
{
    private const string Code = "Mã NCC";
    private const string Name = "Tên NCC";
    private const string ShortName = "Tên viết tắt";
    private const string Group = "Mã nhóm NCC";
    private const string Foreign = "NCC nước ngoài";
    private const string TaxCode = "Mã số thuế";
    private const string Address = "Địa chỉ";
    private const string Phone = "Điện thoại";
    private const string Email = "Email";
    private const string Website = "Website";
    private const string PaymentTerms = "Điều khoản thanh toán";
    private const string SupplyScope = "Ngành hàng";
    private const string Rating = "Xếp hạng";
    private const string Status = "Trạng thái";
    private const string StatusNote = "Lý do";
    private const string InternalCompany = "Mã công ty nội bộ";
    private const string ContactName = "Người liên hệ chính";
    private const string ContactPosition = "Chức vụ";
    private const string ContactPhone = "ĐT người liên hệ";
    private const string ContactEmail = "Email người liên hệ";
    private const string BankName = "Ngân hàng";
    private const string BankBranch = "Chi nhánh";
    private const string AccountNumber = "Số tài khoản";
    private const string AccountName = "Chủ tài khoản";

    /// <summary>Tên trạng thái trên file ↔ giá trị API.</summary>
    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["Potential"] = "Tiềm năng",
        ["Approved"] = "Đã duyệt",
        ["Suspended"] = "Tạm ngưng",
        ["Blacklisted"] = "Danh sách đen",
    };

    /// <inheritdoc />
    public override string Key => ExcelCatalogs.Suppliers;

    /// <inheritdoc />
    public override string SheetName => "Nhà cung cấp";

    /// <inheritdoc />
    public override string FileName => "nha-cung-cap";

    /// <inheritdoc />
    public override string ViewPermission => Permissions.MasterData.SupplierView;

    /// <inheritdoc />
    public override string ManagePermission => Permissions.MasterData.SupplierManage;

    /// <inheritdoc />
    public override IReadOnlyList<ExcelColumn> Columns { get; } =
    [
        new(Code, "2–30 ký tự chữ không dấu, số, - . _ /. Đã có thì cập nhật.", Required: true, Text: true),
        new(Name, "", Required: true),
        new(ShortName, ""),
        new(Group, "Mã nhóm NCC, ví dụ TN.", Required: true, Text: true),
        new(Foreign, "Có / Không; để trống = Không."),
        new(TaxCode, "Trong nước: 10 hoặc 10-3 chữ số. Không trùng NCC khác.", Text: true),
        new(Address, ""),
        new(Phone, "", Text: true),
        new(Email, ""),
        new(Website, ""),
        new(PaymentTerms, ""),
        new(SupplyScope, ""),
        new(Rating, "1–5."),
        new(Status, "Tiềm năng / Đã duyệt / Tạm ngưng / Danh sách đen; để trống = Tiềm năng (khi thêm) hoặc giữ nguyên (khi cập nhật)."),
        new(StatusNote, "Bắt buộc khi Tạm ngưng hoặc Danh sách đen."),
        new(InternalCompany, "Mã công ty thành viên nếu là NCC nội bộ.", Text: true),
        new(ContactName, "Chỉ dùng khi thêm NCC mới."),
        new(ContactPosition, "Chỉ dùng khi thêm NCC mới."),
        new(ContactPhone, "Chỉ dùng khi thêm NCC mới.", Text: true),
        new(ContactEmail, "Chỉ dùng khi thêm NCC mới."),
        new(BankName, "Chỉ dùng khi thêm NCC mới."),
        new(BankBranch, "Chỉ dùng khi thêm NCC mới."),
        new(AccountNumber, "Chỉ dùng khi thêm NCC mới.", Text: true),
        new(AccountName, "Chỉ dùng khi thêm NCC mới."),
    ];

    /// <inheritdoc />
    public override IReadOnlyList<string> Guide =>
    [
        "Người liên hệ và tài khoản ngân hàng: file chỉ có một người liên hệ chính và một tài khoản mặc định, chỉ dùng khi thêm NCC mới. Cập nhật NCC đã có thì giữ nguyên người liên hệ, tài khoản đang có (sửa ở màn hình Nhà cung cấp).",
    ];

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<object?>>> ExportAsync(CancellationToken cancellationToken)
    {
        var groups = await db.SupplierGroups.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Code, cancellationToken);
        var companies = await db.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var list = await db.Suppliers.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);
        return list.Select(s =>
        {
            var contact = s.Contacts.FirstOrDefault(c => c.IsPrimary) ?? s.Contacts.FirstOrDefault();
            var bank = s.BankAccounts.FirstOrDefault(b => b.IsDefault) ?? s.BankAccounts.FirstOrDefault();
            return (IReadOnlyList<object?>)
            [
                s.Code, s.Name, s.ShortName, groups[s.GroupId], ExcelHeaders.YesNo(s.IsForeign), s.TaxCode, s.Address, s.Phone, s.Email,
                s.Website, s.PaymentTerms, s.SupplyScope, s.Rating, StatusLabels[s.Status.ToString()], s.StatusNote,
                s.InternalCompanyId is { } c ? companies.GetValueOrDefault(c) : null,
                contact?.Name, contact?.Position, contact?.Phone, contact?.Email,
                bank?.BankName, bank?.Branch, bank?.AccountNumber, bank?.AccountName,
            ];
        }).ToList();
    }

    /// <inheritdoc />
    public override string? CodeOf(ExcelRowReader row) => row.Text(Code)?.ToUpperInvariant();

    /// <inheritdoc />
    public override async Task<bool> ImportRowAsync(ExcelRowReader row, CancellationToken cancellationToken)
    {
        var code = CodeRules.Normalize(row.RequiredText(Code));
        var groupCode = CodeRules.Normalize(row.RequiredText(Group));
        var groupId = await db.SupplierGroups.Where(g => g.Code == groupCode).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ExcelRowException($"Không có nhóm NCC {groupCode}.");
        Guid? companyId = null;
        if (row.Text(InternalCompany) is { } companyText)
        {
            var companyCode = CodeRules.Normalize(companyText);
            companyId = await db.Companies.Where(c => c.Code == companyCode).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ExcelRowException($"Không có công ty {companyCode}.");
        }

        var statusText = row.Text(Status);
        string? status = null;
        if (statusText is not null)
        {
            status = StatusLabels.FirstOrDefault(s => SearchText.Normalize(s.Value) == SearchText.Normalize(statusText) || string.Equals(s.Key, statusText, StringComparison.OrdinalIgnoreCase)).Key
                ?? throw new ExcelRowException($"Trạng thái \"{statusText}\" không hợp lệ: ghi Tiềm năng, Đã duyệt, Tạm ngưng hoặc Danh sách đen.");
        }

        var existing = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
        IReadOnlyList<SupplierContactDto> contacts;
        IReadOnlyList<SupplierBankAccountDto> banks;
        if (existing is null)
        {
            contacts = row.Text(ContactName) is { } contactName
                ? [new(null, contactName, row.Text(ContactPosition), row.Text(ContactPhone), row.Text(ContactEmail), true)]
                : [];
            banks = row.Text(AccountNumber) is { } accountNumber
                ? [new(null, row.Text(BankName) ?? throw new ExcelRowException($"Thiếu \"{BankName}\" cho số tài khoản {accountNumber}."),
                    row.Text(BankBranch), accountNumber, row.Text(AccountName), true)]
                : [];
        }
        else
        {
            // Cập nhật: giữ nguyên người liên hệ, tài khoản đang có (xem hướng dẫn trên file).
            contacts = existing.Contacts.Select(c => new SupplierContactDto(c.Id, c.Name, c.Position, c.Phone, c.Email, c.IsPrimary)).ToList();
            banks = existing.BankAccounts.Select(b => new SupplierBankAccountDto(b.Id, b.BankName, b.Branch, b.AccountNumber, b.AccountName, b.IsDefault)).ToList();
        }

        var request = new SaveSupplierRequest(
            existing is null ? code : null, groupId, row.RequiredText(Name), row.Text(ShortName), row.Bool(Foreign) ?? false,
            row.Text(TaxCode), row.Text(Address), row.Text(Phone), row.Text(Email), row.Text(Website), row.Text(PaymentTerms),
            row.Text(SupplyScope), row.Int(Rating), status ?? existing?.Status.ToString() ?? "Potential", row.Text(StatusNote), companyId,
            contacts, banks);
        await ValidateAsync(services, request, cancellationToken);

        if (existing is null)
        {
            await suppliers.CreateAsync(request, cancellationToken);
            return true;
        }

        await suppliers.UpdateAsync(existing.Id, request, cancellationToken);
        return false;
    }
}
