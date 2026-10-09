using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExamController : ControllerBase
    {
        private readonly IExamService _examService;

        public ExamController(IExamService examService)
        {
            _examService = examService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Kullanici kimligi bulunamadi.");
            }
            return int.Parse(userIdClaim);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _examService.CreateAsync(userId, request);
                return Ok(result);
            } catch(Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyExams()
        {
            var userId = GetCurrentUserId();
            var result = await _examService.GetMyExamsAsync(userId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _examService.GetByIdAsync(id, userId);
            if (result == null)
            {
                return NotFound( new { message = "Sınav bulunamadı." });
            }
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _examService.DeleteAsync(id, userId);
                return NoContent();
            } catch(Exception Ex)
            {
                return BadRequest(new { message = Ex.Message });
            }
        }
    }
}
