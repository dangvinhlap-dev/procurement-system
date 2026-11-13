using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proc.Domain.MasterData;
using Proc.Domain.Organization;
using Proc.Domain.Purchasing;
using Proc.Infrastructure.Identity;

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
        // Nơi nhận của phiếu ở bước Thủ kho nơi nhận (lát Y3c).
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        // Người được duyệt thay khi duyệt theo ủy quyền (lát O2).
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OnBehalfOf).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng ApprovalDelegation – ủy quyền duyệt có thời hạn (lát O2, design.md mục 19.3).</summary>
internal sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalDelegation> builder)
    {
        builder.ToTable("ApprovalDelegation");
        builder.Property(x => x.DocType).HasMaxLength(10);
        builder.Property(x => x.Reason).HasMaxLength(ApprovalDelegation.TextMaxLength).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(ApprovalDelegation.TextMaxLength);
        // Duyệt: tìm ủy quyền của người được ủy quyền tại công ty; thông báo: tìm theo người ủy quyền.
        builder.HasIndex(x => new { x.CompanyId, x.DelegateUserId });
        builder.HasIndex(x => new { x.CompanyId, x.DelegatorUserId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DelegatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng ApprovalReminder – các lần nhắc quá hạn duyệt (lát O2c, design.md mục 19.3).</summary>
internal sealed class ApprovalReminderConfiguration : IEntityTypeConfiguration<ApprovalReminder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalReminder> builder)
    {
        builder.ToTable("ApprovalReminder");
        builder.Property(x => x.Id).ValueGeneratedNever();
        // Mỗi bước mỗi lần nhắc một dòng – hai tiến trình cùng quét thì lần lưu sau báo lỗi trùng, không gửi trùng thông báo.
        builder.HasIndex(x => new { x.ApprovalStepId, x.Sequence }).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalStep>().WithMany().HasForeignKey(x => x.ApprovalStepId).OnDelete(DeleteBehavior.Restrict);
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
        // Loại mua A/B/DV (lát Y3); phiếu cũ mặc định B = 2. Công trình cùng công ty với PR (khóa ngoại kèm CompanyId như bộ phận).
        builder.Property(x => x.Category).HasDefaultValue(PurchaseCategory.Goods).HasSentinel((PurchaseCategory)0);
        builder.HasOne<ConstructionProject>().WithMany()
            .HasForeignKey(x => new { x.ConstructionProjectId, x.CompanyId }).HasPrincipalKey(p => new { p.Id, p.CompanyId })
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
        builder.Property(x => x.RequiredQty).HasPrecision(18, 4);
        builder.Property(x => x.StockQty).HasPrecision(18, 4);
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
        // Lát Y7: loại mua theo đề nghị nguồn (PO cũ là B = 2 – migration điền lại theo dòng đề nghị A), công trình cùng công ty (khóa ngoại
        // kèm CompanyId như PR, RFQ).
        builder.Property(x => x.Category).HasDefaultValue(PurchaseCategory.Goods).HasSentinel((PurchaseCategory)0);
        builder.HasOne<ConstructionProject>().WithMany()
            .HasForeignKey(x => new { x.ConstructionProjectId, x.CompanyId }).HasPrincipalKey(p => new { p.Id, p.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
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
        // Lát X7: công ty bán của PO mua nội bộ – màn hình "Đơn bán nội bộ" của công ty bán lọc theo cột này.
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.InternalSellerCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InternalSellerCompanyId).HasFilter("\"InternalSellerCompanyId\" IS NOT NULL");
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Cấu hình bảng InternalSaleConfirmation – quyết định của công ty bán với PO mua nội bộ (lát X7, design.md mục 19.3). Mỗi PO tối đa một
/// quyết định (chỉ mục duy nhất – hai người bấm cùng lúc thì một người bị chặn).
/// </summary>
internal sealed class InternalSaleConfirmationConfiguration : IEntityTypeConfiguration<InternalSaleConfirmation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InternalSaleConfirmation> builder)
    {
        builder.ToTable("InternalSaleConfirmation");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OrderNo).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(InternalSaleConfirmation.NoteMaxLength);
        builder.HasIndex(x => x.PurchaseOrderId).IsUnique();
        builder.HasIndex(x => x.CompanyId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.BuyerCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng PurchaseOrderSupplierResponse – NCC xác nhận / từ chối PO trên Portal (lát P4a). Mỗi PO tối đa một phản hồi chưa mở lại (chỉ
/// mục duy nhất có điều kiện – hai người cùng NCC bấm cùng lúc thì người sau bị chặn).
/// </summary>
internal sealed class PurchaseOrderSupplierResponseConfiguration : IEntityTypeConfiguration<PurchaseOrderSupplierResponse>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrderSupplierResponse> builder)
    {
        builder.ToTable("PurchaseOrderSupplierResponse");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Note).HasMaxLength(PurchaseOrderSupplierResponse.NoteMaxLength);
        builder.Property(x => x.RespondedByEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReopenedByName).HasMaxLength(200);
        builder.Property(x => x.ReopenReason).HasMaxLength(PurchaseOrderSupplierResponse.NoteMaxLength);
        builder.Ignore(x => x.IsCurrent);
        // Mỗi PO tối đa một phản hồi đang có hiệu lực; phản hồi đã mở lại là lịch sử, không tính.
        builder.HasIndex(x => x.PurchaseOrderId).IsUnique().HasFilter("\"ReopenedAt\" IS NULL");
        // Hai người cùng mở lại một phản hồi: UPDATE kèm điều kiện "chưa mở lại" nên người sau nhận lỗi.
        builder.Property(x => x.ReopenedAt).IsConcurrencyToken();
        builder.HasOne<Proc.Infrastructure.Identity.AppUser>().WithMany().HasForeignKey(x => x.ReopenedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CompanyId);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierPortalUser>().WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng PurchaseOrderDeliveryNotice – phiếu báo giao hàng NCC gửi trên Portal (lát P5, design.md 19.5). Số phiếu BGH duy nhất theo
/// công ty đặt hàng.
/// </summary>
internal sealed class PurchaseOrderDeliveryNoticeConfiguration : IEntityTypeConfiguration<PurchaseOrderDeliveryNotice>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrderDeliveryNotice> builder)
    {
        builder.ToTable("PurchaseOrderDeliveryNotice");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.SupplierId);
        builder.Property(x => x.DeliveryNoteNo).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Deliverer).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(PurchaseOrderDeliveryNotice.NoteMaxLength);
        builder.Property(x => x.SubmittedByEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CancelledByEmail).HasMaxLength(256);
        builder.Property(x => x.CancelReason).HasMaxLength(PurchaseOrderDeliveryNotice.NoteMaxLength);
        // Hai người cùng NCC hủy cùng lúc / nơi nhận lập XNH đúng lúc NCC hủy: UPDATE kèm điều kiện trạng thái cũ nên người sau nhận lỗi.
        builder.Property(x => x.Status).IsConcurrencyToken();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierPortalUser>().WithMany().HasForeignKey(x => x.PortalUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.NoticeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PurchaseOrderDeliveryNoticeLine.</summary>
internal sealed class PurchaseOrderDeliveryNoticeLineConfiguration : IEntityTypeConfiguration<PurchaseOrderDeliveryNoticeLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseOrderDeliveryNoticeLine> builder)
    {
        builder.ToTable("PurchaseOrderDeliveryNoticeLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.NoticeId, x.LineNo });
        // Cộng SL đang báo giao theo dòng PO khi kiểm SL còn giao được.
        builder.HasIndex(x => x.PurchaseOrderLineId);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
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

/// <summary>Cấu hình bảng ConstructionProject – công trình có tổng dự toán (lát Y2, design.md mục 19.2).</summary>
internal sealed class ConstructionProjectConfiguration : IEntityTypeConfiguration<ConstructionProject>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConstructionProject> builder)
    {
        builder.ToTable("ConstructionProject");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(ConstructionProject.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(ConstructionProject.NameMaxLength).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(ConstructionProject.NameMaxLength);
        builder.Property(x => x.Note).HasMaxLength(ConstructionProject.TextMaxLength);
        builder.Property(x => x.EstimateAmount).HasPrecision(19, 2);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        // Khóa ngoại (DepartmentId, CompanyId) – CSDL chặn công trình gắn phòng ban của công ty khác (như PR, ngân sách – mục 18.5, 18.24).
        builder.HasOne<Department>().WithMany().HasForeignKey(x => new { x.DepartmentId, x.CompanyId })
            .HasPrincipalKey(d => new { d.Id, d.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Revisions).WithOne().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Revisions).HasField("_revisions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng ConstructionProjectEstimateRevision – lịch sử dự toán công trình (lát Y2).</summary>
internal sealed class ConstructionProjectEstimateRevisionConfiguration : IEntityTypeConfiguration<ConstructionProjectEstimateRevision>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConstructionProjectEstimateRevision> builder)
    {
        builder.ToTable("ConstructionProjectEstimateRevision");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PreviousAmount).HasPrecision(19, 2);
        builder.Property(x => x.NewAmount).HasPrecision(19, 2);
        builder.Property(x => x.Basis).HasMaxLength(ConstructionProject.TextMaxLength);
        builder.Property(x => x.Reason).HasMaxLength(ConstructionProject.TextMaxLength);
        // Mỗi lần một dòng – hai người điều chỉnh cùng lúc thì một người bị lỗi trùng thay vì có hai "lần 2".
        builder.HasIndex(x => new { x.ProjectId, x.RevisionNo }).IsUnique();
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

/// <summary>Cấu hình bảng PurchasingUnit – danh mục đơn vị mua của công ty (lát Y5a, design.md mục 19.3).</summary>
internal sealed class PurchasingUnitConfiguration : IEntityTypeConfiguration<PurchasingUnit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchasingUnit> builder)
    {
        builder.ToTable("PurchasingUnit");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(PurchasingUnit.NameMaxLength).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(PurchasingUnit.NoteMaxLength);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        // Khóa (Id, CompanyId) để dòng bảng tổng hợp (lát Y5b) trỏ tới đơn vị kèm công ty – CSDL chặn gán đơn vị của công ty khác.
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId });
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cấu hình bảng PurchaseSummary – bảng tổng hợp đề xuất mua 2A/2B (lát Y5b, design.md mục 19.3).</summary>
internal sealed class PurchaseSummaryConfiguration : IEntityTypeConfiguration<PurchaseSummary>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseSummary> builder)
    {
        builder.ToTable("PurchaseSummary");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.Property(x => x.Note).HasMaxLength(PurchaseSummary.NoteMaxLength);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ClosedReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        // Cột ngân sách của lớp chung ApprovableDocument – bảng tổng hợp không ghi ngân sách (đã tính ở đề nghị mua), luôn trống.
        builder.Property(x => x.BudgetAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetUsedAmount).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // Bộ phận, công trình cùng công ty với phiếu: khóa ngoại kèm CompanyId như đề nghị mua.
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.DepartmentId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConstructionProject>().WithMany()
            .HasForeignKey(x => new { x.ConstructionProjectId, x.CompanyId }).HasPrincipalKey(p => new { p.Id, p.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SummaryId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// Cấu hình bảng PaymentRequest – đề nghị thanh toán 5A/5B (lát Y8, design.md mục 19.3 bước 7). Số DNT duy nhất theo công ty; bộ phận, công trình
/// khóa ngoại kèm công ty như bảng tổng hợp.
/// </summary>
internal sealed class PaymentRequestConfiguration : IEntityTypeConfiguration<PaymentRequest>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaymentRequest> builder)
    {
        builder.ToTable("PaymentRequest");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.DocType);
        builder.Ignore(x => x.IsPaid);
        builder.Ignore(x => x.IsSundry);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        // Người nhận tiền của đề nghị tổng hợp tạp vụ 1C (lát Y11b) – 1C không có PO, NCC.
        builder.Property(x => x.PayeeName).HasMaxLength(PaymentRequest.PayeeMaxLength);
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.SupplierId);
        builder.Property(x => x.Note).HasMaxLength(PaymentRequest.NoteMaxLength);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ClosedReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.BankName).HasMaxLength(200);
        builder.Property(x => x.BankBranch).HasMaxLength(200);
        builder.Property(x => x.BankAccountNumber).HasMaxLength(50);
        builder.Property(x => x.BankAccountName).HasMaxLength(200);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        builder.Property(x => x.DeductionAmount).HasPrecision(19, 2);
        builder.Property(x => x.DeductionReason).HasMaxLength(PaymentRequest.NoteMaxLength);
        builder.Property(x => x.PayableAmount).HasPrecision(19, 2);
        builder.Property(x => x.PaymentNote).HasMaxLength(PaymentRequest.NoteMaxLength);
        // Cột ngân sách của lớp chung ApprovableDocument – đề nghị thanh toán không ghi ngân sách (PO đã tính), luôn trống.
        builder.Property(x => x.BudgetAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetUsedAmount).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.DepartmentId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConstructionProject>().WithMany()
            .HasForeignKey(x => new { x.ConstructionProjectId, x.CompanyId }).HasPrincipalKey(p => new { p.Id, p.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng PaymentRequestLine – dòng đề nghị thanh toán (một XNH có hóa đơn).</summary>
internal sealed class PaymentRequestLineConfiguration : IEntityTypeConfiguration<PaymentRequestLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PaymentRequestLine> builder)
    {
        builder.ToTable("PaymentRequestLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RequestId, x.LineNo });
        // Tìm đề nghị chưa hủy đang giữ một XNH (mỗi XNH một đề nghị – Y8-2).
        builder.HasIndex(x => x.ReceiptConfirmationId);
        // Dòng 1C (lát Y11b) là một bảng kê tạp vụ 2C – không có XNH, số hóa đơn; mỗi bảng kê chỉ ở một đề nghị chưa hủy (kiểm ở Application).
        builder.HasIndex(x => x.SundryExpenseSheetId);
        builder.Property(x => x.InvoiceNo).HasMaxLength(50);
        builder.Property(x => x.InvoiceSeries).HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.Description).HasMaxLength(PaymentRequest.DescriptionMaxLength).IsRequired();
        builder.HasOne<GoodsReceiptConfirmation>().WithMany().HasForeignKey(x => x.ReceiptConfirmationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SundryExpenseSheet>().WithMany().HasForeignKey(x => x.SundryExpenseSheetId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Cấu hình bảng SundryExpenseSheet – bảng kê chi phí tạp vụ 2C (lát Y11a, design.md mục 19.2 loại C). Số BKT duy nhất theo công ty; bộ phận khóa
/// ngoại kèm công ty như bảng tổng hợp.
/// </summary>
internal sealed class SundryExpenseSheetConfiguration : IEntityTypeConfiguration<SundryExpenseSheet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SundryExpenseSheet> builder)
    {
        builder.ToTable("SundryExpenseSheet");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DocumentNumber);
        builder.Ignore(x => x.ApprovalAmount);
        builder.Ignore(x => x.IsAwaitingApproval);
        builder.Ignore(x => x.DocType);
        builder.Property(x => x.No).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.No }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Date });
        // Tính định mức đã dùng theo bộ phận × tháng.
        builder.HasIndex(x => new { x.DepartmentId, x.Date });
        builder.Property(x => x.Note).HasMaxLength(SundryExpenseSheet.NoteMaxLength);
        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.ClosedReason).HasMaxLength(500);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 2);
        // Định mức tháng lúc lưu / gửi gần nhất (ApprovableDocument) – bảng kê tính vào ngân sách bộ phận × tháng.
        builder.Property(x => x.BudgetAmount).HasPrecision(19, 2);
        builder.Property(x => x.BudgetUsedAmount).HasPrecision(19, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.DepartmentId, x.CompanyId }).HasPrincipalKey(d => new { d.Id, d.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SheetId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Cấu hình bảng SundryExpenseLine – một khoản chi của bảng kê tạp vụ.</summary>
internal sealed class SundryExpenseLineConfiguration : IEntityTypeConfiguration<SundryExpenseLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SundryExpenseLine> builder)
    {
        builder.ToTable("SundryExpenseLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.SheetId, x.LineNo });
        builder.Property(x => x.Description).HasMaxLength(SundryExpenseSheet.DescriptionMaxLength).IsRequired();
        builder.Property(x => x.Vendor).HasMaxLength(SundryExpenseSheet.VendorMaxLength);
        builder.Property(x => x.ReceiptNo).HasMaxLength(SundryExpenseSheet.ReceiptNoMaxLength);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
    }
}

/// <summary>Cấu hình bảng PurchaseSummaryLine – dòng bảng tổng hợp (một dòng đề nghị mua + đơn vị mua).</summary>
internal sealed class PurchaseSummaryLineConfiguration : IEntityTypeConfiguration<PurchaseSummaryLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PurchaseSummaryLine> builder)
    {
        builder.ToTable("PurchaseSummaryLine");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.SummaryId, x.LineNo });
        // Tra "dòng đề nghị đã ở bảng tổng hợp nào" (mỗi dòng đề nghị chỉ ở một bảng chưa hủy – kiểm tra ở tầng Application dưới khóa đề nghị).
        builder.HasIndex(x => x.RequestLineId);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UomFactor).HasPrecision(18, 6);
        builder.Property(x => x.BaseQty).HasPrecision(18, 4);
        builder.Property(x => x.EstPrice).HasPrecision(19, 6);
        builder.Property(x => x.Amount).HasPrecision(19, 2);
        builder.Property(x => x.Note).HasMaxLength(PurchaseSummary.LineNoteMaxLength);
        builder.HasOne<PurchaseRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => x.RequestLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomId).OnDelete(DeleteBehavior.Restrict);
        // Đơn vị mua cùng công ty với phiếu (khóa thay thế Id + CompanyId của PurchasingUnit – lát Y5a).
        builder.HasOne<PurchasingUnit>().WithMany()
            .HasForeignKey(x => new { x.PurchasingUnitId, x.CompanyId }).HasPrincipalKey(u => new { u.Id, u.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
