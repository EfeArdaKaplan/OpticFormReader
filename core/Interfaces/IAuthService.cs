using core.DTOs;

namespace core.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request);
        Task SendVerificationCodeAsync(SendVerificationCodeDto request);
        Task<LoginResponseDto> VerifyAndRegisterAsync(VerifyRegistrationDto request);
        Task SendPasswordResetCodeAsync(ForgotPasswordRequestDto request);
        Task ResetPasswordAsync(ResetPasswordConfirmDto request);
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
        Task<LoginResponseDto> RefreshTokenAsync(int userId);
    }
}
