using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PageResult<Notification>> GetAllAsync(int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.Notifications.OrderByDescending(n => n.CreatedAt).AsQueryable();

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PageResult<Notification>
            {
                Items = items,
                TotalCount = totalCount
            };
        }


        public async Task<Notification> GetByIdAsync(int id)
        {
            return await _context.Notifications.FindAsync(id);
        }

        public async Task<string> CreateAsync(NotificationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return "Notification Content is mandatory.";
            }

            if (dto.Status == "Active")
            {
                bool activeExists = await _context.Notifications.AnyAsync(n => n.Status == "Active");
                if (activeExists)
                {
                    return "Only one Active notification allowed. Make others Inactive first.";
                }
                   
            }

            var notification = new Notification
            {
                Content = dto.Content.Trim(),
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return null; // null = success
        }

        public async Task<string> UpdateAsync(int id, NotificationDto dto)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return "Notification not found.";

            if (string.IsNullOrWhiteSpace(dto.Content))
                return "Notification Content is mandatory.";

            if (dto.Status == "Active")
            {
                bool activeExists = await _context.Notifications.AnyAsync(n => n.Status == "Active" && n.Id != id);
                if (activeExists)
                    return "Only one Active notification allowed. Make others Inactive first.";
            }

            // Inactive notifications cannot be activated directly if another is active
            if (notification.Status == "Inactive" && dto.Status == "Active")
            {
                bool activeExists = await _context.Notifications.AnyAsync(n => n.Status == "Active");
                if (activeExists) return "Cannot activate. Another notification is already active.";
            }

            notification.Content = dto.Content.Trim();
            notification.Status = dto.Status;
            notification.UpdatedAt = DateTime.UtcNow;

            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();
            return null;
        }
    }
}
