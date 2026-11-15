using System.Text.RegularExpressions;
using Proc.Domain.Common;
using Proc.Domain.Operations;
using Proc.Domain.Organization;

namespace Proc.Domain.Tests.Organization;

/// <summary>Lát QT2 – nhật ký lỗi: dấu vết gộp nhóm, chuẩn hóa đường dẫn / vị trí, mã theo dõi, trạng thái nhóm, tham số số ngày giữ.</summary>
public class ErrorLogTests
{
    [Fact]
    public void DuongDan_IdVaSo_ThanhChoTrong_BoQuery()
    {
        Assert.Equal("/api/purchase-orders/{id}/lines/{n}",
            ErrorFingerprint.NormalizePath("/api/Purchase-Orders/0193a3b2-1c2d-7e8f-9a0b-1c2d3e4f5a6b/lines/12?x=1#a"));
        Assert.Null(ErrorFingerprint.NormalizePath("  "));
    }

    [Fact]
    public void ViTriMayChu_LayDongCuaPhanMem_BoSoDong()
    {
        const string stack = """
               at Npgsql.NpgsqlConnector.ReadMessage()
               at Proc.Infrastructure.Purchasing.PoService.ApproveAsync(Guid id, CancellationToken ct) in D:\src\PoService.cs:line 120
               at Proc.Api.Endpoints.PoEndpoints.Approve()
            """;
        Assert.Equal("at Proc.Infrastructure.Purchasing.PoService.ApproveAsync(Guid id, CancellationToken ct)",
            ErrorFingerprint.ServerLocation(stack));
        Assert.Null(ErrorFingerprint.ServerLocation(null));
    }

    [Fact]
    public void ViTriGiaoDien_BoMayChu_SoDongCot_MaBamBanBuild()
    {
        var a = ErrorFingerprint.ClientLocation(
            "TypeError: x is undefined\n    at renderRow (http://muahang.local/assets/index-B3xk9Qa1.js:12:345)", "x is undefined");
        var b = ErrorFingerprint.ClientLocation(
            "TypeError: x is undefined\n    at renderRow (http://10.0.0.5:5000/assets/index-Zz81_q-a.js:99:1)", "x is undefined");
        Assert.Equal("at renderRow (/assets/index.js)", a);
        Assert.Equal(a, b);
        // Không có ngăn xếp: dùng nội dung, số thay bằng {n} – cùng lỗi khác số liệu vẫn một nhóm.
        Assert.Equal("Không đọc được dòng {n}", ErrorFingerprint.ClientLocation(null, "Không đọc được dòng 17"));
    }

    [Fact]
    public void DauVet_CungThongTin_CungKetQua_KhacNguon_KhacKetQua()
    {
        var a = ErrorFingerprint.Compute(ErrorSource.Server, "System.Exception", "at X()", "/api/a");
        Assert.Equal(64, a.Length);
        Assert.Equal(a, ErrorFingerprint.Compute(ErrorSource.Server, "System.Exception", "at X()", "/api/a"));
        Assert.NotEqual(a, ErrorFingerprint.Compute(ErrorSource.Client, "System.Exception", "at X()", "/api/a"));
    }

    [Fact]
    public void MaTheoDoi_Err_8KyTu_KhongCoChuDeNham()
    {
        for (var i = 0; i < 200; i++)
            Assert.Matches(new Regex("^ERR-[0-9A-HJKMNP-TV-Z]{8}$"), ErrorFingerprint.NewTrackingCode());
    }

    [Fact]
    public void NhomLoi_DoiTrangThai_GhiChuTrongGiuGhiChuCu()
    {
        var now = DateTimeOffset.UtcNow;
        var g = ErrorGroup.Create(new string('a', 64), ErrorSource.Server, "System.Exception", new string('x', 2000), null, "/api/a", now);
        Assert.Equal((ErrorGroupStatus.New, 1, ErrorGroup.TextMaxLength), (g.Status, g.OccurrenceCount, g.Message.Length));

        g.ChangeStatus(ErrorGroupStatus.InProgress, "Đang xem", "admin", now);
        g.ChangeStatus(ErrorGroupStatus.Resolved, " ", "admin", now);
        Assert.Equal((ErrorGroupStatus.Resolved, "Đang xem", "admin"), (g.Status, g.Note, g.HandledByName));
        Assert.Throws<BusinessRuleException>(() => g.ChangeStatus((ErrorGroupStatus)9, null, "admin", now));
        Assert.Throws<BusinessRuleException>(() => ErrorGroup.Create(" ", ErrorSource.Server, null, null, null, null, now));
    }

    [Fact]
    public void LanXayRa_ChiTietCat16KB()
    {
        var o = ErrorOccurrence.Create(Guid.CreateVersion7(), DateTimeOffset.UtcNow, "ERR-ABCDEFGH", ErrorSource.Client, null, null, null, null,
            "/a", null, "lỗi", new string('d', 20_000), "1.0.0", "Dev", null);
        Assert.Equal(ErrorOccurrence.DetailMaxLength, o.Detail!.Length);
    }

    [Fact]
    public void ThamSoSoNgayGiu_MacDinh90_GioiHan7Den3650()
    {
        var p = CompanyParameter.Create(Guid.CreateVersion7());
        Assert.Equal(90, p.ErrorLogKeepDays);
        p.ChangeErrorLogKeepDays(30);
        Assert.Equal(30, p.ErrorLogKeepDays);
        p.ChangeErrorLogKeepDays(null);
        Assert.Equal(CompanyParameter.DefaultErrorLogKeepDays, p.ErrorLogKeepDays);
        Assert.Throws<BusinessRuleException>(() => p.ChangeErrorLogKeepDays(6));
        Assert.Throws<BusinessRuleException>(() => p.ChangeErrorLogKeepDays(3651));
    }
}
