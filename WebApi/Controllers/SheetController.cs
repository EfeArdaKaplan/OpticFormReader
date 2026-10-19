using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId}/Sheets")]
    [Authorize]
    public class SheetController : ControllerBase
    {
        private readonly ISheetService _sheetService;
        private readonly IFileStorage _storage;

        public SheetController(ISheetService sheetService, IFileStorage storage)
        {
            _sheetService = sheetService;
            _storage = storage;
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
        /// Tekli, çoklu görsel veya çok sayfalı PDF yükleme (Kamera çekimi de buraya gelir)
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB toplam istek limiti
        public async Task<IActionResult> Upload(int examId, [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "Lütfen en az bir dosya seçin veya kameradan çekim yapın." });
            }

            try
            {
                var userId = GetCurrentUserId();

                var fileDtos = new List<UploadFileDto>();
                foreach (var file in files)
                {
                    fileDtos.Add(new UploadFileDto
                    {
                        Content = file.OpenReadStream(),
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        Length = file.Length
                    });
                }

                var result = await _sheetService.UploadSheetsAsync(examId, userId, fileDtos);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Sınava ait tüm optik kağıtları listeler
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSheets(int examId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sheets = await _sheetService.GetSheetsByExamIdAsync(examId, userId);
                return Ok(sheets);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Sınavın yükleme paketlerini (batch geçmişini) listeler
        /// </summary>
        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches(int examId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var batches = await _sheetService.GetBatchesByExamIdAsync(examId, userId);
                return Ok(batches);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tek bir optik kağıdın detayını getirir
        /// </summary>
        [HttpGet("{sheetId}")]
        public async Task<IActionResult> GetSheet(int examId, int sheetId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sheet = await _sheetService.GetSheetByIdAsync(sheetId, userId);
                if (sheet == null)
                {
                    return NotFound(new { message = "Optik form kağıdı bulunamadı." });
                }
                return Ok(sheet);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Optik kağıdın orijinal görüntüsünü döner (Frontend önizlemesi için)
        /// </summary>
        [HttpGet("{sheetId}/image")]
        public async Task<IActionResult> GetSheetImage(int examId, int sheetId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sheet = await _sheetService.GetSheetByIdAsync(sheetId, userId);
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.ImageKey))
                {
                    return NotFound(new { message = "Kağıt veya görsel bulunamadı." });
                }

                var stream = await _storage.OpenReadAsync(sheet.ImageKey);

                // Uzantıya göre doğru MIME tipini belirle
                var ext = Path.GetExtension(sheet.ImageKey).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".pdf" => "application/pdf",
                    _ => "application/octet-stream"
                };

                // Dosyayı inline olarak stream ediyoruz; böylece indirmek yerine doğrudan ekranda gösterilir
                return File(stream, contentType);
            }
            catch (FileNotFoundException)
            {
                return NotFound(new { message = "Depoda görsel dosyası bulunamadı." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// S3/SeaweedFS üzerinden doğrudan yüksek hızlı erişim için süreli imzalı URL döner (Zero-RAM streaming).
        /// </summary>
        [HttpGet("{sheetId}/presigned-url")]
        public async Task<IActionResult> GetPresignedUrl(int examId, int sheetId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var sheet = await _sheetService.GetSheetByIdAsync(sheetId, userId);
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.ImageKey))
                {
                    return NotFound(new { message = "Kağıt veya görsel bulunamadı." });
                }

                var url = _storage.GetPresignedUrl(sheet.ImageKey, TimeSpan.FromMinutes(30));
                return Ok(new { url, expiresMinutes = 30 });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Tek bir optik kağıdı siler
        /// </summary>
        [HttpDelete("{sheetId}")]
        public async Task<IActionResult> DeleteSheet(int examId, int sheetId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _sheetService.DeleteSheetAsync(sheetId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Yüklenen bir dosya paketini (batch) ve onun tüm kağıtlarını siler
        /// </summary>
        [HttpDelete("batches/{batchId}")]
        public async Task<IActionResult> DeleteBatch(int examId, int batchId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _sheetService.DeleteBatchAsync(batchId, userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}