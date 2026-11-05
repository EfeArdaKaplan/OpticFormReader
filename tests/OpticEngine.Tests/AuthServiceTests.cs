using business.Services;
using core.DTOs;
using core.Entities;
using core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace OpticEngine.Tests
{
    public class AuthServiceTests
    {
        private readonly FakeUserRepository _userRepo;
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _userRepo = new FakeUserRepository();

            var settings = new Dictionary<string, string?>
            {
                { "Jwt:Key", "super_secret_jwt_key_that_is_at_least_32_chars_long_12345" },
                { "Jwt:Issuer", "OpticalReaderApi" },
                { "Jwt:Audience", "OpticalReaderClient" }
            };

            _configuration = new TestConfiguration(settings);
            _authService = new AuthService(_userRepo, _configuration);
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

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.Equal("Kemal Sunal", result.FullName);
            Assert.Equal("kemal@example.com", result.Email);
            Assert.Single(_userRepo.Users);
            Assert.Equal("kemal@example.com", _userRepo.Users[0].Email);
            Assert.True(!string.IsNullOrEmpty(_userRepo.Users[0].PasswordHash));
        }

        [Fact]
        public async Task RegisterAsync_WithDuplicateEmail_ThrowsException()
        {
            // Arrange
            await _userRepo.AddAsync(new User
            {
                Id = 1,
                Email = "existing@example.com",
                FullName = "Existing User",
                PasswordHash = "hash"
            });

            var request = new RegisterRequestDto
            {
                FullName = "Kemal Sunal",
                Email = "existing@example.com",
                Password = "Password123!"
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _authService.RegisterAsync(request));
            Assert.Single(_userRepo.Users);
        }

        [Fact]
        public async Task LoginAsync_WithCorrectPassword_ReturnsValidJwtToken()
        {
            // Arrange
            string password = "CorrectPassword123!";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            await _userRepo.AddAsync(new User
            {
                Id = 10,
                FullName = "Şener Şen",
                Email = "sener@example.com",
                PasswordHash = hash
            });

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
            Assert.Equal("sener@example.com", result.Email);
            Assert.Equal("Şener Şen", result.FullName);
        }

        [Fact]
        public async Task LoginAsync_WithWrongPassword_ThrowsException()
        {
            // Arrange
            string hash = BCrypt.Net.BCrypt.HashPassword("RealPassword123!");
            await _userRepo.AddAsync(new User
            {
                Id = 10,
                FullName = "Şener Şen",
                Email = "sener@example.com",
                PasswordHash = hash
            });

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
            await _userRepo.AddAsync(new User
            {
                Id = 5,
                FullName = "İlyas Salman",
                Email = "ilyas@example.com",
                PasswordHash = "hashed"
            });

            // Act
            var result = await _authService.RefreshTokenAsync(5);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.Equal("İlyas Salman", result.FullName);
            Assert.Equal("ilyas@example.com", result.Email);
        }

        // Native, Zero-Dependency Test Fakes
        private class FakeUserRepository : IUserRepository
        {
            private readonly List<User> _users = new();

            public IReadOnlyList<User> Users => _users;

            public Task<User?> GetByIdAsync(int id) =>
                Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

            public Task<User?> GetByEmailAsync(string email) =>
                Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

            public Task<User> AddAsync(User user)
            {
                if (user.Id == 0)
                {
                    user.Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1;
                }
                _users.Add(user);
                return Task.FromResult(user);
            }

            public Task UpdateAsync(User user)
            {
                var idx = _users.FindIndex(u => u.Id == user.Id);
                if (idx >= 0) _users[idx] = user;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(int id)
            {
                _users.RemoveAll(u => u.Id == id);
                return Task.CompletedTask;
            }
        }

        private class TestConfiguration : IConfiguration
        {
            private readonly Dictionary<string, string?> _data;

            public TestConfiguration(Dictionary<string, string?> data)
            {
                _data = data;
            }

            public string? this[string key]
            {
                get => _data.TryGetValue(key, out var val) ? val : null;
                set => _data[key] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
            public IChangeToken GetReloadToken() => new NullChangeToken();
            public IConfigurationSection GetSection(string key) => throw new NotImplementedException();

            private class NullChangeToken : IChangeToken
            {
                public bool HasChanged => false;
                public bool ActiveChangeCallbacks => false;
                public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new NullDisposable();
                private class NullDisposable : IDisposable { public void Dispose() { } }
            }
        }
    }
}
