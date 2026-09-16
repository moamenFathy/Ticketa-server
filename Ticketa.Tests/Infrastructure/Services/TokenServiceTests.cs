using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Ticketa.Core.Entities;
using Ticketa.Core.Helpers;
using Ticketa.Core.Settings;
using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class TokenServiceTests
  {
    private readonly TokenService _sut;
    private readonly JwtSettings _jwtSettings;

    public TokenServiceTests()
    {
      _jwtSettings = new JwtSettings
      {
        SecretKey = "a-very-secure-secret-key-with-sufficient-length-for-hmac-sha256",
        Issuer = "TicketaIssuer",
        Audience = "TicketaAudience",
        ExpiryMinutes = 15,
        RefreshTokenExpiryDate = 7
      };

      _sut = new TokenService(Options.Create(_jwtSettings));
    }

    [Fact]
    public void GenerateAccessToken_EmbedsUserIdNameEmailAndRoles()
    {
      // Arrange
      var user = new AppUser
      {
        Id = "user-123",
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com"
      };
      var roles = new List<string> { "Admin", "Scheduler" };
      var permissions = new List<string> { Permissions.Movies.Import, Permissions.Payments.Refund };

      // Act
      var tokenString = _sut.GenerateAccessToken(user, roles, permissions);

      // Assert
      Assert.NotNull(tokenString);
      var handler = new JwtSecurityTokenHandler();
      var jwtToken = handler.ReadJwtToken(tokenString);

      Assert.Equal(_jwtSettings.Issuer, jwtToken.Issuer);
      Assert.Contains(_jwtSettings.Audience, jwtToken.Audiences);

      var claims = jwtToken.Claims.ToList();
      Assert.Contains(claims, c => c.Type == "uid" && c.Value == "user-123");
      Assert.Contains(claims, c => c.Type == "name" && c.Value == "John Doe");
      Assert.Contains(claims, c => c.Type == "email" && c.Value == "john@example.com");

      // Roles
      Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
      Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Scheduler");

      // Permissions
      Assert.Contains(claims, c => c.Type == "permission" && c.Value == Permissions.Movies.Import);
      Assert.Contains(claims, c => c.Type == "permission" && c.Value == Permissions.Payments.Refund);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64StringWithExpectedLength()
    {
      // Act
      var token1 = _sut.GenerateRefreshToken();
      var token2 = _sut.GenerateRefreshToken();

      // Assert
      Assert.NotNull(token1);
      Assert.NotNull(token2);
      Assert.NotEqual(token1, token2); // Cryptographically random

      var bytes = Convert.FromBase64String(token1);
      Assert.Equal(64, bytes.Length);
    }
  }
}
