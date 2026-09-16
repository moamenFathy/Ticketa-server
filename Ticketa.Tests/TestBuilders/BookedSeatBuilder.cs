using Ticketa.Core.Entities;
using Ticketa.Core.Enums;

namespace Ticketa.Tests.TestBuilders
{
  public class BookedSeatBuilder
  {
    private int _id = 1;
    private int _bookingId = 1;
    private int _showtimeId = 1;
    private int _row = 1;
    private int _seatNumber = 1;
    private SeatCategory _category = SeatCategory.Regular;
    private decimal _price = 100m;

    public BookedSeatBuilder WithId(int id)
    {
      _id = id;
      return this;
    }

    public BookedSeatBuilder WithBookingId(int bookingId)
    {
      _bookingId = bookingId;
      return this;
    }

    public BookedSeatBuilder WithShowtimeId(int showtimeId)
    {
      _showtimeId = showtimeId;
      return this;
    }

    public BookedSeatBuilder WithSeat(int row, int seatNumber)
    {
      _row = row;
      _seatNumber = seatNumber;
      return this;
    }

    public BookedSeatBuilder WithCategory(SeatCategory category)
    {
      _category = category;
      return this;
    }

    public BookedSeatBuilder WithPrice(decimal price)
    {
      _price = price;
      return this;
    }

    public BookedSeat Build()
    {
      return new BookedSeat
      {
        Id = _id,
        BookingId = _bookingId,
        ShowtimeId = _showtimeId,
        Row = _row,
        SeatNumber = _seatNumber,
        Category = _category,
        Price = _price
      };
    }
  }
}
