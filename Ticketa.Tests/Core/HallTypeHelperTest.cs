using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;

namespace Ticketa.Tests.Core
{
  public class HallTypeHelperTest
  {
    [Theory]
    // Arrange
    [InlineData(SeatCategory.Regular, 1.0)]
    [InlineData(SeatCategory.Premium, 1.5)]
    [InlineData(SeatCategory.VIP, 1.5)]
    public void GetPriceMultiplier_ReturnsCorrectMultiplier(SeatCategory category, decimal expectedMultiplier)
    {
      // Act
      var result = HallTypeHelper.GetPriceMultiplier(category);
      // Assert
      Assert.Equal(expectedMultiplier, result);
    }
  }
}
