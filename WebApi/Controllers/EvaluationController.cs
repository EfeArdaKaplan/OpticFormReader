using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId}/Evaluate")]
    [Authorize]
    public class EvaluationController : ControllerBase
    {
        private readonly IEvaluationService _evaluationService;

        public EvaluationController(IEvaluationService evaluationService)
        {
            _evaluationService = evaluationService;
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

        /// <summary>
        /// Sınavın tüm optik kağıtlarını çoklu çekirdekle paralel okur, değerlendirir ve puanlar.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Evaluate(int examId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetCurrentUserId();
                var summary = await _evaluationService.EvaluateExamAsync(examId, userId, cancellationToken);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Sınav değerlendirmesinin anlık ilerleme durumunu döner (% tamamlandı, okunan kağıt sayısı).
        /// </summary>
        [HttpGet("progress")]
        public IActionResult GetProgress(int examId)
        {
            var progress = _evaluationService.GetProgress(examId);
            return Ok(progress);
        }
    }
}