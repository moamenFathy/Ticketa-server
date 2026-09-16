using Ticketa.Core.Entities;
using Ticketa.Core.Enums;

namespace Ticketa.Tests.TestBuilders
{
  public class ShowtimeBuilder
  {
    private int _id = 1;
    private int _movieId = 1;
    private int _hallId = 1;
    private DateTime _startTime = DateTime.UtcNow.AddHours(6);
    private DateTime _endTime = DateTime.UtcNow.AddHours(8);
    private decimal _price = 100m;
    private ShowtimeStatus _status = ShowtimeStatus.Scheduled;
    private bool _isArchived = false;
    private Hall _hall = new Hall
    {
      Id = 1,
      Name = "Main Hall",
      Type = HallType.Standard,
      TotalRows = 10,
      SeatsPerRow = 12
    };
    private Movie _movie = new Movie
    {
      Id = 1,
      Title = "Inception",
      RuntimeMinutes = 120,
      Status = MovieStatus.Active
    };

    public ShowtimeBuilder WithId(int id)
    {
      _id = id;
      return this;
    }

    public ShowtimeBuilder WithHall(Hall hall)
    {
      _hall = hall;
      _hallId = hall.Id;
      return this;
    }

    public ShowtimeBuilder WithHallType(HallType type)
    {
      _hall = new Hall
      {
        Id = _hallId,
        Name = $"{type} Hall",
        Type = type,
        TotalRows = type == HallType.Standard ? 10 : (type == HallType.IMAX ? 14 : 6),
        SeatsPerRow = type == HallType.Standard ? 12 : (type == HallType.IMAX ? 16 : 8)
      };
      return this;
    }

    public ShowtimeBuilder WithPrice(decimal price)
    {
      _price = price;
      return this;
    }

    public ShowtimeBuilder WithStatus(ShowtimeStatus status)
    {
      _status = status;
      return this;
    }

    public ShowtimeBuilder WithMovie(Movie movie)
    {
      _movie = movie;
      _movieId = movie.Id;
      return this;
    }

    public Showtime Build()
    {
      return new Showtime
      {
        Id = _id,
        MovieId = _movieId,
        HallId = _hallId,
        StartTime = _startTime,
        EndTime = _endTime,
        Price = _price,
        Status = _status,
        IsArchived = _isArchived,
        Hall = _hall,
        Movie = _movie
      };
    }
  }
}
