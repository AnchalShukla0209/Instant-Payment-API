using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity.NIFIConfigDTO
{
    public class NIFIConfig
    {
        public string? ClientId { get; set; }
        public string? ApiKey { get; set; }
        public string? EncryptionKey { get; set; }
        public string? EncryptionIv { get; set; }
        public string[]? UsersIp { get; set; }
    }
}
