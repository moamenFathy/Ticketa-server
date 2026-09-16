using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Interfaces.IServices;
using Ticketa.Core.Settings;
using Ticketa.Infrastructure.Service;
using Ticketa.Tests.Helpers;
using Ticketa.Tests.TestBuilders;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class AuthApiServiceTests
  {
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<UserManager<AppUser>> _mockUserManager;
    private readonly Mock<RoleManager<AppRole>> _mockRoleManager;
    private readonly IOptions<JwtSettings> _jwtSettingsOptions;
    private readonly IConfiguration _configuration;
    private readonly List<AppUser> _userStore;
    private readonly AuthApiService _sut;

    private const string DefaultEmail = "user@example.com";
    private const string DefaultPassword = "Password123!";
    private const string DefaultAccessToken = "jwt-access-token-abc";
    private const string DefaultRefreshToken = "refresh-token-xyz";

    public AuthApiServiceTests()
    {
      _mockEmailService = new Mock<IEmailService>();
      _mockTokenService = new Mock<ITokenService>();
      _userStore = new List<AppUser>();
      _mockUserManager = IdentityMockHelper.MockUserManager(_userStore);
      _mockRoleManager = IdentityMockHelper.MockRoleManager();

      var jwtSettings = new JwtSettings
      {
        SecretKey = "super-secret-jwt-key-for-testing-purposes",
        Issuer = "TicketaTest",
        Audience = "TicketaTestAudience",
        ExpiryMinutes = 15,
        RefreshTokenExpiryDate = 7
      };
      _jwtSettingsOptions = Options.Create(jwtSettings);

      var inMemoryConfig = new Dictionary<string, string?>
      {
        { "ClientSettings:BaseUrl", "http://localhost:5173" }
      };
      _configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(inMemoryConfig)
          .Build();

      _mockTokenService
          .Setup(t => t.GenerateAccessToken(It.IsAny<AppUser>(), It.IsAny<IList<string>>(), It.IsAny<IList<string>>()))
          .Returns(DefaultAccessToken);

      _mockTokenService
          .Setup(t => t.GenerateRefreshToken())
          .Returns(DefaultRefreshToken);

      _sut = new AuthApiService(
          _mockEmailService.Object,
          _mockTokenService.Object,
          _mockUserManager.Object,
          _mockRoleManager.Object,
          _jwtSettingsOptions,
          _configuration);
    }

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_WhenNewUser_CreatesUserGeneratesOtpAndSendsEmail()
    {
      // Arrange
      var dto = new RegisterDto
      {
        Email = DefaultEmail,
        Password = DefaultPassword,
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateOnly(1995, 5, 20)
      };

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync((AppUser?)null);

      AppUser? capturedCreatedUser = null;
      _mockUserManager
          .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), dto.Password))
          .Callback<AppUser, string>((u, _) => capturedCreatedUser = u)
          .ReturnsAsync(IdentityResult.Success);

      // Act
      var (success, error) = await _sut.RegisterAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Null(error);

      Assert.NotNull(capturedCreatedUser);
      Assert.Equal(dto.Email, capturedCreatedUser.Email);
      Assert.NotNull(capturedCreatedUser.VerificationCode);
      Assert.Equal(6, capturedCreatedUser.VerificationCode.Length);
      Assert.NotNull(capturedCreatedUser.VerificationCodeExpiry);

      _mockEmailService.Verify(e => e.SendEmailAsync(
          dto.Email,
          "Verify Your Email",
          It.Is<string>(body => body.Contains(capturedCreatedUser.VerificationCode))), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenExistingUserAlreadyConfirmed_ReturnsFailureAndNeverSendsOtp()
    {
      // Arrange
      var dto = new RegisterDto { Email = DefaultEmail, Password = DefaultPassword };
      var existingUser = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(true).Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync(existingUser);

      // Act
      var (success, error) = await _sut.RegisterAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Equal("Email is already registered and confirmed.", error);

      _mockUserManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
      _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenExistingUserUnconfirmed_ResendsOtpAndReturnsSuccess()
    {
      // Arrange (Anti-enumeration / Resend on unconfirmed account)
      var dto = new RegisterDto { Email = DefaultEmail, Password = DefaultPassword };
      var existingUser = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(false).Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync(existingUser);

      // Act
      var (success, error) = await _sut.RegisterAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Null(error);

      Assert.NotNull(existingUser.VerificationCode);
      Assert.NotNull(existingUser.VerificationCodeExpiry);

      _mockUserManager.Verify(m => m.UpdateAsync(existingUser), Times.Once);
      _mockEmailService.Verify(e => e.SendEmailAsync(dto.Email, "Verify Your Email", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenUserManagerCreateFails_ReturnsFailureWithIdentityErrors()
    {
      // Arrange
      var dto = new RegisterDto { Email = DefaultEmail, Password = "weak" };

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync((AppUser?)null);

      var identityError = new IdentityError { Description = "Password must have at least 8 characters." };
      _mockUserManager
          .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), dto.Password))
          .ReturnsAsync(IdentityResult.Failed(identityError));

      // Act
      var (success, error) = await _sut.RegisterAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Equal("Password must have at least 8 characters.", error);
      _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region ConfirmEmailAsync Tests

    [Fact]
    public async Task ConfirmEmailAsync_WhenUserNotFound_ReturnsFailure()
    {
      // Arrange
      var dto = new ConfirmEmailDto { Email = DefaultEmail, Code = "123456" };
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync((AppUser?)null);

      // Act
      var result = await _sut.ConfirmEmailAsync(dto);

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid or expired verification code", result.Message);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenCodeMismatch_ReturnsFailureAndLeavesEmailUnconfirmed()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithEmailConfirmed(false)
          .WithVerificationCode("654321", DateTime.UtcNow.AddMinutes(10))
          .Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      var dto = new ConfirmEmailDto { Email = DefaultEmail, Code = "111111" };

      // Act
      var result = await _sut.ConfirmEmailAsync(dto);

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid or expired verification code", result.Message);
      Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenCodeExpired_ReturnsFailure()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithEmailConfirmed(false)
          .WithVerificationCode("123456", DateTime.UtcNow.AddMinutes(-5)) // Expired 5 mins ago
          .Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      var dto = new ConfirmEmailDto { Email = DefaultEmail, Code = "123456" };

      // Act
      var result = await _sut.ConfirmEmailAsync(dto);

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid or expired verification code", result.Message);
      Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WhenCodeValid_ActivatesAccountAndIssuesTokens()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithEmailConfirmed(false)
          .WithVerificationCode("123456", DateTime.UtcNow.AddMinutes(10))
          .Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      var dto = new ConfirmEmailDto { Email = DefaultEmail, Code = "123456" };

      // Act
      var result = await _sut.ConfirmEmailAsync(dto);

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(DefaultAccessToken, result.AccessToken);
      Assert.Equal(DefaultRefreshToken, result.RefreshToken);

      Assert.True(user.EmailConfirmed);
      Assert.Null(user.VerificationCode);
      Assert.Null(user.VerificationCodeExpiry);
      Assert.Equal(DefaultRefreshToken, user.RefreshToken);
      Assert.NotNull(user.RefreshTokenExpiry);

      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Exactly(2));
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WhenUserNotFoundOrPasswordInvalid_ReturnsFailure()
    {
      // Arrange
      var dto = new LoginDto { Email = DefaultEmail, Password = "WrongPassword" };
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync((AppUser?)null);

      // Act
      var result = await _sut.LoginAsync(dto);

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid email or password", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenEmailNotConfirmed_ReturnsEmailNotConfirmedFailure()
    {
      // Arrange
      var dto = new LoginDto { Email = DefaultEmail, Password = DefaultPassword };
      var user = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(false).Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.CheckPasswordAsync(user, dto.Password))
          .ReturnsAsync(true);

      // Act
      var result = await _sut.LoginAsync(dto);

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Email not confirmed. Please check your inbox.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValidAndConfirmed_ReturnsSuccessWithTokens()
    {
      // Arrange
      var dto = new LoginDto { Email = DefaultEmail, Password = DefaultPassword };
      var user = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(true).Build();

      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.CheckPasswordAsync(user, dto.Password))
          .ReturnsAsync(true);

      // Act
      var result = await _sut.LoginAsync(dto);

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(DefaultAccessToken, result.AccessToken);
      Assert.Equal(DefaultRefreshToken, result.RefreshToken);

      Assert.Equal(DefaultRefreshToken, user.RefreshToken);
      Assert.NotNull(user.RefreshTokenExpiry);

      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    #endregion

    #region RefreshTokenAsync & LogoutAsync Tests

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenNotFound_ReturnsFailure()
    {
      // Arrange: User store is empty
      // Act
      var result = await _sut.RefreshTokenAsync("unknown-refresh-token");

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid or expired refresh token", result.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenExpired_ReturnsFailure()
    {
      // Arrange
      var expiredUser = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithRefreshToken("expired-token", DateTime.UtcNow.AddDays(-1))
          .Build();
      _userStore.Add(expiredUser);

      // Act
      var result = await _sut.RefreshTokenAsync("expired-token");

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid or expired refresh token", result.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenValid_RotatesRefreshTokenAndReturnsSuccess()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithRefreshToken("valid-old-token", DateTime.UtcNow.AddDays(3))
          .Build();
      _userStore.Add(user);

      // Act
      var result = await _sut.RefreshTokenAsync("valid-old-token");

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(DefaultAccessToken, result.AccessToken);
      Assert.Equal(DefaultRefreshToken, result.RefreshToken);

      Assert.Equal(DefaultRefreshToken, user.RefreshToken);
      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenValid_ClearsRefreshTokenAndExpiry()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithEmail(DefaultEmail)
          .WithRefreshToken("token-to-logout", DateTime.UtcNow.AddDays(7))
          .Build();
      _userStore.Add(user);

      // Act
      await _sut.LogoutAsync("token-to-logout");

      // Assert
      Assert.Null(user.RefreshToken);
      Assert.Null(user.RefreshTokenExpiry);
      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenNullOrWhitespace_ReturnsSilently()
    {
      // Act
      await _sut.LogoutAsync(null);
      await _sut.LogoutAsync("");
      await _sut.LogoutAsync("   ");

      // Assert
      _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    #endregion

    #region Google Auth Tests

    [Fact]
    public async Task GoogleAuthAsync_WhenTokenInvalid_ReturnsFailure()
    {
      // Act: Passing an invalid raw string token will cause Google validation to throw
      var result = await _sut.GoogleAuthAsync("invalid-google-token-xyz");

      // Assert
      Assert.False(result.Succeeded);
      Assert.Equal("Invalid Google token.", result.Message);
    }

    [Fact]
    public async Task GoogleMobileAuthAsync_WhenTokenInvalid_ReturnsFailure()
    {
      // Act
      var result = await _sut.GoogleMobileAuthAsync("invalid-mobile-token-xyz");

      // Assert
      Assert.Equal("Invalid Google token.", result.Message);
    }

    #endregion

    #region ResendEmailConfirmationAsync Tests

    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenUserExistsAndUnconfirmed_SendsNewOtp()
    {
      // Arrange
      var user = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(false).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      // Act
      await _sut.ResendEmailConfirmationAsync(DefaultEmail);

      // Assert
      Assert.NotNull(user.VerificationCode);
      Assert.NotNull(user.VerificationCodeExpiry);
      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
      _mockEmailService.Verify(e => e.SendEmailAsync(DefaultEmail, "Verify Your Email", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResendEmailConfirmationAsync_WhenUserNotFoundOrAlreadyConfirmed_ReturnsSilently()
    {
      // Arrange: Confirmed user
      var confirmedUser = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(true).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(confirmedUser);

      // Act
      await _sut.ResendEmailConfirmationAsync(DefaultEmail);
      await _sut.ResendEmailConfirmationAsync("unknown@example.com");

      // Assert
      _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region ForgetPasswordAsync & ResetPasswordAsync Tests

    [Fact]
    public async Task ForgetPasswordAsync_WhenUserExistsAndConfirmed_GeneratesResetLinkAndSendsEmail()
    {
      // Arrange
      var user = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(true).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.GeneratePasswordResetTokenAsync(user))
          .ReturnsAsync("raw-reset-token-123");

      // Act
      await _sut.ForgetPasswordAsync(DefaultEmail);

      // Assert
      _mockEmailService.Verify(e => e.SendEmailAsync(
          DefaultEmail,
          "Reset Your Password",
          It.Is<string>(html => html.Contains("reset-password") && html.Contains(Uri.EscapeDataString(DefaultEmail)))), Times.Once);
    }

    [Fact]
    public async Task ForgetPasswordAsync_WhenUserNotFoundOrUnconfirmed_ReturnsSilently()
    {
      // Arrange: Unconfirmed user
      var unconfirmedUser = new AppUserBuilder().WithEmail(DefaultEmail).WithEmailConfirmed(false).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(unconfirmedUser);

      // Act
      await _sut.ForgetPasswordAsync(DefaultEmail);
      await _sut.ForgetPasswordAsync("nonexistent@example.com");

      // Assert
      _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenUserNotFound_ReturnsFailure()
    {
      // Arrange
      var dto = new ResetPasswordDto { Email = "unknown@example.com", Token = "token", NewPassword = "NewPassword123!" };
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(dto.Email))
          .ReturnsAsync((AppUser?)null);

      // Act
      var (success, error) = await _sut.ResetPasswordAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Equal("Invalid email address.", error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenTokenValid_ResetsPasswordAndReturnsSuccess()
    {
      // Arrange
      var rawToken = "valid-token-123";
      var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

      var user = new AppUserBuilder().WithEmail(DefaultEmail).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.ResetPasswordAsync(user, rawToken, "NewSecurePassword123!"))
          .ReturnsAsync(IdentityResult.Success);

      var dto = new ResetPasswordDto
      {
        Email = DefaultEmail,
        Token = encodedToken,
        NewPassword = "NewSecurePassword123!"
      };

      // Act
      var (success, error) = await _sut.ResetPasswordAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Null(error);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenIdentityResetFails_ReturnsFailureWithDescription()
    {
      // Arrange
      var rawToken = "invalid-token";
      var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

      var user = new AppUserBuilder().WithEmail(DefaultEmail).Build();
      _mockUserManager
          .Setup(m => m.FindByEmailAsync(DefaultEmail))
          .ReturnsAsync(user);

      var identityError = new IdentityError { Description = "Invalid token." };
      _mockUserManager
          .Setup(m => m.ResetPasswordAsync(user, rawToken, "NewPassword123!"))
          .ReturnsAsync(IdentityResult.Failed(identityError));

      var dto = new ResetPasswordDto
      {
        Email = DefaultEmail,
        Token = encodedToken,
        NewPassword = "NewPassword123!"
      };

      // Act
      var (success, error) = await _sut.ResetPasswordAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Equal("Invalid token.", error);
    }

    #endregion
  }
}
