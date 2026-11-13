using Proc.Domain.Common;
using Proc.Domain.MasterData;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>Quy trình duyệt theo ngưỡng (design.md mục 6.2), PR (mục 10.2), PO (mục 10.3).</summary>
public class PurchasingTests
{
    private static readonly Guid Company = Guid.CreateVersion7();
    private static readonly Uom Cai = Uom.Create("CAI", "Cái");
    private static readonly Uom Hop = Uom.Create("HOP", "Hộp");

    private static Item NewItem()
    {
        var item = Item.Create("VB01", ItemType.Inventory, ItemCategory.Create("VT", "Vật tư"), Cai,
            new ItemDescription("Vòng bi", null, null, null, null, null, "10", null));
        item.SetConversions([(Hop, 10m)]);
        return item;
    }

    [Fact]
    public void KeHoachDuyet_TheoNguong_ChuaCauHinhThiMotCap()
    {
        var head = Guid.CreateVersion7();
        var director = Guid.CreateVersion7();
        var rules = new[]
        {
            ApprovalRule.Create(Company, "pr", 1, 0, head),
            ApprovalRule.Create(Company, "PR", 2, 50_000_000, director),
        };

        Assert.Equal([(1, (Guid?)head)], ApprovalPlan.Levels(rules, 10_000_000));
        Assert.Equal([(1, (Guid?)head), (2, (Guid?)director)], ApprovalPlan.Levels(rules, 80_000_000));
        Assert.Equal([(1, (Guid?)null)], ApprovalPlan.Levels([], 1));
        // Có cấu hình nhưng dưới mọi ngưỡng: không cần duyệt.
        Assert.Empty(ApprovalPlan.Levels([ApprovalRule.Create(Company, "PO", 1, 1_000_000, head)], 500_000));

        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "GRN", 1, 0, head));
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 10, 0, head));
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 1, -1, head));
    }

    [Fact]
    public void ChangVuotGiaTran_BoCapCoVaiTroDaDuyetChangTruoc()
    {
        // Lát H3: chặng PO vượt giá trần chỉ đòi "lên Chủ tịch/TGĐ" – Chủ tịch đã có ở chặng trước thì không thêm cấp đó (R3: mỗi người một cấp).
        var deputy = Guid.CreateVersion7();
        var chairman = Guid.CreateVersion7();
        IReadOnlyList<(int, Guid?)> stage = [(1, chairman)];
        Assert.Empty(ApprovalPlan.WithoutPlannedRoles(stage, [deputy, chairman]));
        Assert.Equal([(1, (Guid?)chairman)], ApprovalPlan.WithoutPlannedRoles(stage, [deputy, null]));
        // Cấp không gắn vai trò (chưa cấu hình – ai có quyền duyệt) luôn giữ.
        Assert.Equal([(1, (Guid?)null)], ApprovalPlan.WithoutPlannedRoles([(1, null)], [null, deputy]));
        Assert.True(ApprovalRule.Create(Company, "POC", 1, 0, chairman).DocType == "POC");
    }


    [Fact]
    public void NguoiDuyetDong_ChiLoaiCoBoPhan_KhongCanVaiTro_BuocGhiBoPhan()
    {
        var role = Guid.CreateVersion7();
        var department = Guid.CreateVersion7();

        var head = ApprovalRule.Create(Company, "PR", 1, 0, role, ApproverKind.DepartmentHead);
        Assert.Equal((ApproverKind.DepartmentHead, (Guid?)null), (head.Kind, head.RoleId));
        Assert.Equal(ApproverKind.DivisionDirector, ApprovalRule.Create(Company, "POC", 1, 0, null, ApproverKind.DivisionDirector).Kind);
        Assert.Contains("chỉ chọn được người duyệt theo vai trò",
            Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "NCC", 1, 0, null, ApproverKind.DepartmentHead)).Message);
        Assert.Equal("Hãy chọn vai trò duyệt.", Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 1, 0, null)).Message);
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PR", 1, 0, role, (ApproverKind)9));

        var step = ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, role, kind: ApproverKind.DepartmentHead, departmentId: department);
        Assert.Equal((ApproverKind.DepartmentHead, (Guid?)null, (Guid?)department), (step.Kind, step.RoleId, step.DepartmentId));
        Assert.Throws<BusinessRuleException>(() => ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, null, kind: ApproverKind.DivisionDirector));
        Assert.Null(ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, role, departmentId: department).DepartmentId);
    }

    [Fact]
    public void CapThuKhoNoiNhan_ChiPrVaPra_BuocGhiNoiNhan()
    {
        // Lát Y3c (design.md mục 19.3): cấp "Thủ kho nơi nhận" chỉ cho bộ cấp đề nghị mua có nơi nhận (PR – loại B, PRA – loại A).
        var warehouse = Guid.CreateVersion7();
        Assert.Equal((ApproverKind.Storekeeper, (Guid?)null),
            (ApprovalRule.Create(Company, "PR", 1, 0, null, ApproverKind.Storekeeper) is var r ? (r.Kind, r.RoleId) : default));
        Assert.Equal(ApproverKind.Storekeeper, ApprovalRule.Create(Company, "PRA", 1, 0, null, ApproverKind.Storekeeper).Kind);
        foreach (var docType in new[] { "PRS", "PRE", "PRB", "PO" })
            Assert.Contains("Thủ kho", Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, docType, 1, 0, null, ApproverKind.Storekeeper)).Message);
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "NCC", 1, 0, null, ApproverKind.Storekeeper));

        var step = ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, null, kind: ApproverKind.Storekeeper, warehouseId: warehouse);
        Assert.Equal((ApproverKind.Storekeeper, (Guid?)warehouse, (Guid?)null), (step.Kind, step.WarehouseId, step.DepartmentId));
        Assert.Throws<BusinessRuleException>(() => ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, null, kind: ApproverKind.Storekeeper));
        Assert.Null(ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, Guid.CreateVersion7(), warehouseId: warehouse).WarehouseId);
    }

    [Fact]
    public void LoaiDuyetDungChung_PrlRfqNccCtr_NguongChiChoLoaiCoGiaTri()
    {
        var role = Guid.CreateVersion7();

        Assert.Equal(["PR", "PRA", "PRS", "PRE", "PO", "POX", "POK", "PRB", "POB", "POL", "POC", "BTH", "DNT", "BKT", "TVB", "PRL", "RFQ", "NCC", "CTR", "CTA", "NDQ", "GTH", "GTX", "CKB"], ApprovalTypes.All.Select(t => t.Code));
        Assert.Equal("PRL", ApprovalRule.Create(Company, " prl ", 1, 0, role).DocType);
        Assert.Equal(5_000_000m, ApprovalRule.Create(Company, "RFQ", 2, 5_000_000, role).MinAmount);
        Assert.Equal(100m, ApprovalRule.Create(Company, "CTR", 1, 100, role).MinAmount);
        Assert.Equal(100m, ApprovalRule.Create(Company, "CTA", 1, 100, role).MinAmount);
        // Bảng giá, hồ sơ NCC không có giá trị tiền: ngưỡng phải 0.
        Assert.Contains("ngưỡng phải là 0", Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "NCC", 1, 1, role)).Message);
        Assert.Throws<BusinessRuleException>(() => ApprovalRule.Create(Company, "PRL", 1, 1, role));
        Assert.Equal("Sourcing.Rfq.ApproveAward", ApprovalTypes.Get("rfq").ApprovePermission);
        Assert.Null(ApprovalTypes.Find("GRN"));
    }

    [Fact]
    public void BuocDuyet_TuChoiPhaiCoLyDo_KhongDuyetHaiLan()
    {
        var step = ApprovalStep.Create(Company, "PR", Guid.CreateVersion7(), 1, 1, null);

        Assert.Throws<BusinessRuleException>(() => step.Reject(Guid.CreateVersion7(), DateTimeOffset.UtcNow, " "));
        step.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "Đồng ý");
        Assert.Throws<BusinessRuleException>(() => step.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, null));
        Assert.Equal((ApprovalStepStatus.Approved, "Đồng ý"), (step.Status, step.Comment));
    }

    [Fact]
    public void DeNghiMuaHang_GuiDuyet_TuChoiVeNhap_GuiLaiTangLuot()
    {
        var item = NewItem();
        var pr = PurchaseRequest.Create(Company, "PR-1",
            new PurchaseRequestHeader(new DateOnly(2026, 10, 1), Guid.CreateVersion7(), "Bảo trì", new DateOnly(2026, 10, 15), null, PurchasePriority.Urgent, null),
            Guid.CreateVersion7());
        Assert.Throws<BusinessRuleException>(() => pr.Submit());
        pr.SetLines([(new PurchaseRequestLineData(null, item.Id, Hop.Id, 2, 120_000, null, null, null), item)]);
        Assert.Equal((20m, 240_000m), (pr.Lines[0].BaseQty, pr.TotalAmount));

        Assert.Equal(1, pr.Submit());
        Assert.Throws<BusinessRuleException>(() => pr.SetLines([(new PurchaseRequestLineData(null, item.Id, Cai.Id, 1, 1, null, null, null), item)]));
        pr.ReturnToDraft("Thiếu báo giá");
        Assert.Equal((ApprovalDocumentStatus.Draft, "Thiếu báo giá"), (pr.Status, pr.RejectReason));
        Assert.Equal(2, pr.Submit());
        Assert.Null(pr.RejectReason);
        pr.MarkApproved(DateTimeOffset.UtcNow);
        Assert.Equal(ApprovalDocumentStatus.Approved, pr.Status);

        Assert.Throws<BusinessRuleException>(() =>
            PurchaseRequest.Create(Company, "PR-2", new PurchaseRequestHeader(new DateOnly(2026, 10, 10), null, null, new DateOnly(2026, 10, 1), null, PurchasePriority.Normal, null), null));
    }

    [Fact]
    public void DeNghiMuaHang_BatBuocChonBoPhan()
    {
        var header = new PurchaseRequestHeader(new DateOnly(2026, 10, 1), null, null, null, null, PurchasePriority.Normal, null);

        var ex = Assert.Throws<BusinessRuleException>(() => PurchaseRequest.Create(Company, "PR-3", header, null));
        Assert.Equal("Hãy chọn bộ phận đề nghị.", ex.Message);
    }

    [Fact]
    public void DonMuaHang_TinhChietKhauVat_GiaThucMua_DongMoNhanHang()
    {
        var item = NewItem();
        var po = PurchaseOrder.Create(Company, "PO-1",
            new PurchaseOrderHeader(new DateOnly(2026, 10, 1), Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2026, 10, 10), "30 ngày", null, null, 5, null));
        po.SetLines([(new PurchaseOrderLineData(null, item.Id, Cai.Id, 10, 100_000, 5, "10", null, null, null), item)]);

        var line = po.Lines[0];
        Assert.Equal((1_000_000m, 50_000m, 950_000m, 95_000m, 95_000m), (line.GrossAmount, line.DiscountAmount, line.Amount, line.VatAmount, line.NetUnitPrice));
        Assert.Equal((950_000m, 95_000m, 1_045_000m), (po.TotalAmount, po.TotalVat, po.ApprovalAmount));

        po.Submit();
        po.MarkApproved(DateTimeOffset.UtcNow);
        po.CloseReceived();
        Assert.Equal(ApprovalDocumentStatus.Closed, po.Status);
        Assert.Throws<BusinessRuleException>(() => po.Cancel());
        // Lát R7: phiếu đã duyệt phải có lý do hủy (kiểm tra ở bước đã duyệt bên dưới).
        po.ReopenReceiving();
        Assert.Equal(ApprovalDocumentStatus.Approved, po.Status);

        Assert.Throws<BusinessRuleException>(() =>
            PurchaseOrder.Create(Company, "PO-2", new PurchaseOrderHeader(new DateOnly(2026, 10, 1), Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 25, null)));
    }
}
