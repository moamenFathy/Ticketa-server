using Ticketa.Core.Entities;
using Ticketa.Core.Enums;

namespace Ticketa.Tests.TestBuilders
{
  public class BookingBuilder
  {
    private int _id = 1;
    private string _userId = "user-123";
    private int _showtimeId = 1;
    private DateTime _bookedAt = DateTime.UtcNow;
    private decimal _totalAmount = 100m;
    private BookingStatus _status = BookingStatus.Confirmed;
    private string _reference = "TKT-20260908-1234";
    private List<BookedSeat> _bookedSeats = new();

    public BookingBuilder WithId(int id)
    {
      _id = id;
      return this;
    }

    public BookingBuilder WithUserId(string userId)
    {
      _userId = userId;
      return this;
    }

    public BookingBuilder WithShowtimeId(int showtimeId)
    {
      _showtimeId = showtimeId;
      return this;
    }

    public BookingBuilder WithStatus(BookingStatus status)
    {
      _status = status;
      return this;
    }

    public BookingBuilder WithReference(string reference)
    {
      _reference = reference;
      return this;
    }

    public BookingBuilder WithSeats(params BookedSeat[] seats)
    {
      _bookedSeats = seats.ToList();
      _totalAmount = _bookedSeats.Sum(s => s.Price);
      return this;
    }

    public BookingBuilder WithSeats(IEnumerable<BookedSeat> seats)
    {
      _bookedSeats = seats.ToList();
      _totalAmount = _bookedSeats.Sum(s => s.Price);
      return this;
    }

    public Booking Build()
    {
      return new Booking
      {
        Id = _id,
        UserId = _userId,
        ShowtimeId = _showtimeId,
        BookedAt = _bookedAt,
        TotalAmount = _totalAmount,
        Status = _status,
        BookingRefrence = _reference,
        BookedSeats = _bookedSeats
      };
    }
  }
}
