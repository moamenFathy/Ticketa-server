using Ticketa.Core.DTOs;

namespace Ticketa.Core.Interfaces.IServices
{
  public interface IAuthApiService
  {
    Task<AuthResultDto> ConfirmEmailAsync(ConfirmEmailDto dto, CancellationToken ct = default);
    Task<AuthResultDto> GoogleAuthAsync(string idToken, CancellationToken ct = default);
    Task<ExternalLoginResultDto> GoogleMobileAuthAsync(string idToken, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task<(bool success, string? Error)> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    Task ResendEmailConfirmationAsync(string email, CancellationToken ct = default);
    Task<AuthResultDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    Task ForgetPasswordAsync(string email, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
  }
}
