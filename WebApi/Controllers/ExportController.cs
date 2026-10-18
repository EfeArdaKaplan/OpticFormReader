using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId}/Export")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly IExportService _exportService;

        public ExportController(IExportService exportService)
        {
            _exportService = exportService;
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
        /// Sınav sonuçlarını Excel uyumlu CSV dosyası olarak indirir.
        /// </summary>
        [HttpGet("csv")]
        public async Task<IActionResult> ExportCsv(int examId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var (fileBytes, fileName, contentType) = await _exportService.ExportResultsCsvAsync(examId, userId);
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}