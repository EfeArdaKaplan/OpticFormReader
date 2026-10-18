using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId}/Results")]
    [Authorize]
    public class ResultController : ControllerBase
    {
        private readonly IResultService _resultService;

        public ResultController(IResultService resultService)
        {
            _resultService = resultService;
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
        /// Sınavın sınıf başarı sıralamasını ve genel istatistiklerini getirir.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetResults(int examId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var results = await _resultService.GetExamResultsAsync(examId, userId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tek bir öğrencinin detaylı A4 karnesini ve soru soru baloncuk dökümünü getirir.
        /// </summary>
        [HttpGet("Student/{sheetId}")]
        public async Task<IActionResult> GetStudentReport(int examId, int sheetId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var report = await _resultService.GetStudentReportAsync(examId, sheetId, userId);
                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Şüpheli veya hatalı kağıt bilgilerini öğretmenin elle düzeltmesini ve anında yeniden puanlanmasını sağlar.
        /// </summary>
        [HttpPut("Student/{sheetId}/manual-correction")]
        public async Task<IActionResult> ManualCorrection(int examId, int sheetId, [FromBody] ManualCorrectionDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var updatedStudent = await _resultService.UpdateSheetManualAsync(examId, sheetId, userId, request);
                return Ok(updatedStudent);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}