using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Infrastructure.Specification;
using Xunit;

namespace Ticketa.Tests.Core.Specifications
{
  public class BookingHistorySpecificationTests
  {
    private readonly List<Booking> _testBookings;
    private readonly DateTime _now = DateTime.UtcNow;

    public BookingHistorySpecificationTests()
    {
      var futureShowtime1 = new Showtime { Id = 1, StartTime = _now.AddDays(2), Movie = new Movie { Title = "Future Movie 1" } };
      var futureShowtime2 = new Showtime { Id = 2, StartTime = _now.AddDays(5), Movie = new Movie { Title = "Future Movie 2" } };
      var pastShowtime1 = new Showtime { Id = 3, StartTime = _now.AddDays(-3), Movie = new Movie { Title = "Past Movie 1" } };
      var pastShowtime2 = new Showtime { Id = 4, StartTime = _now.AddDays(-10), Movie = new Movie { Title = "Past Movie 2" } };

      _testBookings =
      [
        new Booking
        {
          Id = 1,
          UserId = "user-1",
          ShowtimeId = 1,
          Showtime = futureShowtime1,
          BookedAt = _now.AddDays(-1)
        },
        new Booking
        {
          Id = 2,
          UserId = "user-1",
          ShowtimeId = 2,
          Showtime = futureShowtime2,
          BookedAt = _now.AddDays(-2)
        },
        new Booking
        {
          Id = 3,
          UserId = "user-1",
          ShowtimeId = 3,
          Showtime = pastShowtime1,
          BookedAt = _now.AddDays(-5)
        },
        new Booking
        {
          Id = 4,
          UserId = "user-1",
          ShowtimeId = 4,
          Showtime = pastShowtime2,
          BookedAt = _now.AddDays(-12)
        },
        new Booking
        {
          Id = 5,
          UserId = "other-user",
          ShowtimeId = 1,
          Showtime = futureShowtime1,
          BookedAt = _now
        }
      ];
    }

    [Fact]
    public void UpcomingFilter_FiltersFutureShowtimes_AndOrdersByStartTimeAscending()
    {
      // Arrange
      var spec = new BookingHistorySpecification("user-1", page: 1, pageSize: 10, filter: BookingHistoryFilter.Upcoming);

      // Act
      var query = _testBookings.AsQueryable().Where(spec.Criteria!);
      if (spec.OrderBy != null)
      {
        query = query.OrderBy(spec.OrderBy);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(2, result.Count);
      Assert.Equal(1, result[0].Id); // Future Movie 1 (+2 days) before Future Movie 2 (+5 days)
      Assert.Equal(2, result[1].Id);
    }

    [Fact]
    public void PastFilter_FiltersPastShowtimes_AndOrdersByStartTimeDescending()
    {
      // Arrange
      var spec = new BookingHistorySpecification("user-1", page: 1, pageSize: 10, filter: BookingHistoryFilter.Past);

      // Act
      var query = _testBookings.AsQueryable().Where(spec.Criteria!);
      if (spec.OrderByDesc != null)
      {
        query = query.OrderByDescending(spec.OrderByDesc);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(2, result.Count);
      Assert.Equal(3, result[0].Id); // Past Movie 1 (-3 days) before Past Movie 2 (-10 days)
      Assert.Equal(4, result[1].Id);
    }

    [Fact]
    public void AllFilter_IncludesAllShowtimesForUser_AndOrdersByBookedAtDescending()
    {
      // Arrange
      var spec = new BookingHistorySpecification("user-1", page: 1, pageSize: 10, filter: BookingHistoryFilter.All);

      // Act
      var query = _testBookings.AsQueryable().Where(spec.Criteria!);
      if (spec.OrderByDesc != null)
      {
        query = query.OrderByDescending(spec.OrderByDesc);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(4, result.Count);
      Assert.Equal(1, result[0].Id); // Most recent BookedAt (-1 day)
    }

    [Fact]
    public void Paging_CalculatesPageOffsetAndPageSizeCorrectly()
    {
      // Arrange: Page 2 with PageSize 10 -> Skip = 10, Take = 10
      var spec = new BookingHistorySpecification("user-1", page: 2, pageSize: 10);

      // Assert
      Assert.True(spec.IsPagingEnabled);
      Assert.Equal(10, spec.Skip);
      Assert.Equal(10, spec.Take);
    }

    [Fact]
    public void BookingHistoryCountSpecification_AppliesIdenticalFilter()
    {
      // Arrange
      var spec = new BookingHistoryCountSpecification("user-1", BookingHistoryFilter.Upcoming);

      // Act
      var count = _testBookings.AsQueryable().Count(spec.Criteria!);

      // Assert
      Assert.Equal(2, count);
    }
  }
}
