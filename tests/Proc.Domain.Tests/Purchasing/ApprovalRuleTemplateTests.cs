using Proc.Domain.Organization;
using Proc.Domain.Purchasing;

namespace Proc.Domain.Tests.Purchasing;

/// <summary>
/// Vai trò mẫu và cấu hình duyệt mẫu theo sơ đồ tổ chức & lưu đồ phê duyệt mua sắm (lát G10, design.md mục 18.25): mẫu phải tự nhất quán để
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
            new HashSet<string> { "CHUTICH", "TRUONGBANCUVT", "PHOBAN", "THAMDINHCL", "THAMDINHPL", "VPBDIEUPHOI", "VPBTIMNGUON", "CUCTRUONGPHONG", "CUCDATHANG", "DONVIDATHANG" });
    }

    [Fact]
    public void VaiTroMau_KhongKiemNhiem_TheoSoDo()
    {
        var byCode = RoleTemplates.Procurement.ToDictionary(t => t.Code, t => t.Permissions.ToHashSet());
        var approvals = ApprovalTypes.All.Select(t => t.ApprovePermission).ToHashSet();
        // Thành viên chuyên môn, Văn phòng Ban, nhân viên đặt hàng, đơn vị đặt hàng không có quyền duyệt nào (không biểu quyết, chỉ đề xuất).
        foreach (var code in new[] { "THAMDINHCL", "THAMDINHPL", "VPBDIEUPHOI", "VPBTIMNGUON", "CUCDATHANG", "DONVIDATHANG" })
            Assert.Empty(byCode[code].Intersect(approvals));
        // Văn phòng Ban không đặt hàng; Phòng cung ứng không quản lý NCC, không lập/sửa giá.
        Assert.DoesNotContain(Permissions.Purchase.PoCreate, byCode["VPBTIMNGUON"]);
        Assert.DoesNotContain(Permissions.Purchase.PoCreate, byCode["VPBDIEUPHOI"]);
        foreach (var code in new[] { "CUCTRUONGPHONG", "CUCDATHANG" })
        {
            Assert.DoesNotContain(Permissions.MasterData.SupplierManage, byCode[code]);
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
            Assert.True(byCode.ContainsKey(rule.RoleCode), $"{rule.DocType} cấp {rule.Level}: vai trò {rule.RoleCode} không có trong bộ mẫu");
            Assert.True(byCode[rule.RoleCode].Contains(type.ApprovePermission),
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
    }
}
