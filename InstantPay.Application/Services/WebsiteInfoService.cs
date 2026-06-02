using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services
{
    public class WebsiteInfoService : IWebsiteInfoService
    {
        private readonly AppDbContext _context;

        public WebsiteInfoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<WebsiteInfoResponseDto?> GetWebsiteInfoByDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            var wlUser = await _context.TblWlUsers
                .FirstOrDefaultAsync(u => u.DomainName == domain);

            if (wlUser == null)
                return null;

            string logoFileName = !string.IsNullOrWhiteSpace(wlUser.logo_img)
                ? wlUser.logo_img
                : (!string.IsNullOrWhiteSpace(wlUser.Logo) ? wlUser.Logo : "logo__.png");

            string baseUrl = $"https://{domain}/assets/images/";
            string logoUrl = baseUrl + logoFileName;

            return new WebsiteInfoResponseDto
            {
                WlId = wlUser.Id.ToString(),
                LogoUrl = logoUrl
            };
        }
    }
}
