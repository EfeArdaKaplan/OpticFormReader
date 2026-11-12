using core.DTOs;
using core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("AuthStrict")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeDto request)
        {
            try
            {
                await _authService.SendVerificationCodeAsync(request);
                return Ok(new { message = "6 haneli doğrulama kodu e-posta adresinize gönderildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-register")]
        public async Task<IActionResult> VerifyAndRegister([FromBody] VerifyRegistrationDto request)
        {
            try
            {
                var result = await _authService.VerifyAndRegisterAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password/send-code")]
        public async Task<IActionResult> SendPasswordResetCode([FromBody] ForgotPasswordRequestDto request)
        {
            try
            {
                await _authService.SendPasswordResetCodeAsync(request);
                return Ok(new { message = "6 haneli şifre sıfırlama kodu e-posta adresinize gönderildi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordConfirmDto request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { message = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Sliding Expiration: Kullanıcı aktif işlem yaptıkça token'ı arka planda sessizce yeniler.
        /// </summary>
        [Authorize]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(new { message = "Geçersiz oturum." });
                }

                var userId = int.Parse(userIdClaim);
                var result = await _authService.RefreshTokenAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
