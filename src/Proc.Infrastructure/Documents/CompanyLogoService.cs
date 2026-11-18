using Microsoft.EntityFrameworkCore;
using Proc.Application.Common;
using Proc.Application.Documents;
using Proc.Domain.Organization;
using Proc.Infrastructure.Persistence;

namespace Proc.Infrastructure.Documents;

/// <summary>
/// Logo công ty in đầu phiếu (lát IN1 – design.md mục 20.4). Quyền kiểm ở endpoint (xem: <c>System.Company.View</c>; tải / xóa:
/// <c>System.Company.Manage</c>) như danh mục công ty – danh mục công ty dùng chung nên không lọc theo công ty đang làm việc.
/// </summary>
public sealed class CompanyLogoService : ICompanyLogoService
{
    private readonly ProcDbContext _db;

    /// <summary>Tạo dịch vụ.</summary>
    /// <param name="db">CSDL.</param>
    public CompanyLogoService(ProcDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<CompanyLogoDto> GetAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var logo = await _db.CompanyLogos.AsNoTracking().Where(l => l.CompanyId == companyId)
            .Select(l => new { l.ContentType, l.Width, l.Height, At = l.UpdatedAt ?? l.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        return logo is null
            ? new CompanyLogoDto(companyId, false, null, 0, 0, null)
            : new CompanyLogoDto(companyId, true, logo.ContentType, logo.Width, logo.Height, logo.At);
    }

    /// <inheritdoc />
    public async Task<(byte[] Content, string ContentType)> GetImageAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var logo = await _db.CompanyLogos.AsNoTracking().Where(l => l.CompanyId == companyId)
                       .Select(l => new { l.Image, l.ContentType }).SingleOrDefaultAsync(cancellationToken)
                   ?? throw new NotFoundException("Công ty chưa có logo.");
        return (logo.Image, logo.ContentType);
    }

    /// <inheritdoc />
    public async Task<CompanyLogoDto> UploadAsync(Guid companyId, byte[] image, CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var logo = await _db.CompanyLogos.SingleOrDefaultAsync(l => l.CompanyId == companyId, cancellationToken);
        if (logo is null) _db.CompanyLogos.Add(CompanyLogo.Create(companyId, image));
        else logo.Replace(image);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(companyId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var logo = await _db.CompanyLogos.SingleOrDefaultAsync(l => l.CompanyId == companyId, cancellationToken);
        if (logo is null) return;
        _db.CompanyLogos.Remove(logo);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await _db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken)) throw new NotFoundException("Không tìm thấy công ty.");
    }
}
