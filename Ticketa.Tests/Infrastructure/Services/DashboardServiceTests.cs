using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Core.Interfaces.IServices;
using Ticketa.Infrastructure.Data;
using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class DashboardServiceTests
  {
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMoviesService> _mockMoviesService;
    private readonly TimeConversions _timeConversions;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
      var options = new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseInMemoryDatabase(Guid.NewGuid().ToString())
          .Options;
      _context = new ApplicationDbContext(options);

      _mockMoviesService = new Mock<IMoviesService>();

      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string?> { { "AppTimeZone", "UTC" } })
          .Build();
      _timeConversions = new TimeConversions(configuration);

      _sut = new DashboardService(_context, _mockMoviesService.Object, _timeConversions);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_CalculatesRevenueBookingsOccupancyAndTopMoviesCorrectly()
    {
      // Arrange: Seed database with test data
      var movie = new Movie { Id = 1, Title = "Avatar" };
      var hall = new Hall { Id = 1, Name = "IMAX", Type = HallType.Gold, TotalRows = 6, SeatsPerRow = 8 }; // 38 visible seats
      _context.Movies.Add(movie);
      _context.Halls.Add(hall);

      var today = DateTime.UtcNow.Date;
      var showtimeToday = new Showtime
      {
        Id = 1,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = today.AddHours(14),
        EndTime = today.AddHours(16),
        Price = 100m,
        Status = ShowtimeStatus.Scheduled,
        IsArchived = false
      };
      var showtimeSoldOut = new Showtime
      {
        Id = 2,
        MovieId = 1,
        HallId = 1,
        Movie = movie,
        Hall = hall,
        StartTime = today.AddDays(1),
        EndTime = today.AddDays(1).AddHours(2),
        Price = 100m,
        Status = ShowtimeStatus.SoldOut,
        IsArchived = false
      };
      _context.Showtimes.AddRange(showtimeToday, showtimeSoldOut);

      // Seed 19 booked seats in showtimeToday (19 / 38 = 50.0% occupancy)
      for (int i = 1; i <= 19; i++)
      {
        _context.BookedSeats.Add(new BookedSeat { ShowtimeId = 1, Row = 2, SeatNumber = i, Price = 100m });
      }

      // Seed Payments: 1 Completed within 30 days ($200), 1 Completed outside 30 days ($500), 1 Pending ($300)
      _context.Payments.AddRange(
        new Payment
        {
          Id = 1,
          TotalAmount = 200m,
          Status = PaymentStatus.Completed,
          CompletedAt = DateTime.UtcNow.AddDays(-5),
          BookingReference = "TKT-100"
        },
        new Payment
        {
          Id = 2,
          TotalAmount = 500m,
          Status = PaymentStatus.Completed,
          CompletedAt = DateTime.UtcNow.AddDays(-40) // Outside 30 days
        },
        new Payment
        {
          Id = 3,
          TotalAmount = 300m,
          Status = PaymentStatus.Pending
        }
      );

      // Seed Bookings today
      _context.Bookings.Add(new Booking
      {
        Id = 1,
        UserId = "u1",
        ShowtimeId = 1,
        BookedAt = today.AddHours(2),
        TotalAmount = 200m,
        Status = BookingStatus.Confirmed
      });

      await _context.SaveChangesAsync();

      _mockMoviesService
          .Setup(m => m.GetTopBookedMoviesAsync(5, It.IsAny<CancellationToken>()))
          .ReturnsAsync([new TopBookedMovieDto { Id = 1, Title = "Avatar", TicketsSold = 19 }]);

      // Act
      var result = await _sut.GetDashboardSummaryAsync();

      // Assert
      Assert.Equal(200m, result.Revenue30Days);
      Assert.Single(result.RevenueTrend);
      Assert.Equal(200m, result.RevenueTrend[0].Amount);

      Assert.Equal(1, result.BookingsToday);
      Assert.Equal(1, result.SoldOutShowtimeCount);
      Assert.Single(result.TodayShowtimes);
      Assert.Equal("Avatar", result.TodayShowtimes[0].MovieTitle);
      Assert.Equal(19, result.TodayShowtimes[0].BookedSeats);
      Assert.Equal(38, result.TodayShowtimes[0].VisibleSeats);

      Assert.Single(result.TopMovies);
      Assert.Equal("Avatar", result.TopMovies[0].Title);
      Assert.Equal(19, result.TopMovies[0].BookingCount);

      Assert.Equal(50.0, result.AverageOccupancyPercent); // 19 / 38 * 100 = 50.0%
      Assert.NotEmpty(result.RecentActivity);
    }
  }
}
