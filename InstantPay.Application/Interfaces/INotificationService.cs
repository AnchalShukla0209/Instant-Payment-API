using InstantPay.SharedKernel.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface INotificationService
    {
        Task<PageResult<Notification>> GetAllAsync(int pageIndex = 1, int pageSize = 10);
        Task<Notification> GetByIdAsync(int id);
        Task<string> CreateAsync(NotificationDto dto);
        Task<string> UpdateAsync(int id, NotificationDto dto);
    }
}
