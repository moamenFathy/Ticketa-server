using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class EmailTemplatesTests
  {
    [Fact]
    public void VerificationCode_ContainsCodeAndHeader()
    {
      // Act
      var html = EmailTemplates.VerificationCode("849201");

      // Assert
      Assert.Contains("849201", html);
      Assert.Contains("Verify Your Email", html);
      Assert.Contains("Ticketa", html);
    }

    [Fact]
    public void PasswordReset_ContainsNameAndResetLink()
    {
      // Act
      var html = EmailTemplates.PasswordReset("Alice", "https://ticketa.com/reset-password?token=abc");

      // Assert
      Assert.Contains("Alice", html);
      Assert.Contains("https://ticketa.com/reset-password?token=abc", html);
    }

    [Fact]
    public void ForgotPassword_ContainsUserNameAndLink()
    {
      // Act
      var html = EmailTemplates.ForgotPassword("Bob", "https://ticketa.com/reset-password?token=xyz");

      // Assert
      Assert.Contains("Bob", html);
      Assert.Contains("https://ticketa.com/reset-password?token=xyz", html);
    }

    [Fact]
    public void BookingConfirmation_RendersMovieDetailsSeatsAndCid()
    {
      // Act
      var html = EmailTemplates.BookingConfirmation(
          "John",
          "Interstellar",
          new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc),
          "IMAX Hall",
          ["A1", "A2"],
          250m,
          "TKT-9999",
          "ticket-qr",
          "https://image.tmdb.org/t/p/w780/backdrop.jpg");

      // Assert
      Assert.Contains("Interstellar", html);
      Assert.Contains("IMAX Hall", html);
      Assert.Contains("A1, A2", html);
      Assert.Contains("TKT-9999", html);
      Assert.Contains("cid:ticket-qr", html);
      Assert.Contains("https://image.tmdb.org/t/p/w780/backdrop.jpg", html);
    }
  }
}
