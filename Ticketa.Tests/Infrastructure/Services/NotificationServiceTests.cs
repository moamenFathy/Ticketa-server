using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Infrastructure.Data;
using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class NotificationServiceTests
  {
    private readonly ApplicationDbContext _context;
    private readonly TimeConversions _timeConversions;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
      var options = new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseInMemoryDatabase(Guid.NewGuid().ToString())
          .Options;
      _context = new ApplicationDbContext(options);

      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string?> { { "AppTimeZone", "UTC" } })
          .Build();
      _timeConversions = new TimeConversions(configuration);

      _sut = new NotificationService(_context, _timeConversions);
    }

    [Fact]
    public async Task GetNotificationCenterAsync_PopulatesSoldOutCurrentlyRunningAndRecentlyCompletedBuckets()
    {
      // Arrange
      var now = DateTime.UtcNow;
      var movie = new Movie { Id = 1, Title = "Dune" };
      var hall = new Hall { Id = 1, Name = "IMAX 1" };
      _context.Movies.Add(movie);
      _context.Halls.Add(hall);

      var soldOut = new Showtime
      {
        Id = 1,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = now.AddHours(2),
        EndTime = now.AddHours(4),
        Status = ShowtimeStatus.SoldOut,
        IsArchived = false
      };

      var currentlyRunning = new Showtime
      {
        Id = 2,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = now.AddMinutes(-30),
        EndTime = now.AddMinutes(90),
        Status = ShowtimeStatus.Scheduled,
        IsArchived = false
      };

      var recentlyCompleted = new Showtime
      {
        Id = 3,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = now.AddHours(-5),
        EndTime = now.AddHours(-3),
        Status = ShowtimeStatus.Completed,
        IsArchived = true,
        ArchivedAt = now.AddHours(-2) // Within 24h
      };

      var oldArchived = new Showtime
      {
        Id = 4,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = now.AddDays(-5),
        EndTime = now.AddDays(-5).AddHours(2),
        Status = ShowtimeStatus.Completed,
        IsArchived = true,
        ArchivedAt = now.AddDays(-3) // Outside 24h
      };

      _context.Showtimes.AddRange(soldOut, currentlyRunning, recentlyCompleted, oldArchived);
      await _context.SaveChangesAsync();

      // Act
      var result = await _sut.GetNotificationCenterAsync();

      // Assert
      Assert.Single(result.SoldOut);
      Assert.Equal(1, result.SoldOut[0].ShowtimeId);

      Assert.Single(result.CurrentlyRunning);
      Assert.Equal(2, result.CurrentlyRunning[0].ShowtimeId);

      Assert.Single(result.RecentlyCompleted);
      Assert.Equal(3, result.RecentlyCompleted[0].ShowtimeId);
    }
  }
}
