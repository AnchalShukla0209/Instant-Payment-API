using InstantPay.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IAPICodeService
    {
        Task<List<APICodeDropdownDto>> GetAPICodesForDropdown();
    }
}
