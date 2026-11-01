using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Analysis;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng PurchaseHistoryBatch – lô nhập dữ liệu mua lịch sử (design.md mục 18.1).</summary>
internal sealed class PurchaseHistoryBatchConfiguration : IEntityTypeConfiguration<PurchaseHistoryBatch>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseHistoryBatch> builder)
    {
        builder.ToTable("PurchaseHistoryBatch");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FileName).HasMaxLength(PurchaseHistoryBatch.FileNameMaxLength).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseHistoryRecord – dòng dữ liệu mua lịch sử (design.md mục 18.1).</summary>
internal sealed class PurchaseHistoryRecordConfiguration : IEntityTypeConfiguration<PurchaseHistoryRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseHistoryRecord> builder)
    {
        builder.ToTable("PurchaseHistoryRecord");
        builder.Property(x => x.Id).ValueGeneratedNever();
        // Độ dài lấy từ domain để cột CSDL và kiểm tra dòng không lệch nhau.
        foreach (var (field, length) in PurchaseHistoryRecord.MaxLengths) builder.Property(field).HasMaxLength(length);
        builder.Property(x => x.ItemCode).IsRequired();
        builder.Property(x => x.ItemName).IsRequired();
        builder.Property(x => x.Uom).IsRequired();
        builder.Property(x => x.SupplierName).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.SearchText).HasMaxLength(PurchaseHistoryRecord.SearchTextMaxLength).IsRequired();
        // Kiểu số theo CLAUDE.md mục 4: SL numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2).
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.RelatedCost).HasPrecision(19, 2);
        builder.Property(x => x.VatPercent).HasPrecision(5, 2);
        // Truy vấn chính: dữ liệu của công ty theo ngày mua; lọc theo lô; xóa lô.
        builder.HasIndex(x => new { x.CompanyId, x.PurchaseDate });
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => x.ItemId);
        builder.HasIndex(x => x.SupplierId);
        // Ánh xạ (mục 18.2) gán lại mặt hàng/NCC cho mọi dòng cùng mã cũ, cùng MST của pháp nhân.
        builder.HasIndex(x => new { x.CompanyId, x.ItemCode });
        builder.HasIndex(x => new { x.CompanyId, x.SupplierTaxCode });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Xóa lô thì xóa mọi dòng của lô (lô nhập nhầm); dịch vụ vẫn xóa dòng trước bằng một câu DELETE để không phụ thuộc cascade.
        builder.HasOne<PurchaseHistoryBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        // Mặt hàng, NCC chỉ là kết quả khớp; danh mục không xóa được khi đã dùng.
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseHistoryItemMapping – ánh xạ mã vật tư cũ → mã hàng chung (design.md mục 18.2).</summary>
internal sealed class PurchaseHistoryItemMappingConfiguration : IEntityTypeConfiguration<PurchaseHistoryItemMapping>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseHistoryItemMapping> builder)
    {
        builder.ToTable("PurchaseHistoryItemMapping");
        builder.Property(x => x.SourceCode).HasMaxLength(PurchaseHistoryRecord.MaxLengths[nameof(PurchaseHistoryRecord.ItemCode)]).IsRequired();
        // Mỗi mã cũ của một pháp nhân chỉ trỏ tới một mã chung.
        builder.HasIndex(x => new { x.CompanyId, x.SourceCode }).IsUnique();
        builder.HasIndex(x => x.ItemId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseHistorySupplierMapping – ánh xạ NCC cũ → NCC trong danh mục (design.md mục 18.2).</summary>
internal sealed class PurchaseHistorySupplierMappingConfiguration : IEntityTypeConfiguration<PurchaseHistorySupplierMapping>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseHistorySupplierMapping> builder)
    {
        builder.ToTable("PurchaseHistorySupplierMapping");
        builder.Property(x => x.SourceKey).HasMaxLength(PurchaseHistoryKeys.SupplierKeyMaxLength).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.SourceKey }).IsUnique();
        builder.HasIndex(x => x.SupplierId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
