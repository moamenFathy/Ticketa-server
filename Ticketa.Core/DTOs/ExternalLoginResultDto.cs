namespace Ticketa.Core.DTOs
{
  public class ExternalLoginResultDto
  {
    public bool IsNewUser { get; set; }
    public bool HasPassword { get; set; }
    public string? Email { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
    public string? Message { get; set; }
  }
}