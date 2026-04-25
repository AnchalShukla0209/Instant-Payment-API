using InstantPay.SharedKernel.RequestPayload.DebitCredit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces.SMS
{
    public interface ISmsService
    {
        Task<bool> SendDebitCreditSmsAsync(DebitCreditSmsRequest data);
    }
}
