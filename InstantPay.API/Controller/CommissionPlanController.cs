using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstantPay.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommissionPlanController : ControllerBase
    {
        private readonly IPlanDetailService _planDetailService;
        private readonly ICommissionPlanService _commissionPlanService;

        public CommissionPlanController(
            IPlanDetailService planDetailService,
            ICommissionPlanService commissionPlanService)
        {
            _planDetailService = planDetailService;
            _commissionPlanService = commissionPlanService;
        }

        // PlanDetail Endpoints

        [HttpPost("plan/create")]
        public async Task<IActionResult> CreatePlanDetail([FromBody] CreatePlanDetailDto dto)
        {
            try
            {
                var result = await _planDetailService.CreatePlanDetail(dto);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("plan/update")]
        public async Task<IActionResult> UpdatePlanDetail([FromBody] UpdatePlanDetailDto dto)
        {
            try
            {
                var result = await _planDetailService.UpdatePlanDetail(dto);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("plan/{id}")]
        public async Task<IActionResult> GetPlanDetailById(int id)
        {
            try
            {
                var result = await _planDetailService.GetPlanDetailById(id);
                if (result == null)
                    return NotFound(new { success = false, message = "Plan not found" });

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("plan/dropdown")]
        public async Task<IActionResult> GetPlanDetailsForDropdown()
        {
            try
            {
                var result = await _planDetailService.GetPlanDetailsForDropdown();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("plan/list")]
        public async Task<IActionResult> GetPlanDetailsWithPagination([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            try
            {
                var (items, totalCount) = await _planDetailService.GetPlanDetailsWithPagination(pageNumber, pageSize, search);
                return Ok(new { success = true, data = items, totalCount, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("plan/{id}")]
        public async Task<IActionResult> DeletePlanDetail(int id)
        {
            try
            {
                var result = await _planDetailService.DeletePlanDetail(id);
                if (!result)
                    return NotFound(new { success = false, message = "Plan not found" });

                return Ok(new { success = true, message = "Plan deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // CommissionPlan Endpoints

        [HttpPost("create")]
        public async Task<IActionResult> CreateCommissionPlan([FromBody] CreateCommissionPlanDto dto)
        {
            try
            {
                var result = await _commissionPlanService.CreateCommissionPlan(dto);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateCommissionPlan([FromBody] UpdateCommissionPlanDto dto)
        {
            try
            {
                var result = await _commissionPlanService.UpdateCommissionPlan(dto);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCommissionPlanById(int id)
        {
            try
            {
                var result = await _commissionPlanService.GetCommissionPlanById(id);
                if (result == null)
                    return NotFound(new { success = false, message = "Commission plan not found" });

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("dropdown")]
        public async Task<IActionResult> GetCommissionPlansForDropdown()
        {
            try
            {
                var result = await _commissionPlanService.GetCommissionPlansForDropdown();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetCommissionPlansWithPagination([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            try
            {
                var (items, totalCount) = await _commissionPlanService.GetCommissionPlansWithPagination(pageNumber, pageSize, search);
                return Ok(new { success = true, data = items, totalCount, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCommissionPlan(int id)
        {
            try
            {
                var result = await _commissionPlanService.DeleteCommissionPlan(id);
                if (!result)
                    return NotFound(new { success = false, message = "Commission plan not found" });

                return Ok(new { success = true, message = "Commission plan deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
