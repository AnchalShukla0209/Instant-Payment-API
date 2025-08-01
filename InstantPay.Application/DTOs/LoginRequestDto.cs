using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.DTOs
{
    public class LoginRequestDto
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    public class EncryptedRequest
    {
        public string Data { get; set; }
    }

    public class OperatorDTORequest
    {
        public string ServiceName { get; set; }
    }
}
