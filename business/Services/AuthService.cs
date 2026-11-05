using core.DTOs;
using core.Entities;
using core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace business.Services
{
    public class AuthService : IAuthService
    {
        private sealed record PendingRegistration(
            string Username,
            string Email,
            string PasswordHash,
            string Code,
            DateTime ExpiresAtUtc);

        private sealed record PendingPasswordReset(
            string Email,
            string Code,
            DateTime ExpiresAtUtc);

        private static readonly ConcurrentDictionary<string, PendingRegistration> PendingVerifications = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, PendingPasswordReset> PendingPasswordResets = new(StringComparer.OrdinalIgnoreCase);

        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task SendVerificationCodeAsync(SendVerificationCodeDto request)
        {
            if (request == null)
                throw new Exception("Geçersiz kayıt isteği.");

            var username = (request.Username ?? string.Empty).Trim();
            if (username.Length < 3)
                throw new Exception("Kullanıcı adı en az 3 karakter olmalıdır.");

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                throw new Exception("Geçerli bir e-posta adresi giriniz.");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new Exception("Şifre en az 6 karakter olmalıdır.");

            if (!string.Equals(request.Password, request.PasswordConfirm, StringComparison.Ordinal))
                throw new Exception("Girdiğiniz şifreler birbiriyle eşleşmiyor.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                throw new Exception("Bu e-posta adresi zaten kayıtlı.");
            }

            // Kriptografik olarak güvenli 6 haneli sayısal doğrulama kodu üret (100000 - 999999)
            string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Önce gerçek e-postayı gönder; gönderilemezse kayıt askıya alınmaz ve doğrudan hata fırlatılır
            await SendVerificationEmailAsync(normalizedEmail, username, code, isPasswordReset: false);

            PendingVerifications[normalizedEmail] = new PendingRegistration(
                Username: username,
                Email: normalizedEmail,
                PasswordHash: passwordHash,
                Code: code,
                ExpiresAtUtc: DateTime.UtcNow.AddMinutes(3));
        }

        public async Task<LoginResponseDto> VerifyAndRegisterAsync(VerifyRegistrationDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
                throw new Exception("Lütfen 6 haneli doğrulama kodunu eksiksiz giriniz.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var cleanCode = request.Code.Trim();

            if (!PendingVerifications.TryGetValue(normalizedEmail, out var pending))
            {
                throw new Exception("Doğrulama kaydı bulunamadı. Lütfen tekrar kod gönderin.");
            }

            if (DateTime.UtcNow > pending.ExpiresAtUtc)
            {
                PendingVerifications.TryRemove(normalizedEmail, out _);
                throw new Exception("Doğrulama kodunun 3 dakikalık süresi doldu. Lütfen yeni kod gönderin.");
            }

            if (!string.Equals(pending.Code, cleanCode, StringComparison.Ordinal))
            {
                throw new Exception("Girdiğiniz 6 haneli doğrulama kodu hatalı.");
            }

            PendingVerifications.TryRemove(normalizedEmail, out _);

            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                throw new Exception("Bu e-posta adresi zaten kayıtlı.");
            }

            var user = new User
            {
                FullName = pending.Username,
                Email = pending.Email,
                PasswordHash = pending.PasswordHash
            };

            await _userRepository.AddAsync(user);

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        public async Task SendPasswordResetCodeAsync(ForgotPasswordRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                throw new Exception("Lütfen geçerli bir e-posta adresi giriniz.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail)
                               ?? await _userRepository.GetByEmailAsync(request.Email.Trim());

            if (existingUser == null)
            {
                throw new Exception("Bu e-posta adresine ait kayıtlı bir hesap bulunamadı.");
            }

            string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

            await SendVerificationEmailAsync(
                normalizedEmail,
                existingUser.FullName,
                code,
                isPasswordReset: true);

            PendingPasswordResets[normalizedEmail] = new PendingPasswordReset(
                Email: normalizedEmail,
                Code: code,
                ExpiresAtUtc: DateTime.UtcNow.AddMinutes(3));
        }

        public async Task ResetPasswordAsync(ResetPasswordConfirmDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                throw new Exception("Geçersiz şifre sıfırlama isteği.");

            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length != 6)
                throw new Exception("Lütfen e-postanıza gelen 6 haneli doğrulama kodunu eksiksiz giriniz.");

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                throw new Exception("Yeni şifreniz en az 6 karakter olmalıdır.");

            if (!string.Equals(request.NewPassword, request.NewPasswordConfirm, StringComparison.Ordinal))
                throw new Exception("Girdiğiniz yeni şifreler birbiriyle eşleşmiyor.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var cleanCode = request.Code.Trim();

            if (!PendingPasswordResets.TryGetValue(normalizedEmail, out var pending))
            {
                throw new Exception("Şifre sıfırlama kodu bulunamadı. Lütfen tekrar kod gönderin.");
            }

            if (DateTime.UtcNow > pending.ExpiresAtUtc)
            {
                PendingPasswordResets.TryRemove(normalizedEmail, out _);
                throw new Exception("Doğrulama kodunun 3 dakikalık süresi doldu. Lütfen yeni kod gönderin.");
            }

            if (!string.Equals(pending.Code, cleanCode, StringComparison.Ordinal))
            {
                throw new Exception("Girdiğiniz 6 haneli doğrulama kodu hatalı.");
            }

            var user = await _userRepository.GetByEmailAsync(normalizedEmail)
                       ?? await _userRepository.GetByEmailAsync(request.Email.Trim());

            if (user == null)
            {
                PendingPasswordResets.TryRemove(normalizedEmail, out _);
                throw new Exception("Kullanıcı hesabı bulunamadı.");
            }

            PendingPasswordResets.TryRemove(normalizedEmail, out _);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _userRepository.UpdateAsync(user);
        }

        public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            if (request == null)
                throw new Exception("Geçersiz kayıt isteği.");

            string displayName = !string.IsNullOrWhiteSpace(request.Username)
                ? request.Username.Trim()
                : (request.FullName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length < 2)
                throw new Exception("Kullanıcı adı en az 2 karakter olmalıdır.");
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
                throw new Exception("Geçerli bir e-posta adresi giriniz.");
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new Exception("Şifre en az 6 karakter olmalıdır.");
            if (!string.IsNullOrEmpty(request.PasswordConfirm) && request.Password != request.PasswordConfirm)
                throw new Exception("Girdiğiniz şifreler birbiriyle eşleşmiyor.");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser != null)
            {
                throw new Exception("Bu e-posta adresi zaten kayıtlı.");
            }

            var user = new User
            {
                FullName = displayName,
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            await _userRepository.AddAsync(user);

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new Exception("E-posta veya şifre hatalı.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(normalizedEmail)
                       ?? await _userRepository.GetByEmailAsync(request.Email.Trim());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new Exception("E-posta veya şifre hatalı.");
            }

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        public async Task<LoginResponseDto> RefreshTokenAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("Kullanıcı bulunamadı veya oturum geçersiz.");
            }

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                Token = token,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        private async Task SendVerificationEmailAsync(string toEmail, string username, string code, bool isPasswordReset = false)
        {
            string host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
            int port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
            string senderEmail = _configuration["Smtp:Email"] ?? "info.optimatrix@gmail.com";
            string rawPass = _configuration["Smtp:Password"] ?? "ycftyweskebujbjo";
            string appPassword = rawPass.Replace(" ", "").Trim();
            string displayName = _configuration["Smtp:DisplayName"] ?? "OptiMatrix Doğrulama Merkezi";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, appPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            string actionDescription = isPasswordReset
                ? "OptiMatrix hesap şifrenizi yenilemek için aşağıdaki <strong>6 haneli şifre sıfırlama kodunu</strong> ekrandaki kutucuklara giriniz:"
                : "OptiMatrix hesabınızı oluşturmak için aşağıdaki <strong>6 haneli doğrulama kodunu</strong> ekrandaki kutucuklara giriniz:";

            string subjectTitle = isPasswordReset
                ? $"{code} - OptiMatrix Şifre Sıfırlama Kodunuz"
                : $"{code} - OptiMatrix E-Posta Doğrulama Kodunuz";

            string htmlBody = $@"
<!DOCTYPE html>
<html lang=""tr"">
<head><meta charset=""UTF-8""></head>
<body style=""margin:0;padding:0;background-color:#0b111d;font-family:'Segoe UI',Arial,sans-serif;"">
  <div style=""max-width:520px;margin:32px auto;background:#141e33;border:1px solid #1e293b;border-radius:14px;overflow:hidden;color:#f8fafc;"">
    <div style=""background:linear-gradient(135deg,#2563eb,#06b6d4);padding:24px;text-align:center;"">
      <h1 style=""margin:0;font-size:22px;font-weight:800;color:#ffffff;letter-spacing:0.5px;"">OptiMatrix Enterprise OMR</h1>
      <p style=""margin:6px 0 0;font-size:13px;color:#e0f2fe;"">Kurumsal Optik Okuyucu &amp; Sınav Değerlendirme Platformu</p>
    </div>
    <div style=""padding:28px 24px;text-align:center;"">
      <p style=""font-size:15px;color:#cbd5e1;margin:0 0 12px;"">Merhaba <strong style=""color:#ffffff;"">{WebUtility.HtmlEncode(username)}</strong>,</p>
      <p style=""font-size:14px;color:#94a3b8;line-height:1.5;margin:0 0 24px;"">
        {actionDescription}
      </p>
      <div style=""display:inline-block;background:#070a11;border:2px solid #3b82f6;border-radius:12px;padding:16px 28px;margin-bottom:20px;"">
        <span style=""font-family:'Courier New',monospace;font-size:32px;font-weight:800;letter-spacing:10px;color:#38bdf8;"">{code}</span>
      </div>
      <p style=""font-size:12px;color:#64748b;margin:0;"">
        ⏱️ Bu kod güvenlik nedeniyle <strong>3 dakika</strong> boyunca geçerlidir.<br>
        Bu işlemi siz yapmadıysanız bu e-postayı dikkate almayınız.
      </p>
    </div>
  </div>
</body>
</html>";

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, displayName, Encoding.UTF8),
                Subject = subjectTitle,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                throw new Exception($"Doğrulama e-postası gönderilemedi ({ex.Message}). Lütfen e-posta adresinizi kontrol ediniz.");
            }
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 20 dakika sınırlı geçerlilik süresi (Sliding refresh & Inactivity ile korunur)
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(20),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
