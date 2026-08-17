using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IUserDropdownService
{
    Task<List<UserDropdownDto>> GetWhiteLabelUsersAsync();
    Task<List<UserDropdownDto>> GetAreaDistributorUsersAsync();
    Task<List<UserDropdownDto>> GetMasterDistributorUsersAsync();
    Task<List<UserDropdownDto>> GetSalesTeamUsersAsync();
    Task<List<UserDropdownDto>> GetRetailerUsersAsync();
}
