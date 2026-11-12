using core.interfaces;
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
        private readonly IExamRepository _examRepository;

        public EvaluationController(IEvaluationService evaluationService, IExamRepository examRepository)
        {
            _evaluationService = evaluationService;
            _examRepository = examRepository;
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
        public async Task<IActionResult> GetProgress(int examId)
        {
            var userId = GetCurrentUserId();
            var exam = await _examRepository.GetByIdAsync(examId);
            if (exam == null || exam.OwnerUserId != userId)
            {
                return NotFound(new { message = "Sınav bulunamadı veya erişim yetkiniz yok." });
            }

            var progress = _evaluationService.GetProgress(examId);
            return Ok(progress);
        }
    }
}
