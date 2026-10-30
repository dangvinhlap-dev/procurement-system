using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.Inventory;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng InventoryPeriod (design.md mục 7, 12.1).</summary>
internal sealed class InventoryPeriodConfiguration : IEntityTypeConfiguration<InventoryPeriod>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryPeriod> builder)
    {
        builder.ToTable("InventoryPeriod");
        // Mỗi công ty mỗi tháng (và mỗi kỳ năm, Month = 0) đúng một kỳ.
        builder.HasIndex(x => new { x.CompanyId, x.Year, x.Month }).IsUnique();
        builder.Ignore(x => x.IsYear);
        builder.Ignore(x => x.Label);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Tháng 0–12; kỳ tháng nằm gọn trong một tháng dương lịch.
        builder.ToTable(t => t.HasCheckConstraint("CK_InventoryPeriod_Month", "\"Month\" BETWEEN 0 AND 12"));
    }
}

/// <summary>Cấu hình bảng InventoryDocument (design.md mục 12.1, 12.2).</summary>
internal sealed class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryDocument> builder)
    {
        builder.ToTable("InventoryDocument");
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        // Số chứng từ duy nhất theo (CompanyId, DocType, No) – design.md mục 12.2.
        builder.HasIndex(x => new { x.CompanyId, x.DocType, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.DocType, x.Date });
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.InvoiceNo).HasMaxLength(50);
        builder.Property(x => x.InvoiceSeries).HasMaxLength(20);
        builder.Property(x => x.DeliveryNoteNo).HasMaxLength(50);
        builder.Property(x => x.Deliverer).HasMaxLength(200);
        builder.Property(x => x.Receiver).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        // Phiếu trả NCC trỏ về phiếu nhập gốc (design.md mục 10.5).
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(x => x.LinkedDocumentId).OnDelete(DeleteBehavior.Restrict);
        // Tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalVat).HasPrecision(19, 2);

        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Kho trỏ tới khóa (Id, CompanyId) của Warehouse: CSDL tự chặn chứng từ dùng kho của công ty khác (design.md mục 12.2).
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.ToWarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.FromWarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng InventoryDocumentLine (design.md mục 12.1).</summary>
internal sealed class InventoryDocumentLineConfiguration : IEntityTypeConfiguration<InventoryDocumentLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryDocumentLine> builder)
    {
        builder.ToTable("InventoryDocumentLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.DocumentId, x.LineNo });
        // Số lượng numeric(18,4), hệ số numeric(18,6), đơn giá numeric(19,6), tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.VatAmount).HasPrecision(19, 2);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.LotNo).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        // Dòng trả trỏ về dòng nhập gốc – tính số còn trả được.
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(x => x.SourceLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng StockLedger – sổ kho (design.md mục 12).</summary>
internal sealed class StockLedgerConfiguration : IEntityTypeConfiguration<StockLedgerEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StockLedgerEntry> builder)
    {
        builder.ToTable("StockLedger");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.SignedQty);
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.LotNo).HasMaxLength(50);
        // Số lượng numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        // Thẻ kho: theo kho, mặt hàng, ngày; tính giá vốn: theo công ty, kỳ, mặt hàng.
        builder.HasIndex(x => new { x.CompanyId, x.WarehouseId, x.ItemId, x.PostingDate });
        builder.HasIndex(x => new { x.CompanyId, x.PeriodId, x.ItemId });
        builder.HasIndex(x => x.DocumentId);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockLedger_Direction", "\"Direction\" IN (-1, 1)");
            t.HasCheckConstraint("CK_StockLedger_BaseQty", "\"BaseQty\" > 0");
        });

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(x => x.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng StockOnHand – tồn tức thời (design.md mục 12.1).</summary>
internal sealed class StockOnHandConfiguration : IEntityTypeConfiguration<StockOnHand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StockOnHand> builder)
    {
        builder.ToTable("StockOnHand");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LotNo).HasMaxLength(50);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.ReservedQty).HasPrecision(18, 4);
        // Một dòng tồn cho mỗi công ty × kho × hàng × lô; NULLS NOT DISTINCT để hàng không theo lô (LotNo null) cũng chỉ một dòng
        // – câu INSERT … ON CONFLICT của ghi sổ dựa vào chỉ mục này.
        builder.HasIndex(x => new { x.CompanyId, x.WarehouseId, x.ItemId, x.LotNo }).IsUnique().AreNullsDistinct(false);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
