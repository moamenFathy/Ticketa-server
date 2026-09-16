using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class QrCodeServiceTests
  {
    private readonly QrCodeService _sut;

    public QrCodeServiceTests()
    {
      _sut = new QrCodeService();
    }

    [Fact]
    public void GeneratePng_ReturnsValidPngHeaderBytes()
    {
      // Arrange
      const string payload = "https://ticketa.com/scan/TKT-20260908-1234";

      // Act
      var pngBytes = _sut.GeneratePng(payload);

      // Assert
      Assert.NotNull(pngBytes);
      Assert.NotEmpty(pngBytes);

      // Verify standard PNG magic header: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
      byte[] expectedPngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
      Assert.Equal(expectedPngHeader, pngBytes.Take(8).ToArray());
    }
  }
}
