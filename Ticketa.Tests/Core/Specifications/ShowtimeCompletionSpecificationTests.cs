using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Specifications;
using Xunit;

namespace Ticketa.Tests.Core.Specifications
{
  public class ShowtimeCompletionSpecificationTests
  {
    private readonly DateTime _now = DateTime.UtcNow;

    #region ShowtimeCloseBookingsSpecification Tests

    [Fact]
    public void CloseBookings_WhenSessionStartsInLessThan10Mins_MatchesForClosing()
    {
      // Arrange
      var showtime = new Showtime
      {
        Id = 1,
        Status = ShowtimeStatus.Scheduled,
        IsArchived = false,
        StartTime = _now.AddMinutes(5) // In 5 minutes (< 10 mins)
      };

      var spec = new ShowtimeCloseBookingsSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.True(matches);
    }

    [Fact]
    public void CloseBookings_WhenSessionSoldOutStartsIn5Mins_MatchesForClosing()
    {
      // Arrange
      var showtime = new Showtime
      {
        Id = 2,
        Status = ShowtimeStatus.SoldOut,
        IsArchived = false,
        StartTime = _now.AddMinutes(5)
      };

      var spec = new ShowtimeCloseBookingsSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.True(matches);
    }

    [Fact]
    public void CloseBookings_WhenSessionStartsIn1Hour_ExcludedFromClosing()
    {
      // Arrange: Starts in 60 minutes (> 10 mins)
      var showtime = new Showtime
      {
        Id = 3,
        Status = ShowtimeStatus.Scheduled,
        IsArchived = false,
        StartTime = _now.AddMinutes(60)
      };

      var spec = new ShowtimeCloseBookingsSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.False(matches);
    }

    [Fact]
    public void CloseBookings_WhenAlreadyArchived_ExcludedFromClosing()
    {
      // Arrange
      var showtime = new Showtime
      {
        Id = 4,
        Status = ShowtimeStatus.Scheduled,
        IsArchived = true,
        StartTime = _now.AddMinutes(5)
      };

      var spec = new ShowtimeCloseBookingsSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.False(matches);
    }

    #endregion

    #region ShowtimeCompletionSpecification Tests

    [Fact]
    public void CompletionSpec_WhenCompletedAndEndTimePassed_MatchesForArchiving()
    {
      // Arrange: Ended 10 minutes ago
      var showtime = new Showtime
      {
        Id = 1,
        Status = ShowtimeStatus.Completed,
        IsArchived = false,
        EndTime = _now.AddMinutes(-10)
      };

      var spec = new ShowtimeCompletionSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.True(matches);
    }

    [Fact]
    public void CompletionSpec_WhenEndTimeInFuture_ExcludedFromArchiving()
    {
      // Arrange: Completed status, but movie is still playing (EndTime in future)
      var showtime = new Showtime
      {
        Id = 2,
        Status = ShowtimeStatus.Completed,
        IsArchived = false,
        EndTime = _now.AddMinutes(30)
      };

      var spec = new ShowtimeCompletionSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.False(matches);
    }

    [Fact]
    public void CompletionSpec_WhenAlreadyArchived_ExcludedFromArchiving()
    {
      // Arrange
      var showtime = new Showtime
      {
        Id = 3,
        Status = ShowtimeStatus.Completed,
        IsArchived = true,
        EndTime = _now.AddMinutes(-10)
      };

      var spec = new ShowtimeCompletionSpecification();

      // Act
      var func = spec.Criteria!.Compile();
      var matches = func(showtime);

      // Assert
      Assert.False(matches);
    }

    #endregion
  }
}
