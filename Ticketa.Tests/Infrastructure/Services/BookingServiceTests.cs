using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Core.Specifications;
using Ticketa.Infrastructure.Service;
using Ticketa.Tests.TestBuilders;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class BookingServiceTests
  {
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IShowtimeRepository> _mockShowtimeRepo;
    private readonly Mock<IBookedSeatRepository> _mockBookedSeatRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly ILogger<BookingService> _logger;
    private readonly TimeConversions _timeConversions;
    private readonly BookingService _sut;

    private const int DefaultShowtimeId = 1;
    private const string DefaultUserId = "user-123";
    private const decimal BasePrice = 100m;

    public BookingServiceTests()
    {
      _mockUow = new Mock<IUnitOfWork>();
      _mockShowtimeRepo = new Mock<IShowtimeRepository>();
      _mockBookedSeatRepo = new Mock<IBookedSeatRepository>();
      _mockBookingRepo = new Mock<IBookingRepository>();

      _mockUow.Setup(u => u.Showtimes).Returns(_mockShowtimeRepo.Object);
      _mockUow.Setup(u => u.BookedSeats).Returns(_mockBookedSeatRepo.Object);
      _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);

      _logger = Mock.Of<ILogger<BookingService>>();

      var inMemorySettings = new Dictionary<string, string?>
      {
        { "AppTimeZone", "UTC" }
      };
      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(inMemorySettings)
          .Build();
      _timeConversions = new TimeConversions(configuration);

      _sut = new BookingService(_mockUow.Object, _logger, _timeConversions);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenShowtimeNotFound_ReturnsConflictWithEmptySeatsAndNeverSaves()
    {
      // Arrange
      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = [new SeatDto { Row = 1, SeatNumber = 1 }]
      };

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((Showtime?)null);

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.False(result.Succeeded);
      Assert.NotNull(result.ConflictingSeats);
      Assert.Empty(result.ConflictingSeats);

      _mockBookingRepo.Verify(r => r.CreateAsync(It.IsAny<Booking>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSeatsAlreadyBooked_ReturnsConflictWithConflictingSeatsAndNeverCreatesBooking()
    {
      // Arrange
      var requestedSeats = new List<SeatDto>
      {
        new() { Row = 1, SeatNumber = 1 },
        new() { Row = 1, SeatNumber = 2 }
      };
      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = requestedSeats
      };

      var showtime = new ShowtimeBuilder().WithId(DefaultShowtimeId).Build();
      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      var conflictingSeats = new List<BookedSeat>
      {
        new BookedSeatBuilder().WithShowtimeId(DefaultShowtimeId).WithSeat(1, 1).Build()
      };
      _mockBookedSeatRepo
          .Setup(r => r.GetConflictAsync(DefaultShowtimeId, requestedSeats, It.IsAny<CancellationToken>()))
          .ReturnsAsync(conflictingSeats);

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.False(result.Succeeded);
      Assert.NotNull(result.ConflictingSeats);
      Assert.Single(result.ConflictingSeats);
      Assert.Equal(1, result.ConflictingSeats[0].Row);
      Assert.Equal(1, result.ConflictingSeats[0].SeatNumber);

      _mockBookingRepo.Verify(r => r.CreateAsync(It.IsAny<Booking>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithValidSeats_CalculatesPriceMultipliersAndTotalCorrectly()
    {
      // Arrange: Standard Hall has Rows 1-9 as Regular (1.0x) and Rows 10-12 as VIP (1.5x)
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithHallType(HallType.Standard)
          .WithPrice(BasePrice)
          .Build();

      var requestedSeats = new List<SeatDto>
      {
        new() { Row = 1, SeatNumber = 5 },   // Regular -> 100 * 1.0 = 100
        new() { Row = 10, SeatNumber = 5 }   // VIP     -> 100 * 1.5 = 150
      };
      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = requestedSeats
      };

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      _mockBookedSeatRepo
          .Setup(r => r.GetConflictAsync(DefaultShowtimeId, requestedSeats, It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(10); // Far below visible capacity

      Booking? capturedBooking = null;
      _mockBookingRepo
          .Setup(r => r.CreateAsync(It.IsAny<Booking>()))
          .Callback<Booking>(b => capturedBooking = b)
          .Returns(Task.CompletedTask);

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(250m, result.TotalAmount); // 100 + 150 = 250
      Assert.NotNull(result.BookingReference);

      Assert.NotNull(capturedBooking);
      Assert.Equal(DefaultUserId, capturedBooking.UserId);
      Assert.Equal(DefaultShowtimeId, capturedBooking.ShowtimeId);
      Assert.Equal(BookingStatus.Confirmed, capturedBooking.Status);
      Assert.Equal(250m, capturedBooking.TotalAmount);
      Assert.Equal(2, capturedBooking.BookedSeats.Count);

      var regularSeat = capturedBooking.BookedSeats.First(s => s.Row == 1);
      Assert.Equal(SeatCategory.Regular, regularSeat.Category);
      Assert.Equal(100m, regularSeat.Price);

      var vipSeat = capturedBooking.BookedSeats.First(s => s.Row == 10);
      Assert.Equal(SeatCategory.VIP, vipSeat.Category);
      Assert.Equal(150m, vipSeat.Price);

      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenBookingFillsCapacity_TransitionsShowtimeStatusToSoldOutAndUpdates()
    {
      // Arrange: Gold Hall has 38 visible seats
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithHallType(HallType.Gold)
          .WithStatus(ShowtimeStatus.Scheduled)
          .Build();

      var template = HallTypeHelper.GetTemplate(HallType.Gold);
      var totalSeats = template.VisibleSeatCount; // 38
      var existingBookedCount = totalSeats - 2;   // 36 existing

      var requestedSeats = new List<SeatDto>
      {
        new() { Row = 1, SeatNumber = 1 },
        new() { Row = 1, SeatNumber = 2 }
      }; // 2 seats -> 36 + 2 = 38 >= 38

      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = requestedSeats
      };

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      _mockBookedSeatRepo
          .Setup(r => r.GetConflictAsync(DefaultShowtimeId, requestedSeats, It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(existingBookedCount);

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(ShowtimeStatus.SoldOut, showtime.Status);
      _mockShowtimeRepo.Verify(r => r.UpdateAsync(showtime), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenBookingDoesNotFillCapacity_ShowtimeStatusRemainsScheduled()
    {
      // Arrange: Gold Hall has 38 visible seats
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithHallType(HallType.Gold)
          .WithStatus(ShowtimeStatus.Scheduled)
          .Build();

      var existingBookedCount = 10; // 10 + 2 = 12 < 38
      var requestedSeats = new List<SeatDto>
      {
        new() { Row = 1, SeatNumber = 1 },
        new() { Row = 1, SeatNumber = 2 }
      };

      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = requestedSeats
      };

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      _mockBookedSeatRepo
          .Setup(r => r.GetConflictAsync(DefaultShowtimeId, requestedSeats, It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(existingBookedCount);

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.True(result.Succeeded);
      Assert.Equal(ShowtimeStatus.Scheduled, showtime.Status);
      _mockShowtimeRepo.Verify(r => r.UpdateAsync(It.IsAny<Showtime>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenDbUpdateExceptionOccurs_CatchesExceptionAndReturnsLateConflict()
    {
      // Arrange
      var requestedSeats = new List<SeatDto>
      {
        new() { Row = 1, SeatNumber = 1 }
      };
      var dto = new BookingCreateDto
      {
        ShowtimeId = DefaultShowtimeId,
        Seats = requestedSeats
      };

      var showtime = new ShowtimeBuilder().WithId(DefaultShowtimeId).Build();
      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      // First pre-check passes (empty conflict)
      _mockBookedSeatRepo
          .SetupSequence(r => r.GetConflictAsync(DefaultShowtimeId, requestedSeats, It.IsAny<CancellationToken>()))
          .ReturnsAsync([])
          .ReturnsAsync([new BookedSeatBuilder().WithShowtimeId(DefaultShowtimeId).WithSeat(1, 1).Build()]);

      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(5);

      _mockUow
          .Setup(u => u.SaveAsync())
          .ThrowsAsync(new DbUpdateException("Concurrency unique constraint violation"));

      // Act
      var result = await _sut.CreateAsync(dto, DefaultUserId);

      // Assert
      Assert.False(result.Succeeded);
      Assert.NotNull(result.ConflictingSeats);
      Assert.Single(result.ConflictingSeats);
      Assert.Equal(1, result.ConflictingSeats[0].Row);
      Assert.Equal(1, result.ConflictingSeats[0].SeatNumber);
    }

    #endregion

    #region CancelBookingsForPaymentAsync Tests

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenShowtimeNotFound_ReturnsFailureWithMessage()
    {
      // Arrange
      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((Showtime?)null);

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.False(success);
      Assert.Equal("Showtime not found.", message);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenPaymentSeatsEmpty_ReturnsFailureWithMessage()
    {
      // Arrange
      var showtime = new ShowtimeBuilder().WithId(DefaultShowtimeId).Build();
      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, []);

      // Assert
      Assert.False(success);
      Assert.Equal("No seats associated with this payment.", message);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenNoMatchingBookedSeatsFound_ReturnsFailureWithMessage()
    {
      // Arrange
      var showtime = new ShowtimeBuilder().WithId(DefaultShowtimeId).Build();
      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      _mockBookedSeatRepo
          .Setup(r => r.GetByShowtimeIdAsync(DefaultShowtimeId, It.IsAny<CancellationToken>()))
          .ReturnsAsync([]); // Empty booked seats in DB

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.False(success);
      Assert.Equal("No matching booked seats found for this payment.", message);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenPartialSeatsCancelled_DeletesMatchedSeatsAndPreservesBookingStatus()
    {
      // Arrange: Booking 100 has 2 seats: (R1, S1) and (R1, S2). We cancel only (R1, S1).
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithStatus(ShowtimeStatus.Scheduled)
          .Build();

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      var seat1 = new BookedSeatBuilder().WithId(1).WithBookingId(100).WithSeat(1, 1).Build();
      var seat2 = new BookedSeatBuilder().WithId(2).WithBookingId(100).WithSeat(1, 2).Build();

      _mockBookedSeatRepo
          .Setup(r => r.GetByShowtimeIdAsync(DefaultShowtimeId, It.IsAny<CancellationToken>()))
          .ReturnsAsync([seat1, seat2]);

      var booking = new BookingBuilder()
          .WithId(100)
          .WithStatus(BookingStatus.Confirmed)
          .WithSeats(seat1, seat2)
          .Build();

      _mockBookingRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
          .ReturnsAsync(booking);

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.True(success);
      Assert.Equal("Bookings cancelled successfully.", message);

      _mockBookedSeatRepo.Verify(r => r.Delete(seat1), Times.Once);
      _mockBookedSeatRepo.Verify(r => r.Delete(seat2), Times.Never);
      Assert.Equal(BookingStatus.Confirmed, booking.Status); // Preserved because 1 seat remains

      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenAllSeatsForBookingCancelled_UpdatesBookingStatusToCancelled()
    {
      // Arrange: Booking 100 has only 1 seat (R1, S1). We cancel (R1, S1).
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithStatus(ShowtimeStatus.Scheduled)
          .Build();

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      var seat1 = new BookedSeatBuilder().WithId(1).WithBookingId(100).WithSeat(1, 1).Build();

      _mockBookedSeatRepo
          .Setup(r => r.GetByShowtimeIdAsync(DefaultShowtimeId, It.IsAny<CancellationToken>()))
          .ReturnsAsync([seat1]);

      var booking = new BookingBuilder()
          .WithId(100)
          .WithStatus(BookingStatus.Confirmed)
          .WithSeats(seat1)
          .Build();

      _mockBookingRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
          .ReturnsAsync(booking);

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.True(success);
      Assert.Equal(BookingStatus.Cancelled, booking.Status);

      _mockBookedSeatRepo.Verify(r => r.Delete(seat1), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenSoldOutShowtimeHasCapacityFreed_RevertsStatusToScheduled()
    {
      // Arrange: Gold Hall has 38 visible seats, currently SoldOut
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithHallType(HallType.Gold)
          .WithStatus(ShowtimeStatus.SoldOut)
          .Build();

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      var seat1 = new BookedSeatBuilder().WithId(1).WithBookingId(100).WithSeat(1, 1).Build();

      _mockBookedSeatRepo
          .Setup(r => r.GetByShowtimeIdAsync(DefaultShowtimeId, It.IsAny<CancellationToken>()))
          .ReturnsAsync([seat1]);

      var booking = new BookingBuilder().WithId(100).WithSeats(seat1).Build();
      _mockBookingRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
          .ReturnsAsync(booking);

      // After deletion, remaining count is 37 (< 38)
      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(37);

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.True(success);
      Assert.Equal(ShowtimeStatus.Scheduled, showtime.Status);
      _mockUow.Verify(u => u.SaveAsync(), Times.Exactly(2)); // Save for seats deletion + Save for status reversion
    }

    [Fact]
    public async Task CancelBookingsForPaymentAsync_WhenSoldOutShowtimeStillAtOrAboveCapacity_StatusRemainsSoldOut()
    {
      // Arrange: Gold Hall (38 seats), still at 38 seats remaining
      var showtime = new ShowtimeBuilder()
          .WithId(DefaultShowtimeId)
          .WithHallType(HallType.Gold)
          .WithStatus(ShowtimeStatus.SoldOut)
          .Build();

      _mockShowtimeRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ShowtimeByIdSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(showtime);

      var seat1 = new BookedSeatBuilder().WithId(1).WithBookingId(100).WithSeat(1, 1).Build();

      _mockBookedSeatRepo
          .Setup(r => r.GetByShowtimeIdAsync(DefaultShowtimeId, It.IsAny<CancellationToken>()))
          .ReturnsAsync([seat1]);

      var booking = new BookingBuilder().WithId(100).WithSeats(seat1).Build();
      _mockBookingRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
          .ReturnsAsync(booking);

      // Remaining count is still 38 (>= 38)
      _mockBookedSeatRepo
          .Setup(r => r.CountAsync(It.IsAny<BookedSeatByShowtimeIdSpecification>()))
          .ReturnsAsync(38);

      var paymentSeats = new List<PaymentSeat>
      {
        new() { Row = 1, SeatNumber = 1 }
      };

      // Act
      var (success, message) = await _sut.CancelBookingsForPaymentAsync(DefaultShowtimeId, paymentSeats);

      // Assert
      Assert.True(success);
      Assert.Equal(ShowtimeStatus.SoldOut, showtime.Status);
      _mockUow.Verify(u => u.SaveAsync(), Times.Exactly(2));
    }

    #endregion
  }
}
