using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces
{
    public interface IWebsiteInfoService
    {
        Task<WebsiteInfoResponseDto?> GetWebsiteInfoByDomainAsync(string domain);
    }
}
