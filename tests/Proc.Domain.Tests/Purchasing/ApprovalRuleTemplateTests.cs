using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>
/// Vai trò mẫu và cấu hình duyệt mẫu theo sơ đồ tổ chức và lưu đồ phê duyệt mua sắm (lát G10, design.md mục 18.25): mẫu phải tự nhất quán để
/// áp xong là duyệt được ngay.
/// </summary>
public class ApprovalRuleTemplateTests
{
    [Fact]
    public void VaiTroMau_MaHopLe_KhongTrung_QuyenCoTrongDanhMuc()
    {
        var codes = RoleTemplates.Procurement.Select(t => t.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        foreach (var t in RoleTemplates.Procurement)
        {
            Role.Create(t.Code, t.Name);
            Assert.Empty(Permissions.FindUnknown(t.Permissions));
        }

        // Có đủ vai trò của sơ đồ.
        Assert.Subset(codes.ToHashSet(),
            new HashSet<string> { "CHUTICH", "TRUONGBANCUVT", "PHOBAN", "THAMDINHCL", "THAMDINHPL", "VPBDIEUPHOI", "VPBPHANTICH", "CUCTRUONGPHONG", "CUCDATHANG", "DONVIDATHANG" });
    }

    [Fact]
    public void VaiTroMau_KhongKiemNhiem_TheoSoDo()
    {
        var byCode = RoleTemplates.Procurement.ToDictionary(t => t.Code, t => t.Permissions.ToHashSet());
        var approvals = ApprovalTypes.All.Select(t => t.ApprovePermission).ToHashSet();
        // Thành viên chuyên môn, Văn phòng Ban, nhân viên đặt hàng, đơn vị đặt hàng không có quyền duyệt nào (không biểu quyết, chỉ đề xuất).
        foreach (var code in new[] { "THAMDINHCL", "THAMDINHPL", "VPBDIEUPHOI", "VPBPHANTICH", "CUCDATHANG", "DONVIDATHANG" })
            Assert.Empty(byCode[code].Intersect(approvals));
        // Q2 (người dùng chốt 08/11/2026): Phòng cung ứng tìm kiếm, đánh giá NCC, hỏi giá; Văn phòng Ban không lập hồ sơ NCC, không đặt hàng.
        foreach (var code in new[] { "VPBPHANTICH", "VPBDIEUPHOI" })
        {
            Assert.DoesNotContain(Permissions.Purchase.PoCreate, byCode[code]);
            Assert.DoesNotContain(Permissions.MasterData.SupplierManage, byCode[code]);
        }

        // Phòng cung ứng không lập/sửa giá, không lập phiếu duyệt NCC & giá tháng (không tự ý thay đổi NCC/giá đã duyệt).
        foreach (var code in new[] { "CUCTRUONGPHONG", "CUCDATHANG" })
        {
            Assert.Contains(Permissions.MasterData.SupplierManage, byCode[code]);
            Assert.Contains(Permissions.Sourcing.RfqManage, byCode[code]);
            Assert.DoesNotContain(Permissions.Pricing.PriceListManage, byCode[code]);
            Assert.DoesNotContain(Permissions.Pricing.MonthlyApprovalManage, byCode[code]);
        }
    }

    [Fact]
    public void CauHinhDuyetMau_VaiTroCoQuyenDuyetLoaiPhieu_CapLienTuc()
    {
        var byCode = RoleTemplates.Procurement.ToDictionary(t => t.Code, t => t.Permissions.ToHashSet());
        foreach (var rule in ApprovalRuleTemplates.Procurement)
        {
            var type = ApprovalTypes.Get(rule.DocType);
            // Bộ theo sơ đồ (G10) chỉ có cấp theo vai trò.
            Assert.Equal(ApproverKind.Role, rule.Kind);
            Assert.True(byCode.ContainsKey(rule.RoleCode!), $"{rule.DocType} cấp {rule.Level}: vai trò {rule.RoleCode} không có trong bộ mẫu");
            Assert.True(byCode[rule.RoleCode!].Contains(type.ApprovePermission),
                $"{rule.DocType} cấp {rule.Level}: vai trò {rule.RoleCode} thiếu quyền {type.ApprovePermission}");
            Assert.False(string.IsNullOrWhiteSpace(rule.Note));
        }

        foreach (var group in ApprovalRuleTemplates.Procurement.GroupBy(r => r.DocType))
        {
            Assert.Equal(Enumerable.Range(1, group.Count()), group.Select(r => r.Level).Order());
            // Cùng loại thì cùng phạm vi (tổng công ty hoặc mọi công ty).
            Assert.Single(group.Select(r => r.HeadOfficeOnly).Distinct());
        }

        // PO theo sơ đồ: 02 Phó ban kiểm tra, xác nhận rồi Chủ tịch/TGĐ duyệt.
        Assert.Equal(["PHOBAN", "PHOBAN", "CHUTICH"],
            ApprovalRuleTemplates.Procurement.Where(r => r.DocType == "PO").OrderBy(r => r.Level).Select(r => r.RoleCode));

        // Q3 (lát H3): PO vượt giá trần → Chủ tịch/TGĐ phê duyệt, mọi công ty như PO.
        Assert.Equal([("CHUTICH", false)],
            ApprovalRuleTemplates.Procurement.Where(r => r.DocType == "POC").Select(r => (r.RoleCode, r.HeadOfficeOnly)));

        // Q6 (lát H2): phiếu GTH có ngoại lệ → Chủ tịch/TGĐ phê duyệt, chỉ ở tổng công ty như GTH.

        Assert.Equal([("CHUTICH", true)],
            ApprovalRuleTemplates.Procurement.Where(r => r.DocType == "GTX").Select(r => (r.RoleCode, r.HeadOfficeOnly)));
    }

    [Fact]
    public void BoVaiTroMau_TheoBo_CuvtGomViTriKyBieuMau_BanKhongGomVaiTroCuvtRieng()
    {
        var cuvt = RoleTemplates.InSet("cuvt").Select(t => t.Code).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "TRUONGPHONGBAN", "GDKHOI", "TPCUVT", "NVCUVT", "TPTCKT", "GDTCKT", "TPHC", "PHOCTUBCL", "DENGHI", "KHO", "CHUTICH" },
            cuvt);
        var ban = RoleTemplates.InSet(RoleTemplateSets.Ban).Select(t => t.Code).ToHashSet();
        Assert.Contains("CHUTICH", ban);
        Assert.DoesNotContain("TPCUVT", ban);
        // Hai bộ cộng lại đủ mọi vai trò mẫu; trống = mọi vai trò.
        Assert.Equal(RoleTemplates.Procurement.Count, ban.Union(cuvt).Count());
        Assert.Equal(RoleTemplates.Procurement.Count, RoleTemplates.InSet(null).Count);
        Assert.Throws<Proc.Domain.Common.BusinessRuleException>(() => RoleTemplates.InSet("XYZ"));
    }

    [Fact]
    public void VaiTroMauCuvt_KhongKiemNhiem()
    {
        var byCode = RoleTemplates.Procurement.ToDictionary(t => t.Code, t => t.Permissions.ToHashSet());
        var approvals = ApprovalTypes.All.Select(t => t.ApprovePermission).ToHashSet();
        // Nhân viên Phòng CUVT lập RFQ, PO nhưng không duyệt; người duyệt các cấp không lập RFQ, PO, đề nghị mua.
        Assert.Empty(byCode["NVCUVT"].Intersect(approvals));
        Assert.Contains(Permissions.Purchase.PoCreate, byCode["NVCUVT"]);
        foreach (var code in new[] { "TRUONGPHONGBAN", "GDKHOI", "TPCUVT", "TPTCKT", "GDTCKT", "TPHC", "PHOCTUBCL" })
        {
            Assert.NotEmpty(byCode[code].Intersect(approvals));
            Assert.DoesNotContain(Permissions.Purchase.PoCreate, byCode[code]);
            Assert.DoesNotContain(Permissions.Purchase.PrCreate, byCode[code]);
            Assert.DoesNotContain(Permissions.Sourcing.RfqManage, byCode[code]);
        }
    }

    [Fact]
    public void CauHinhDuyetMauCuvt_DungChuoiBieuMau_NguoiDuyetDongChiChoPhieuCoBoPhan()
    {
        var byCode = RoleTemplates.InSet(RoleTemplateSets.Cuvt).ToDictionary(t => t.Code, t => t.Permissions.ToHashSet());
        var rules = ApprovalRuleTemplates.InSet(ApprovalRuleTemplateSets.Cuvt);
        foreach (var rule in rules)
        {
            var type = ApprovalTypes.Get(rule.DocType);
            Assert.False(rule.HeadOfficeOnly);
            Assert.False(string.IsNullOrWhiteSpace(rule.Note));
            if (rule.Kind == ApproverKind.Role)
            {
                Assert.True(byCode.ContainsKey(rule.RoleCode!), $"{rule.DocType} cấp {rule.Level}: vai trò {rule.RoleCode} không có trong bộ CUVT");
                Assert.Contains(type.ApprovePermission, byCode[rule.RoleCode!]);
            }
            else
            {
                Assert.Null(rule.RoleCode);
                Assert.True(type.HasDepartment, $"{rule.DocType}: người duyệt động chỉ cho phiếu có bộ phận (lát O1)");
            }
        }

        foreach (var group in rules.GroupBy(r => r.DocType))
            Assert.Equal(Enumerable.Range(1, group.Count()), group.Select(r => r.Level).Order());

        // Mục 19.3 (Thủ kho, TP HC nhóm B thêm ở lát Y3): 1A/1B/DV, 3a/3A/3B, 4A/4B.
        static IEnumerable<string> Chain(IEnumerable<ApprovalRuleTemplate> rs, string docType) =>
            rs.Where(r => r.DocType == docType).OrderBy(r => r.Level).Select(r => r.RoleCode ?? r.Kind.ToString());
        Assert.Equal(["DepartmentHead", "TPCUVT", "TPTCKT", "DivisionDirector", "PHOCTUBCL", "CHUTICH"], Chain(rules, "PR"));
        Assert.Equal(["TPCUVT", "TPTCKT", "GDTCKT", "PHOCTUBCL", "CHUTICH"], Chain(rules, "RFQ"));
        Assert.Equal(["TPCUVT", "TPTCKT", "GDTCKT", "CHUTICH"], Chain(rules, "PO"));

        // Trống = theo sơ đồ (G10) – giữ hành vi cũ của API.
        Assert.Same(ApprovalRuleTemplates.Procurement, ApprovalRuleTemplates.InSet(null));
        Assert.Throws<Proc.Domain.Common.BusinessRuleException>(() => ApprovalRuleTemplates.InSet("XYZ"));
    }
}
