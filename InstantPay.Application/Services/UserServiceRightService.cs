using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services;

public sealed class UserServiceRightService : IUserServiceRightService
{
    private readonly AppDbContext _context;

    public UserServiceRightService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsEnabledAsync(int userId, string serviceName)
    {
        var user = await _context.TblUsers
            .AsNoTracking()
            .Where(x => x.Id == userId && x.Status == "Active")
            .Select(x => new { x.RazorpayPayment, x.Settlement })
            .FirstOrDefaultAsync();

        if (user == null)
            return false;

        return serviceName.Trim().ToLowerInvariant() switch
        {
            "razorpaypayment" => string.Equals(user.RazorpayPayment, "Active", StringComparison.OrdinalIgnoreCase),
            "settlement" => string.Equals(user.Settlement, "Active", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
