namespace InstantPay.Application.Interfaces;

public interface IUserServiceRightService
{
    Task<bool> IsEnabledAsync(int userId, string serviceName);
}
