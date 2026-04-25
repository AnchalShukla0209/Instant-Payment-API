using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.RequestPayload
{
    public class InsuranceFetchRequestDto
    {
        public string PolicyNumber { get; set; }   // tel
        public string Operator { get; set; }       // optr
        public string Email { get; set; }           // optional
        public string Dob { get; set; }             // DD/MM/YYYY
        public string optional { get; set; } = "";
    }
}
