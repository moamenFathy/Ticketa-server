using Ticketa.Core.Helpers;

namespace Ticketa.Tests.Core
{
  public class HallTemplateTest
  {
    [Theory]
    [InlineData(10, 12, 10, 110)]
    [InlineData(14, 16, 10, 214)]
    [InlineData(6, 8, 10, 38)]
    public void VisibleAndInvisibleSeatCount_CalculateCorrectly(int rows, int seatsPerRow, int expectedInvisible, int expectedVisible)
    {
      // Arrange
      var hall = new HallTemplate { Rows = rows, SeatsPerRow = seatsPerRow };

      // Act
      var visibleSeatCount = hall.VisibleSeatCount;
      var invisibleSeatCount = hall.InvisibleSeatCount;

      // Assert
      Assert.Equal(expectedVisible, visibleSeatCount);
      Assert.Equal(expectedInvisible, invisibleSeatCount);
    }
  }
}
