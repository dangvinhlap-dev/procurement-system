using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Organization;
using Proc.Domain.Common;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Organization;

/// <summary>
/// Tham số hệ thống tại công ty đang làm việc (design.md mục 5.4). Dòng <see cref="CompanyParameter"/> đọc qua global query
/// filter (chỉ công ty đang làm việc); công ty chưa có dòng thì dùng giá trị mặc định, lần sửa đầu tiên mới tạo dòng.
/// </summary>
public sealed class SystemParameterService : ISystemParameterService
{
    private readonly ProcDbContext _db;
    private readonly ICurrentUserScope _scope;

    /// <summary>
    /// Tạo dịch vụ tham số.
    /// </summary>
    /// <param name="db">DbContext.</param>
    /// <param name="scope">Phạm vi (công ty đang làm việc).</param>
    public SystemParameterService(ProcDbContext db, ICurrentUserScope scope)
    {
        _db = db;
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SystemParameterDto>> ListAsync(CancellationToken cancellationToken)
    {
        _scope.RequireCompany();
        var parameter = await _db.CompanyParameters.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var rfqMin = parameter?.RfqMinSuppliers ?? CompanyParameter.DefaultRfqMinSuppliers;

        // Chỉ thấy kho trong phạm vi kho được gán (global query filter) – đúng với những gì người dùng sửa được ở màn hình Kho.
        var negative = await _db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive && w.AllowNegativeStock)
            .OrderBy(w => w.Code)
            .Select(w => w.Code)
            .ToListAsync(cancellationToken);

        var defaultText = CompanyParameter.DefaultRfqMinSuppliers.ToString(CultureInfo.InvariantCulture);
        return
        [
            new(SystemParameterCodes.RfqMinSuppliers, "Đấu thầu", "Số NCC tối thiểu khi gửi RFQ",
                rfqMin.ToString(CultureInfo.InvariantCulture), defaultText, rfqMin == CompanyParameter.DefaultRfqMinSuppliers, true,
                $"Phải mời đủ số NCC này mới gửi được yêu cầu báo giá (1–{CompanyParameter.MaxRfqMinSuppliers}). RFQ đã gửi không bị ảnh hưởng."),
            new(SystemParameterCodes.CostingMethod, "Kho", "Phương pháp giá vốn",
                "Bình quân gia quyền cuối kỳ tháng", null, true, false,
                "Tính theo công ty × mặt hàng khi đóng kỳ; cố định cho mọi công ty (design.md mục 7)."),
            new(SystemParameterCodes.NegativeStock, "Kho", "Kho cho phép tồn âm",
                negative.Count == 0 ? "Không kho nào" : string.Join(", ", negative), null, negative.Count == 0, false,
                "Khai báo trên từng kho ở màn hình Danh mục kho; mặc định không cho tồn âm."),
        ];
    }

    /// <inheritdoc />
    public async Task<SystemParameterDto> UpdateAsync(string code, UpdateSystemParameterRequest request, CancellationToken cancellationToken)
    {
        var companyId = _scope.RequireCompany();
        var key = code.Trim().ToUpperInvariant();
        switch (key)
        {
            case SystemParameterCodes.RfqMinSuppliers:
                int? value = null;
                if (!string.IsNullOrWhiteSpace(request.Value))
                {
                    if (!int.TryParse(request.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                        throw new BusinessRuleException("Số NCC tối thiểu phải là số nguyên.");
                    value = parsed;
                }

                var parameter = await _db.CompanyParameters.FirstOrDefaultAsync(cancellationToken);
                if (parameter is null)
                {
                    parameter = CompanyParameter.Create(companyId);
                    _db.CompanyParameters.Add(parameter);
                }

                parameter.ChangeRfqMinSuppliers(value);
                await _db.SaveChangesAsync(cancellationToken);
                break;
            case SystemParameterCodes.CostingMethod:
            case SystemParameterCodes.NegativeStock:
                throw new BusinessRuleException("Tham số này chỉ xem, không sửa ở màn hình Tham số hệ thống.");
            default:
                throw new NotFoundException($"Không có tham số {key}.");
        }

        return (await ListAsync(cancellationToken)).Single(x => x.Code == key);
    }

    /// <inheritdoc />
    public async Task<int> RfqMinSuppliersAsync(CancellationToken cancellationToken) =>
        await _db.CompanyParameters.AsNoTracking().Select(p => (int?)p.RfqMinSuppliers).FirstOrDefaultAsync(cancellationToken)
        ?? CompanyParameter.DefaultRfqMinSuppliers;
}
