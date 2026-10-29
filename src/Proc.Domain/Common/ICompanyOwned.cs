namespace Proc.Domain.Common;

/// <summary>
/// Entity thuộc về một công ty. Mỗi công ty là pháp nhân độc lập nên mọi bảng giao dịch có CompanyId (CLAUDE.md mục 4);
/// Infrastructure tự gắn global query filter theo công ty đang làm việc cho mọi entity cài interface này (design.md mục 9.2).
/// </summary>
public interface ICompanyOwned
{
    /// <summary>Công ty sở hữu bản ghi.</summary>
    Guid CompanyId { get; }
}
