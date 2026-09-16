using System.ComponentModel.DataAnnotations;

namespace Ticketa.Core.DTOs
{
  public class RefreshTokenDto
  {
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
  }
}