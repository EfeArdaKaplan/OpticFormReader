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
    public class ExamTemplateController : ControllerBase
    {
        private readonly IExamTemplateService _examTemplateService;

        public ExamTemplateController(IExamTemplateService examTemplateService)
        {
            _examTemplateService = examTemplateService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Kullanıcı kimliği bulunamadı.");
            }
            return int.Parse(userIdClaim);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamTemplateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _examTemplateService.CreateAsync(userId, request);
                return Ok(result);

            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyTemplates()
        {
            var userId = GetCurrentUserId();
            var templates = await _examTemplateService.GetMyTemplatesAsync(userId);
            return Ok(templates);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _examTemplateService.GetByIdAsync(id, userId);
            if (result == null)
            {
                return NotFound(new { message = "Sablon bulunamadi."});
            }
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateExamTemplateDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _examTemplateService.UpdateAsync(id, userId, request);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _examTemplateService.DeleteAsync(id, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
