using Proc.Domain.Common;
using Proc.Domain.Documents;

namespace Proc.Domain.Tests.Documents;

/// <summary>
/// Ý kiến thẩm định của thành viên chuyên môn (lát H1 – design.md mục 18.25): Đạt/Không đạt + nội dung, chỉ khi chủ còn mở, người lập không tự
/// thẩm định, người ghi rút lại có lý do.
/// </summary>
public class AppraisalOpinionTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 3, 0, 0, TimeSpan.Zero);

    private static AppraisalOwnerState Owner(bool isOpen = true) => new(Guid.NewGuid(), isOpen, Creator, "Phiếu GTH-TCT-2611-0001");

    private static AppraisalOpinion Give(AppraisalOwnerState? owner = null, Guid? author = null, string? content = "Hồ sơ năng lực đủ") =>
        AppraisalOpinion.Create(AppraisalOwnerTypes.MonthlySupplierApproval, Guid.NewGuid(), owner ?? Owner(), author ?? Author,
            AppraisalAspect.Quality, AppraisalResult.Pass, content);

    [Fact]
    public void GhiYKien_ChuConMo_LuuNoiDungDaCatKhoangTrang()
    {
        var opinion = Give(content: "  Mẫu thử đạt TCVN  ");
        Assert.Equal("Mẫu thử đạt TCVN", opinion.Content);
        Assert.Equal(AppraisalResult.Pass, opinion.Result);
        Assert.False(opinion.IsWithdrawn);
    }

    [Fact]
    public void GhiYKien_ChanChuDaQuyet_NguoiLapTuThamDinh_ThieuNoiDung_LoaiChuSai()
    {
        Assert.Contains("quyết định cuối cùng", Assert.Throws<BusinessRuleException>(() => Give(Owner(isOpen: false))).Message);
        Assert.Contains("Người lập", Assert.Throws<BusinessRuleException>(() => Give(author: Creator)).Message);
        Assert.Contains("nội dung", Assert.Throws<BusinessRuleException>(() => Give(content: "  ")).Message);
        Assert.Throws<BusinessRuleException>(() => Give(content: new string('x', AppraisalOpinion.ContentMaxLength + 1)));
        Assert.Throws<BusinessRuleException>(() => AppraisalOpinion.Create(
            AttachmentOwnerTypes.PurchaseOrder, Guid.NewGuid(), Owner(), Author, AppraisalAspect.Legal, AppraisalResult.Fail, "PO không thẩm định"));
        Assert.Throws<BusinessRuleException>(() => AppraisalOpinion.Create(
            AppraisalOwnerTypes.Rfq, Guid.NewGuid(), Owner(), Author, (AppraisalAspect)9, AppraisalResult.Fail, "Sai khía cạnh"));
    }

    [Fact]
    public void RutYKien_ChiNguoiGhi_BatBuocLyDo_KhiChuConMo()
    {
        var opinion = Give();
        opinion.CreatedBy = Author;
        Assert.Contains("người ghi", Assert.Throws<BusinessRuleException>(() => opinion.Withdraw(Owner(), Creator, "Sai", Now)).Message);
        Assert.Contains("lý do", Assert.Throws<BusinessRuleException>(() => opinion.Withdraw(Owner(), Author, " ", Now)).Message);
        Assert.Contains("quyết định cuối cùng",
            Assert.Throws<BusinessRuleException>(() => opinion.Withdraw(Owner(isOpen: false), Author, "Ghi nhầm", Now)).Message);

        opinion.Withdraw(Owner(), Author, "Ghi nhầm phiếu", Now);
        Assert.True(opinion.IsWithdrawn);
        Assert.Equal("Ghi nhầm phiếu", opinion.WithdrawReason);
        Assert.Equal(Now, opinion.WithdrawnAt);
        Assert.Contains("đã được rút", Assert.Throws<BusinessRuleException>(() => opinion.Withdraw(Owner(), Author, "Lần nữa", Now)).Message);
    }
}
