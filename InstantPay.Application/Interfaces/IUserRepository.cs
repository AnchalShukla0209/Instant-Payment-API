
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;

namespace InstantPay.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAndPasswordAsync(string username, string password);
        Task<TblUser?> GetUserByIdAsync(int userId);
        Task<bool> LogOtpLoginAsync(OtpLoginLogDto dto);
        Task<string> ResendOTPAsyncn(OtpLoginLogDto dto);
        Task<ServiceRightsData> GetUserRightsInfo(int Id);
    }
}
