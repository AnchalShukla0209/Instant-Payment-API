using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize);
}
