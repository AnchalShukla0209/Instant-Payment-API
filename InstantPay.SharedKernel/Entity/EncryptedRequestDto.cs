using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class EncryptedRequestDto
    {
        public string ServiceName { get; set; }
    }

    public class OperatorDto
    {
        public int Id { get; set; }
        public string ServiceName { get; set; }
        public string OperatorName { get; set; }
        public string Spkey { get; set; }
        public string Picture { get; set; }
        public string Status { get; set; }
    }

}
