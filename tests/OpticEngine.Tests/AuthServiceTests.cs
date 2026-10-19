using business.Services;
using core.DTOs;
using core.Entities;
using core.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace OpticEngine.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _userRepoMock = new Mock<IUserRepository>();

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Jwt:Key", "super_secret_jwt_key_that_is_at_least_32_chars_long_12345" },
                { "Jwt:Issuer", "OpticalReaderApi" },
                { "Jwt:Audience", "OpticalReaderClient" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _authService = new AuthService(_userRepoMock.Object, _configuration);
        }

        [Fact]
        public async Task RegisterAsync_WithNewEmail_ReturnsValidTokenAndResponse()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                FullName = "Kemal Sunal",
                Email = "kemal@example.com",
                Password = "Password123!"
            };

            _userRepoMock.Setup(r => r.GetByEmailAsync(request.Email))
                .ReturnsAsync((User?)null);

            _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.Equal("Kemal Sunal", result.FullName);
            Assert.Equal("kemal@example.com", result.Email);
            _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u => u.Email == request.Email && !string.IsNullOrEmpty(u.PasswordHash))), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WithDuplicateEmail_ThrowsException()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                FullName = "Kemal Sunal",
                Email = "existing@example.com",
                Password = "Password123!"
            };

            _userRepoMock.Setup(r => r.GetByEmailAsync(request.Email))
                .ReturnsAsync(new User { Id = 1, Email = request.Email, FullName = "Existing User" });

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _authService.RegisterAsync(request));
            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WithCorrectPassword_ReturnsValidJwtToken()
        {
            // Arrange
            string password = "CorrectPassword123!";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Id = 10,
                FullName = "Şener Şen",
                Email = "sener@example.com",
                PasswordHash = hash
            };

            _userRepoMock.Setup(r => r.GetByEmailAsync(user.Email))
                .ReturnsAsync(user);

            var request = new LoginRequestDto
            {
                Email = "sener@example.com",
                Password = password
            };

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task LoginAsync_WithWrongPassword_ThrowsException()
        {
            // Arrange
            string hash = BCrypt.Net.BCrypt.HashPassword("RealPassword123!");
            var user = new User
            {
                Id = 10,
                FullName = "Şener Şen",
                Email = "sener@example.com",
                PasswordHash = hash
            };

            _userRepoMock.Setup(r => r.GetByEmailAsync(user.Email))
                .ReturnsAsync(user);

            var request = new LoginRequestDto
            {
                Email = "sener@example.com",
                Password = "WrongPassword999!"
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _authService.LoginAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_WithValidUserId_ReturnsRefreshedToken()
        {
            // Arrange
            var user = new User
            {
                Id = 5,
                FullName = "İlyas Salman",
                Email = "ilyas@example.com",
                PasswordHash = "hashed"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(5))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.RefreshTokenAsync(5);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.Equal("İlyas Salman", result.FullName);
        }
    }
}
