using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IAuthService
    {
        Task<UnlockResponseDto?> UnlockAsync(UnlockRequestDto request);
        string GenerateJwtToken(User user);
    }

}
