using Ticketa.Core.Entities;

namespace Ticketa.Tests.TestBuilders
{
  public class AppUserBuilder
  {
    private string _id = "user-123";
    private string _userName = "john@example.com";
    private string _email = "john@example.com";
    private bool _emailConfirmed = true;
    private string _firstName = "John";
    private string _lastName = "Doe";
    private DateOnly _dateOfBirth = new(1995, 5, 20);
    private string _theme = "light";
    private string? _verificationCode = null;
    private DateTime? _verificationCodeExpiry = null;
    private string? _refreshToken = "valid-refresh-token-xyz";
    private DateTime? _refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

    public AppUserBuilder WithId(string id)
    {
      _id = id;
      return this;
    }

    public AppUserBuilder WithEmail(string email)
    {
      _email = email;
      _userName = email;
      return this;
    }

    public AppUserBuilder WithFirstName(string firstName)
    {
      _firstName = firstName;
      return this;
    }

    public AppUserBuilder WithLastName(string lastName)
    {
      _lastName = lastName;
      return this;
    }

    public AppUserBuilder WithEmailConfirmed(bool confirmed)
    {
      _emailConfirmed = confirmed;
      return this;
    }

    public AppUserBuilder WithVerificationCode(string code, DateTime expiry)
    {
      _verificationCode = code;
      _verificationCodeExpiry = expiry;
      return this;
    }

    public AppUserBuilder WithRefreshToken(string? token, DateTime? expiry = null)
    {
      _refreshToken = token;
      _refreshTokenExpiry = expiry ?? (token != null ? DateTime.UtcNow.AddDays(7) : null);
      return this;
    }

    public AppUser Build()
    {
      return new AppUser
      {
        Id = _id,
        UserName = _userName,
        Email = _email,
        EmailConfirmed = _emailConfirmed,
        FirstName = _firstName,
        LastName = _lastName,
        DateOfBirth = _dateOfBirth,
        Theme = _theme,
        VerificationCode = _verificationCode,
        VerificationCodeExpiry = _verificationCodeExpiry,
        RefreshToken = _refreshToken,
        RefreshTokenExpiry = _refreshTokenExpiry
      };
    }
  }
}
