using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng ApprovalRule – quy trình duyệt (design.md mục 5, 6.2).</summary>
internal sealed class ApprovalRuleConfiguration : IEntityTypeConfiguration<ApprovalRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalRule> builder)
    {
        builder.ToTable("ApprovalRule");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Label);
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.MinAmount).HasPrecision(19, 2);
        // Mỗi công ty × loại chứng từ mỗi cấp một dòng.
        builder.HasIndex(x => new { x.CompanyId, x.DocType, x.Level }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng ApprovalStep – lịch sử duyệt (design.md mục 12.1).</summary>
internal sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("ApprovalStep");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DocType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.HasIndex(x => new { x.DocType, x.DocumentId, x.Round, x.Level });
        // Việc cần làm: tìm bước đang chờ theo công ty.
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        // Bộ phận của phiếu ở bước người duyệt động (lát O1).
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseRequest (design.md mục 10.2, 12.1).</summary>
internal sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequest");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        // Cột chữ cũ "Department" giữ nguyên tên, chỉ đọc – ngừng dùng từ lát A2 (design.md mục 18.5).
        builder.Property(x => x.LegacyDepartment).HasColumnName("Department").HasMaxLength(200);
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ClosedReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetUsedAmount).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        // Bộ phận phải cùng công ty với PR: khóa ngoại kèm CompanyId như kho nhận.
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.DepartmentId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.NewItems).WithOne().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.NewItems).HasField("_newItems").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PurchaseRequestNewItem – hàng mới chưa có mã trên PR (lát C2b, design.md mục 18.12).</summary>
internal sealed class PurchaseRequestNewItemConfiguration : IEntityTypeConfiguration<PurchaseRequestNewItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseRequestNewItem> builder)
    {
        builder.ToTable("PurchaseRequestNewItem");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RequestId, x.LineNo });
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Specification).HasMaxLength(1000);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.EstPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => x.LinkedLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Proc.Domain.Sourcing.SourcingRequest>().WithMany().HasForeignKey(x => x.SourcingRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseRequestLine.</summary>
internal sealed class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.ToTable("PurchaseRequestLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RequestId, x.LineNo });
        // Số lượng numeric(18,4), hệ số numeric(18,6), đơn giá numeric(19,6), tiền numeric(19,2) – CLAUDE.md mục 4.
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.EstPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SuggestedSupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseOrder (design.md mục 10.3, 12.1).</summary>
internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrder");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EmergencyReason).HasMaxLength(500);
        builder.Property(x => x.DocsCompletionNote).HasMaxLength(500);
        // Bộ phận đặt hàng (lát C1b – design.md mục 18.10).
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.DeliveryTerms).HasMaxLength(200);
        builder.Property(x => x.DeliveryAddress).HasMaxLength(500);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ClosedReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.ReceiveTolerancePct).HasPrecision(9, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(19, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetUsedAmount).HasPrecision(19, 2);
        builder.Property(x => x.TotalVat).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(x => new { x.WarehouseId, x.CompanyId }).HasPrincipalKey(w => new { w.Id, w.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PurchaseOrderLine.</summary>
internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.NetUnitPrice);
        builder.HasIndex(x => new { x.OrderId, x.LineNo });
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.DiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(19, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(19, 2);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.VatAmount).HasPrecision(19, 2);
        builder.Property(x => x.VatCode).HasMaxLength(5);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.SourcePrLineId);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => x.SourcePrLineId).OnDelete(DeleteBehavior.Restrict);
        // Nguồn giá (lát C1 – design.md mục 18.10). Dòng cũ nhận PriceSource = 0 (không xác định).
        builder.Ignore(x => x.IsPriceDeviation);
        builder.Ignore(x => x.IsOutOfContract);
        builder.Property(x => x.ApprovedUnitPrice).HasPrecision(19, 6);
        builder.Property(x => x.ApprovedDiscountPct).HasPrecision(9, 4);
        builder.Property(x => x.PriceDeviationReason).HasMaxLength(500);
        builder.HasOne<Proc.Domain.Pricing.SupplierPriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseContract>().WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Proc.Domain.Sourcing.RfqLine>().WithMany().HasForeignKey(x => x.SourceRfqLineId).OnDelete(DeleteBehavior.Restrict);
        // Ghi nhận mua theo NCC độc quyền (lát C1b – design.md mục 18.10).
        builder.HasOne<Proc.Domain.MasterData.SoleSourceApproval>().WithMany().HasForeignKey(x => x.SoleSourceApprovalId).OnDelete(DeleteBehavior.Restrict);
        // Mã hàng theo NCC chép từ NCC – sản phẩm lúc lưu/gửi (design.md mục 5.9), cùng độ dài với SupplierItem.SupplierItemCode.
        builder.Property(x => x.SupplierItemCode).HasMaxLength(50);
        // Phiếu duyệt NCC và giá tháng phủ dòng (lát G3 – design.md mục 18.25).
        builder.Ignore(x => x.IsOffMonthlyList);
        builder.Ignore(x => x.NeedsOffListApproval);
        builder.HasOne<Proc.Domain.Pricing.MonthlySupplierApprovalLine>().WithMany().HasForeignKey(x => x.MonthlyApprovalLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng DepartmentBudget – ngân sách bộ phận × tháng (design.md mục 18.24).</summary>
internal sealed class DepartmentBudgetConfiguration : IEntityTypeConfiguration<DepartmentBudget>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DepartmentBudget> builder)
    {
        builder.ToTable("DepartmentBudget");
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Ignore(x => x.Label);
        builder.HasIndex(x => new { x.CompanyId, x.DepartmentId, x.Year, x.Month }).IsUnique();
        // Khóa ngoại (DepartmentId, CompanyId) – CSDL chặn ngân sách gắn bộ phận của công ty khác (như PR, mục 18.5).
        builder.HasOne<Department>().WithMany().HasForeignKey(x => new { x.DepartmentId, x.CompanyId })
            .HasPrincipalKey(d => new { d.Id, d.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng DemandPlan – phiếu kế hoạch nhu cầu (design.md mục 18.25, lát G5a).</summary>
internal sealed class DemandPlanConfiguration : IEntityTypeConfiguration<DemandPlan>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DemandPlan> builder)
    {
        builder.ToTable("DemandPlan");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        // Dò trùng mặt hàng × năm × công ty và tổng hợp nhu cầu theo năm.
        builder.HasIndex(x => new { x.Year, x.Status });
        builder.HasIndex(x => x.ReplacesId);
        builder.Property(x => x.Note).HasMaxLength(DemandPlan.TextMaxLength);
        builder.Property(x => x.CancelReason).HasMaxLength(DemandPlan.TextMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ItemCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DemandPlan>().WithMany().HasForeignKey(x => x.ReplacesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng DemandPlanLine – dòng kế hoạch nhu cầu (SL 12 tháng numeric(18,4)).</summary>
internal sealed class DemandPlanLineConfiguration : IEntityTypeConfiguration<DemandPlanLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DemandPlanLine> builder)
    {
        builder.ToTable("DemandPlanLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.Months);
        builder.Ignore(x => x.Total);
        builder.HasIndex(x => x.ItemId);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        foreach (var month in new[] { "Qty01", "Qty02", "Qty03", "Qty04", "Qty05", "Qty06", "Qty07", "Qty08", "Qty09", "Qty10", "Qty11", "Qty12" })
            builder.Property<decimal>(month).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
    }
}
