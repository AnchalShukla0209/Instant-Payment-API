using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize);
    public class PageResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
    }

}
