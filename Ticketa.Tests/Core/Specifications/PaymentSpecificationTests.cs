using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Specifications;
using Xunit;

namespace Ticketa.Tests.Core.Specifications
{
  public class PaymentSpecificationTests
  {
    private readonly List<Payment> _testPayments;

    public PaymentSpecificationTests()
    {
      _testPayments =
      [
        new Payment
        {
          Id = 1,
          UserId = "user-1",
          ShowtimeId = 10,
          SeatHash = "1:1,1:2",
          Status = PaymentStatus.Pending
        },
        new Payment
        {
          Id = 2,
          UserId = "user-1",
          ShowtimeId = 10,
          SeatHash = "1:1,1:2",
          Status = PaymentStatus.Completed
        },
        new Payment
        {
          Id = 3,
          UserId = "user-2",
          ShowtimeId = 20,
          SeatHash = "2:1",
          Status = PaymentStatus.Pending
        }
      ];
    }

    [Fact]
    public void DeduplicationConstructor_FiltersByUserIdShowtimeIdSeatHashAndPendingStatus()
    {
      // Arrange
      var spec = new PaymentSpecification("user-1", 10, "1:1,1:2");

      // Act
      var result = _testPayments.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal(1, result[0].Id);
      Assert.Equal(PaymentStatus.Pending, result[0].Status);
    }

    [Fact]
    public void FilterConstructor_WithShowtimeIdAndUserId_FiltersCorrectly()
    {
      // Arrange
      var spec = new PaymentSpecification(showtimeId: 20, userId: "user-2", status: null, includeUser: false, includeShowtime: false, skip: 0, take: 10);

      // Act
      var result = _testPayments.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void FilterConstructor_WithStatus_FiltersCorrectly()
    {
      // Arrange
      var spec = new PaymentSpecification(showtimeId: null, userId: null, status: PaymentStatus.Completed, includeUser: false, includeShowtime: false, skip: 0, take: 10);

      // Act
      var result = _testPayments.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void FilterConstructor_WithIncludes_AddsExpectedIncludeStrings()
    {
      // Arrange
      var spec = new PaymentSpecification(showtimeId: null, userId: null, status: null, includeUser: true, includeShowtime: true, skip: 5, take: 20);

      // Assert
      Assert.True(spec.IsPagingEnabled);
      Assert.Equal(5, spec.Skip);
      Assert.Equal(20, spec.Take);

      Assert.Equal(3, spec.Includes.Count); // PaymentSeats, User, Showtime
      Assert.Contains("Showtime.Movie", spec.IncludeStrings);
      Assert.Contains("Showtime.Hall", spec.IncludeStrings);
    }
  }
}
