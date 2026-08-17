using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services;

public sealed class UserDropdownService : IUserDropdownService
{
    private readonly AppDbContext _context;

    public UserDropdownService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserDropdownDto>> GetWhiteLabelUsersAsync()
    {
        return await _context.TblWlUsers
            .AsNoTracking()
            .Where(user => user.Status == "Active")
            .OrderBy(user => user.CompanyName)
            .ThenBy(user => user.UserName)
            .Select(user => new UserDropdownDto
            {
                Id = user.Id,
                Name = (user.CompanyName ?? user.UserName ?? string.Empty)
                    + "-"
                    + (user.Phone ?? string.Empty),
                Username = user.UserName ?? string.Empty,
                Phone = user.Phone ?? string.Empty,
                UserType = "WL"
            })
            .ToListAsync();
    }

    public Task<List<UserDropdownDto>> GetAreaDistributorUsersAsync() => GetUsersByTypeAsync("AD");

    public Task<List<UserDropdownDto>> GetMasterDistributorUsersAsync() => GetUsersByTypeAsync("MD");

    public Task<List<UserDropdownDto>> GetSalesTeamUsersAsync() => GetUsersByTypeAsync("ST");

    public Task<List<UserDropdownDto>> GetRetailerUsersAsync() => GetUsersByTypeAsync("RT");

    private async Task<List<UserDropdownDto>> GetUsersByTypeAsync(string userType)
    {
        return await _context.TblUsers
            .AsNoTracking()
            .Where(user => user.Usertype == userType && user.Status == "Active")
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Username)
            .Select(user => new UserDropdownDto
            {
                Id = user.Id,
                Name = (user.Name ?? user.CompanyName ?? user.Username ?? string.Empty)
                    + "-"
                    + (user.Phone ?? string.Empty),
                Username = user.Username ?? string.Empty,
                Phone = user.Phone ?? string.Empty,
                UserType = user.Usertype ?? userType
            })
            .ToListAsync();
    }
}
