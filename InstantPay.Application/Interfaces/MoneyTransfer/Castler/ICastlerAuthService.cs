using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.MoneyTransfer.Castler
{
    public interface ICastlerAuthService
    {
        Task<string> GenerateToken();
        Task<string> GetToken();
    }
}
