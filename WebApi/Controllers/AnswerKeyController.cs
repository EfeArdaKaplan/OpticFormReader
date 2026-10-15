using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId}/AnswerKey")]
    [Authorize]
    public class AnswerKeyController : ControllerBase
    {
        private readonly IAnswerKeyService _answerKeyService;
        public AnswerKeyController(IAnswerKeyService answerKeyService)
        {
            _answerKeyService = answerKeyService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("Kullanıcı kimliği Bulunamadi.");
            }
            return int.Parse(userIdClaim);
        }

        [HttpPut]
        public async Task<IActionResult> Save(int examId, [FromBody] SaveAnswerKeyDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _answerKeyService.SaveManualAsync(examId, userId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Get(int examId)
        {
            var userId = GetCurrentUserId();
            var result = await _answerKeyService.GetByExamIdAsync(examId, userId);
            if (result.Count == 0)
            {
                return NotFound(new { message = "Cevap anahtari bulunamadi." });
            }
            return Ok(result);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(int examId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _answerKeyService.DeleteAsync(examId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
