using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class BalanceInquiryRequestDto
    {
        public string AgentLoginId { get; set; }         // agentId
        public string AgentPin { get; set; }             // agentPin
        public string AadhaarNumber { get; set; }        // customer aadhaar
        public string BankId { get; set; }               // bank id
        public string BankName { get; set; }             // bank name
        public string Latitude { get; set; }             // latitude as string
        public string Longitude { get; set; }            // longitude as string
        public string MobileNumber { get; set; }         // customer mobile
        public string PidXml { get; set; }               // fingerprint XML
        public string AccessToken { get; set; }          // x-app-access-token
        public string AppIdentifierToken { get; set; }   // x-appid-token
        public string UserId { get; set; }               // for TransactionDetail.UserId
        public string ComingFrom { get; set; } = "WEB";  // e.g. WEB, APP
        public int? ServiceId { get; set; }              // optional
    }
}
