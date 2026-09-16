using Ticketa.Core.Entities;
using Ticketa.Core.Enums;

namespace Ticketa.Tests.TestBuilders
{
  public class PaymentBuilder
  {
    private int _id = 1;
    private string _stripePaymentIntentId = "pi_test_123456789";
    private string _clientSecret = "pi_test_123456789_secret_abc";
    private string _userId = "user-123";
    private int _showtimeId = 1;
    private decimal _totalAmount = 100m;
    private string _currency = "AED";
    private PaymentStatus _status = PaymentStatus.Pending;
    private string? _bookingReference = null;
    private string _seatHash = "1:1";
    private int _seatCount = 1;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _completedAt = null;
    private DateTime? _refundedAt = null;
    private List<PaymentSeat> _paymentSeats = new();

    public PaymentBuilder WithId(int id)
    {
      _id = id;
      return this;
    }

    public PaymentBuilder WithStripePaymentIntentId(string intentId)
    {
      _stripePaymentIntentId = intentId;
      return this;
    }

    public PaymentBuilder WithClientSecret(string clientSecret)
    {
      _clientSecret = clientSecret;
      return this;
    }

    public PaymentBuilder WithUserId(string userId)
    {
      _userId = userId;
      return this;
    }

    public PaymentBuilder WithShowtimeId(int showtimeId)
    {
      _showtimeId = showtimeId;
      return this;
    }

    public PaymentBuilder WithTotalAmount(decimal totalAmount)
    {
      _totalAmount = totalAmount;
      return this;
    }

    public PaymentBuilder WithStatus(PaymentStatus status)
    {
      _status = status;
      if (status == PaymentStatus.Completed)
      {
        _completedAt = DateTime.UtcNow;
      }
      else if (status == PaymentStatus.Refunded)
      {
        _refundedAt = DateTime.UtcNow;
      }
      return this;
    }

    public PaymentBuilder WithBookingReference(string? reference)
    {
      _bookingReference = reference;
      return this;
    }

    public PaymentBuilder WithSeatHash(string seatHash)
    {
      _seatHash = seatHash;
      return this;
    }

    public PaymentBuilder WithSeats(params PaymentSeat[] seats)
    {
      _paymentSeats = seats.ToList();
      _seatCount = _paymentSeats.Count;
      _totalAmount = _paymentSeats.Sum(s => s.UnitPrice);
      return this;
    }

    public Payment Build()
    {
      return new Payment
      {
        Id = _id,
        StripePaymentIntentId = _stripePaymentIntentId,
        ClientSecret = _clientSecret,
        UserId = _userId,
        ShowtimeId = _showtimeId,
        TotalAmount = _totalAmount,
        Currency = _currency,
        Status = _status,
        BookingReference = _bookingReference,
        SeatHash = _seatHash,
        SeatCount = _seatCount,
        CreatedAt = _createdAt,
        CompletedAt = _completedAt,
        RefundedAt = _refundedAt,
        PaymentSeats = _paymentSeats
      };
    }
  }
}
