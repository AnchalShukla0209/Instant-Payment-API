using InstantPay.SharedKernel.RequestPayload.WhatsApp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<WhatsAppBroadcastResult> SendBroadcastMessageAsync(WhatsAppBroadcastRequest request);
    }
}
