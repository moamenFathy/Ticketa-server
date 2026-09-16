using Moq;
using Stripe;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Core.Interfaces.IServices;
using Ticketa.Core.Specifications;
using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class PaymentManagementServiceTests
  {
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IPaymentRepository> _mockPaymentRepo;
    private readonly Mock<IBookingService> _mockBookingService;
    private readonly Mock<RefundService> _mockRefundService;
    private readonly PaymentManagementService _sut;

    public PaymentManagementServiceTests()
    {
      _mockUow = new Mock<IUnitOfWork>();
      _mockPaymentRepo = new Mock<IPaymentRepository>();
      _mockUow.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);

      _mockBookingService = new Mock<IBookingService>();
      _mockRefundService = new Mock<RefundService>();

      _sut = new PaymentManagementService(_mockUow.Object, _mockBookingService.Object, _mockRefundService.Object);
    }

    [Fact]
    public async Task GetAllAsync_DataTableRequest_ReturnsPaginatedDataTableResult()
    {
      // Arrange
      var request = new DataTableRequestsDto { Draw = 1, Start = 0, Length = 10 };
      _mockPaymentRepo
          .Setup(r => r.CountAsync(It.IsAny<PaymentManagementSpecification>()))
          .ReturnsAsync(25);

      var user = new AppUser { FirstName = "Alice", LastName = "Smith" };
      var movie = new Movie { Title = "Avatar" };
      var showtime = new Showtime { Movie = movie };
      var payments = new List<Payment>
      {
        new() { Id = 1, User = user, Showtime = showtime, TotalAmount = 100m, Status = PaymentStatus.Completed }
      };

      _mockPaymentRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<PaymentManagementSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(payments);

      // Act
      var result = await _sut.GetAllAsync(request, "Alice", 0, "asc");

      // Assert
      Assert.NotNull(result);
      _mockPaymentRepo.Verify(r => r.CountAsync(It.IsAny<PaymentManagementSpecification>()), Times.Exactly(2));
      _mockPaymentRepo.Verify(r => r.GetAllWithSpecAsync(It.IsAny<PaymentManagementSpecification>(), default), Times.Once);
    }

    [Fact]
    public async Task RefundAsync_WhenPaymentNotFound_ReturnsFailure()
    {
      // Arrange
      _mockPaymentRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<PaymentManagementSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync((Payment?)null);

      // Act
      var (success, message) = await _sut.RefundAsync(999);

      // Assert
      Assert.False(success);
      Assert.Equal("Payment not found.", message);
    }

    [Fact]
    public async Task RefundAsync_WhenAlreadyRefunded_ReturnsFailure()
    {
      // Arrange
      var payment = new Payment { Id = 1, Status = PaymentStatus.Refunded };
      _mockPaymentRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<PaymentManagementSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(payment);

      // Act
      var (success, message) = await _sut.RefundAsync(1);

      // Assert
      Assert.False(success);
      Assert.Equal("Payment has already been refunded.", message);
    }

    [Fact]
    public async Task RefundAsync_WhenValid_TriggersStripeRefundUpdatesStatusAndCancelsBookings()
    {
      // Arrange
      var payment = new Payment
      {
        Id = 1,
        StripePaymentIntentId = "pi_valid_123",
        ShowtimeId = 10,
        Status = PaymentStatus.Completed,
        PaymentSeats = [new PaymentSeat { Row = 1, SeatNumber = 1 }]
      };

      _mockPaymentRepo
          .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<PaymentManagementSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(payment);

      _mockRefundService
          .Setup(r => r.CreateAsync(It.IsAny<RefundCreateOptions>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Refund { Id = "re_123" });

      _mockBookingService
          .Setup(b => b.CancelBookingsForPaymentAsync(10, payment.PaymentSeats))
          .ReturnsAsync((true, "Bookings cancelled successfully."));

      // Act
      var (success, message) = await _sut.RefundAsync(1);

      // Assert
      Assert.True(success);
      Assert.Equal("Payment refunded successfully.", message);
      Assert.Equal(PaymentStatus.Refunded, payment.Status);
      Assert.NotNull(payment.RefundedAt);

      _mockRefundService.Verify(r => r.CreateAsync(It.Is<RefundCreateOptions>(o => o.PaymentIntent == "pi_valid_123"), null, default), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
      _mockBookingService.Verify(b => b.CancelBookingsForPaymentAsync(10, payment.PaymentSeats), Times.Once);
    }
  }
}
