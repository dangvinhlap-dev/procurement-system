using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng GoodsReceiptConfirmation – phiếu xác nhận nhận hàng XNH (lát K2, design.md mục 19.4).</summary>
internal sealed class GoodsReceiptConfirmationConfiguration : IEntityTypeConfiguration<GoodsReceiptConfirmation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GoodsReceiptConfirmation> builder)
    {
        builder.ToTable("GoodsReceiptConfirmation");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.Property(x => x.DeliveryNoteNo).HasMaxLength(100);
        builder.Property(x => x.Deliverer).HasMaxLength(200);
        builder.Property(x => x.InvoiceNo).HasMaxLength(50);
        builder.Property(x => x.InvoiceSeries).HasMaxLength(50);
        builder.Property(x => x.InvoiceAmount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CancelReason).HasMaxLength(GoodsReceiptConfirmation.MaxReasonLength);
        builder.Property(x => x.AcceptanceNote).HasMaxLength(GoodsReceiptConfirmation.MaxReasonLength);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalVat).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        // Nơi nhận phải là kho của cùng công ty (như PO).
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ConfirmationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.Adjustments).WithOne().HasForeignKey(a => a.ConfirmationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Adjustments).HasField("_adjustments").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng GoodsReceiptConfirmationLine.</summary>
internal sealed class GoodsReceiptConfirmationLineConfiguration : IEntityTypeConfiguration<GoodsReceiptConfirmationLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GoodsReceiptConfirmationLine> builder)
    {
        builder.ToTable("GoodsReceiptConfirmationLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ConfirmationId, x.LineNo });
        // Cộng SL đã nhận theo dòng PO (thay phiếu nhập kho) – truy vấn thường xuyên.
        builder.HasIndex(x => x.PurchaseOrderLineId);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.DeliveredQty).HasPrecision(18, 4);
        builder.Property(x => x.AcceptedQty).HasPrecision(18, 4);
        builder.Property(x => x.RejectedQty).HasPrecision(18, 4);
        builder.Property(x => x.AdjustedQty).HasPrecision(18, 4);
        builder.Property(x => x.ReceivedQty).HasPrecision(18, 4);
        builder.Property(x => x.ReceivedBaseQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.VatAmount).HasPrecision(19, 2);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.CheckNote).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng GoodsReceiptAdjustment – lịch sử điều chỉnh SL thực nhận (chỉ thêm).</summary>
internal sealed class GoodsReceiptAdjustmentConfiguration : IEntityTypeConfiguration<GoodsReceiptAdjustment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GoodsReceiptAdjustment> builder)
    {
        builder.ToTable("GoodsReceiptAdjustment");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.ConfirmationId);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.Reason).HasMaxLength(GoodsReceiptConfirmation.MaxReasonLength).IsRequired();
        builder.HasOne<GoodsReceiptConfirmationLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
    }
}
