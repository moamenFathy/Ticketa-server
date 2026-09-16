using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Specifications;
using Ticketa.Infrastructure.Specification;
using Xunit;

namespace Ticketa.Tests.Core.Specifications
{
  public class PaymentManagementSpecificationTests
  {
    private readonly List<Payment> _testPayments;

    public PaymentManagementSpecificationTests()
    {
      var userAlice = new AppUser { Id = "u1", FirstName = "Alice", LastName = "Smith", Email = "alice@example.com" };
      var userBob = new AppUser { Id = "u2", FirstName = "Bob", LastName = "Jones", Email = "bob@example.com" };
      var userCharlie = new AppUser { Id = "u3", FirstName = "Charlie", LastName = "Brown", Email = "charlie@example.com" };

      var movieAvatar = new Movie { Id = 1, Title = "Avatar" };
      var movieInception = new Movie { Id = 2, Title = "Inception" };
      var movieTitanic = new Movie { Id = 3, Title = "Titanic" };

      var showtime1 = new Showtime { Id = 1, Movie = movieAvatar };
      var showtime2 = new Showtime { Id = 2, Movie = movieInception };
      var showtime3 = new Showtime { Id = 3, Movie = movieTitanic };

      _testPayments =
      [
        new Payment
        {
          Id = 1,
          UserId = "u1",
          User = userAlice,
          Showtime = showtime1,
          TotalAmount = 200m,
          Status = PaymentStatus.Completed,
          BookingReference = "TKT-REF-100",
          CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
        },
        new Payment
        {
          Id = 2,
          UserId = "u2",
          User = userBob,
          Showtime = showtime2,
          TotalAmount = 100m,
          Status = PaymentStatus.Pending,
          BookingReference = "TKT-REF-200",
          CreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)
        },
        new Payment
        {
          Id = 3,
          UserId = "u3",
          User = userCharlie,
          Showtime = showtime3,
          TotalAmount = 300m,
          Status = PaymentStatus.Refunded,
          BookingReference = null,
          CreatedAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc)
        }
      ];
    }

    [Fact]
    public void Constructor_Includes_AddsExpectedNavigationProperties()
    {
      // Act
      var spec = new PaymentManagementSpecification();

      // Assert
      Assert.Equal(3, spec.Includes.Count); // User, Showtime, PaymentSeats
      Assert.Contains("Showtime.Movie", spec.IncludeStrings);
    }

    [Fact]
    public void Constructor_WithId_FiltersBySpecificId()
    {
      // Arrange
      var spec = new PaymentManagementSpecification(2);

      // Act
      var query = _testPayments.AsQueryable().Where(spec.Criteria!);
      var result = query.ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal(2, result[0].Id);
    }

    [Theory]
    [InlineData("Alice", 1)]
    [InlineData("bob@example.com", 1)]
    [InlineData("Inception", 1)]
    [InlineData("TKT-REF-100", 1)]
    [InlineData("NonExistent", 0)]
    public void ApplySearch_FiltersMatchingRecords(string searchTerm, int expectedCount)
    {
      // Arrange
      var spec = new PaymentManagementSpecification(searchTerm);

      // Act
      var query = _testPayments.AsQueryable();
      if (spec.Criteria != null)
      {
        query = query.Where(spec.Criteria);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void ApplySearch_WhenSearchNullOrWhitespace_DoesNotFilter()
    {
      // Arrange
      var specNull = new PaymentManagementSpecification(null);
      var specEmpty = new PaymentManagementSpecification("   ");

      // Assert
      Assert.Null(specNull.Criteria);
      Assert.Null(specEmpty.Criteria);
    }

    [Theory]
    [InlineData(0, "asc", "Alice", "Charlie")]
    [InlineData(0, "desc", "Charlie", "Alice")]
    [InlineData(1, "asc", "Alice", "Charlie")]
    [InlineData(1, "desc", "Charlie", "Alice")]
    [InlineData(2, "asc", "Alice", "Charlie")] // Avatar (A) -> Inception (B) -> Titanic (C)
    [InlineData(2, "desc", "Charlie", "Alice")]// Titanic (C) -> Inception (B) -> Avatar (A)
    [InlineData(3, "asc", "Bob", "Charlie")]   // 100m -> 300m
    [InlineData(3, "desc", "Charlie", "Bob")]  // 300m -> 100m
    [InlineData(4, "asc", "Bob", "Charlie")]   // Pending (0) -> Completed (1) -> Refunded (2)
    [InlineData(4, "desc", "Charlie", "Bob")]  // Refunded (2) -> Pending (0)
    [InlineData(5, "asc", "Alice", "Charlie")] // Jan 1 -> Jan 3
    [InlineData(5, "desc", "Charlie", "Alice")]// Jan 3 -> Jan 1
    [InlineData(99, "desc", "Charlie", "Alice")]// Default fallback: CreatedAt desc
    public void ApplyOrdering_OrdersCorrectlyByColumnAndDirection(int column, string direction, string expectedFirstUser, string expectedLastUser)
    {
      // Arrange
      var spec = new PaymentManagementSpecification(null, column, direction, 0, 10);

      // Act
      var query = _testPayments.AsQueryable();
      if (spec.OrderByDesc != null)
      {
        query = query.OrderByDescending(spec.OrderByDesc);
      }
      else if (spec.OrderBy != null)
      {
        query = query.OrderBy(spec.OrderBy);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(expectedFirstUser, result.First().User.FirstName);
      Assert.Equal(expectedLastUser, result.Last().User.FirstName);
    }

    [Fact]
    public void ApplyPaging_SetsSkipTakeAndIsPagingEnabled()
    {
      // Arrange
      var spec = new PaymentManagementSpecification(null, 0, "asc", skip: 1, take: 2);

      // Assert
      Assert.True(spec.IsPagingEnabled);
      Assert.Equal(1, spec.Skip);
      Assert.Equal(2, spec.Take);

      var query = _testPayments.AsQueryable().Skip(spec.Skip).Take(spec.Take);
      var result = query.ToList();
      Assert.Equal(2, result.Count);
    }
  }
}
