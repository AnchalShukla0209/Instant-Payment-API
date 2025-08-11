using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class SlabInfoDto
    {
        public int Id { get; init; }
        public string ServiceName { get; init; } = string.Empty;
        public string SlabName { get; init; } = string.Empty;
        public decimal IPShare { get; init; }
        public decimal WLShare { get; init; }
        public string CommissionType { get; init; } = string.Empty;
    }
}
