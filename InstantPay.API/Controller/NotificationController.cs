using InstantPay.Application.Interfaces;
using InstantPay.Application.Services;
using InstantPay.SharedKernel.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _service;
        public NotificationController(INotificationService mediator) => _service = mediator;

        [HttpGet("GetAllNotification")]
        public async Task<IActionResult> GetAll(int pageIndex = 1, int pageSize = 10)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var result = await _service.GetAllAsync(pageIndex, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var notification = await _service.GetByIdAsync(id);
            if (notification == null) return NotFound();
            return Ok(notification);
        }

        [HttpPost]
        public async Task<IActionResult> Create(NotificationDto dto)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var error = await _service.CreateAsync(dto);
            return Ok(error);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, NotificationDto dto)
        {
            var userId = Request.Headers["userid"].FirstOrDefault();
            var username = Request.Headers["username"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int uid))
            {
                return Unauthorized(new { message = "Invalid or missing userId" });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Invalid or missing username" });
            }
            var error = await _service.UpdateAsync(id, dto);
            return Ok(error);
        }
    }
}
