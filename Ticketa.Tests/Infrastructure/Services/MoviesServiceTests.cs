using System.Linq.Expressions;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Moq;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Core.Interfaces.Services;
using Ticketa.Core.Specifications;
using Ticketa.Infrastructure.Service;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class MoviesServiceTests
  {
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IMovieRepository> _mockMovieRepo;
    private readonly Mock<IShowtimeRepository> _mockShowtimeRepo;
    private readonly Mock<IGenreRepository> _mockGenreRepo;
    private readonly Mock<ITmdbService> _mockTmdbService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly TimeConversions _timeConversions;
    private readonly MoviesService _sut;

    private const int DefaultMovieId = 1;

    public MoviesServiceTests()
    {
      _mockUow = new Mock<IUnitOfWork>();
      _mockMovieRepo = new Mock<IMovieRepository>();
      _mockShowtimeRepo = new Mock<IShowtimeRepository>();
      _mockGenreRepo = new Mock<IGenreRepository>();
      _mockTmdbService = new Mock<ITmdbService>();
      _mockMapper = new Mock<IMapper>();

      _mockUow.Setup(u => u.Movies).Returns(_mockMovieRepo.Object);
      _mockUow.Setup(u => u.Showtimes).Returns(_mockShowtimeRepo.Object);
      _mockUow.Setup(u => u.Genres).Returns(_mockGenreRepo.Object);

      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string?> { { "AppTimeZone", "UTC" } })
          .Build();
      _timeConversions = new TimeConversions(configuration);

      _sut = new MoviesService(_mockUow.Object, _mockTmdbService.Object, _mockMapper.Object, _timeConversions);
    }

    #region UpdateStatusAsync & Archiving Tests

    [Fact]
    public async Task UpdateStatusAsync_WhenStatusSetToArchived_SetsIsArchivedTrueAndArchivedAt()
    {
      // Arrange
      var movie = new Movie
      {
        Id = DefaultMovieId,
        Title = "Inception",
        Status = MovieStatus.Active,
        IsArchived = false,
        ArchivedAt = null
      };

      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync(movie);

      // Act
      var result = await _sut.UpdateStatusAsync(DefaultMovieId, MovieStatus.Archived);

      // Assert
      Assert.True(result);
      Assert.Equal(MovieStatus.Archived, movie.Status);
      Assert.True(movie.IsArchived);
      Assert.NotNull(movie.ArchivedAt);

      _mockMovieRepo.Verify(r => r.UpdateAsync(movie), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenStatusSetToActive_ResetsIsArchivedFalseAndClearsArchivedAt()
    {
      // Arrange: Reactivating an archived movie
      var movie = new Movie
      {
        Id = DefaultMovieId,
        Title = "Inception",
        Status = MovieStatus.Archived,
        IsArchived = true,
        ArchivedAt = DateTime.UtcNow.AddDays(-10)
      };

      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync(movie);

      // Act
      var result = await _sut.UpdateStatusAsync(DefaultMovieId, MovieStatus.Active);

      // Assert
      Assert.True(result);
      Assert.Equal(MovieStatus.Active, movie.Status);
      Assert.False(movie.IsArchived);
      Assert.Null(movie.ArchivedAt);

      _mockMovieRepo.Verify(r => r.UpdateAsync(movie), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenMovieNotFound_ReturnsFalse()
    {
      // Arrange
      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync((Movie?)null);

      // Act
      var result = await _sut.UpdateStatusAsync(999, MovieStatus.Archived);

      // Assert
      Assert.False(result);
      _mockMovieRepo.Verify(r => r.UpdateAsync(It.IsAny<Movie>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WhenMovieNotFound_ReturnsMovieNotFoundError()
    {
      // Arrange
      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync((Movie?)null);

      // Act
      var error = await _sut.DeleteAsync(DefaultMovieId);

      // Assert
      Assert.Equal("Movie not found.", error);
      _mockMovieRepo.Verify(r => r.Delete(It.IsAny<Movie>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenMovieHasAssociatedShowtimes_BlocksDeletion()
    {
      // Arrange: Deletion safety check
      var movie = new Movie { Id = DefaultMovieId, Title = "Avatar" };
      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync(movie);

      _mockShowtimeRepo
          .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Showtime, bool>>>()))
          .ReturnsAsync(true); // Has showtimes!

      // Act
      var error = await _sut.DeleteAsync(DefaultMovieId);

      // Assert
      Assert.Equal("Can't remove this movie — it still has showtimes.", error);
      _mockMovieRepo.Verify(r => r.Delete(It.IsAny<Movie>()), Times.Never);
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenMovieHasZeroShowtimes_DeletesMovieAndSaves()
    {
      // Arrange
      var movie = new Movie { Id = DefaultMovieId, Title = "Avatar" };
      _mockMovieRepo
          .Setup(r => r.GetAsync(It.IsAny<Expression<Func<Movie, bool>>>()))
          .ReturnsAsync(movie);

      _mockShowtimeRepo
          .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Showtime, bool>>>()))
          .ReturnsAsync(false); // Zero showtimes

      // Act
      var error = await _sut.DeleteAsync(DefaultMovieId);

      // Assert
      Assert.Null(error);
      _mockMovieRepo.Verify(r => r.Delete(movie), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    #endregion

    #region TMDB & Movie Discovery Tests

    [Fact]
    public async Task SearchMoviesAsync_ReturnsMappedSearchResultList()
    {
      // Arrange
      var tmdbResults = new List<TmdbMovieDto>
      {
        new()
        {
          TmdbId = 550,
          Title = "Fight Club",
          ReleaseDate = "1999-10-15",
          VoteAverage = 8.4,
          PosterPath = "/poster.jpg",
          Overview = "An insomniac office worker..."
        }
      };

      _mockTmdbService
          .Setup(s => s.SearchMoviesAsync("Fight Club", It.IsAny<CancellationToken>()))
          .ReturnsAsync(tmdbResults);

      // Act
      var result = (await _sut.SearchMoviesAsync("Fight Club", default)).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal(550, result[0].Value);
      Assert.Equal("Fight Club", result[0].Text);
      Assert.Equal("1999", result[0].Year);
      Assert.Equal("8.4", result[0].Rating);
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsActiveMoviesForDropdown()
    {
      // Arrange
      var activeMovies = new List<Movie>
      {
        new() { Id = 1, Title = "Avatar", RuntimeMinutes = 160, PosterPath = "/avatar.jpg" },
        new() { Id = 2, Title = "Batman", RuntimeMinutes = 175, PosterPath = "/batman.jpg" }
      };

      _mockMovieRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<MovieSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(activeMovies);

      // Act
      var result = (await _sut.GetAllActiveAsync()).ToList();

      // Assert
      Assert.Equal(2, result.Count);
      Assert.Equal("Avatar", result[0].Title);
      Assert.Equal("Batman", result[1].Title);
    }

    [Fact]
    public async Task GetComingSoonMoviesAsync_ReturnsComingSoonMovies()
    {
      // Arrange
      var comingSoonMovies = new List<Movie>
      {
        new()
        {
          Id = 1,
          Title = "Dune 3",
          Overview = "The saga continues...",
          PosterPath = "/dune.jpg",
          VoteAverage = 9.0,
          RuntimeMinutes = 160,
          ReleaseDate = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
          Language = "en",
          Genres = [new Genre { Name = "Sci-Fi" }]
        }
      };

      _mockMovieRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<MovieSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(comingSoonMovies);

      // Act
      var result = (await _sut.GetComingSoonMoviesAsync(default)).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal("Dune 3", result[0].Title);
      Assert.Single(result[0].Genres);
      Assert.Equal("Sci-Fi", result[0].Genres[0]);
    }

    [Fact]
    public async Task GetTopBookedMoviesAsync_CallsRepository()
    {
      // Arrange
      var topMovies = new List<TopBookedMovieDto>
      {
        new() { Id = 1, Title = "Top Movie", TicketsSold = 500 }
      };

      _mockMovieRepo
          .Setup(r => r.GetTopBookedMoviesAsync(6, It.IsAny<CancellationToken>()))
          .ReturnsAsync(topMovies);

      // Act
      var result = await _sut.GetTopBookedMoviesAsync(6, default);

      // Assert
      Assert.Single(result);
      Assert.Equal("Top Movie", result[0].Title);
      Assert.Equal(500, result[0].TicketsSold);
    }

    [Fact]
    public async Task ImportMoviesAsync_WhenNewMovies_ImportsAndSavesGenresAndMovies()
    {
      // Arrange
      var tmdbIds = new List<int> { 550 };
      _mockMovieRepo
          .Setup(r => r.ExistingTmdbIdsAsync(It.IsAny<IEnumerable<int>>()))
          .ReturnsAsync([]);

      var tmdbDetail = new TmdbMovieDetailDto
      {
        TmdbId = 550,
        Title = "Fight Club",
        Runtime = 139,
        Genres = [new TmdbGenreDto { Id = 18, Name = "Drama" }]
      };

      _mockTmdbService
          .Setup(s => s.GetMovieDetailAsync(550, It.IsAny<CancellationToken>()))
          .ReturnsAsync(tmdbDetail);

      _mockTmdbService
          .Setup(s => s.GetCreditsAsync(550, It.IsAny<CancellationToken>()))
          .ReturnsAsync(new TmdbCreditsDto { Cast = [] });

      _mockTmdbService
          .Setup(s => s.GetTrailerKeyAsync(550, It.IsAny<CancellationToken>()))
          .ReturnsAsync("trailer-key-123");

      var mappedMovie = new Movie
      {
        TmdbId = 550,
        Title = "Fight Club"
      };
      _mockMapper
          .Setup(m => m.Map<Movie>(tmdbDetail))
          .Returns(mappedMovie);

      _mockGenreRepo
          .Setup(r => r.GetByTmdbIdsAsync(It.IsAny<IEnumerable<int>>()))
          .ReturnsAsync([]); // New genre

      // Act
      var result = await _sut.ImportMoviesAsync(tmdbIds, default);

      // Assert
      Assert.Equal(1, result.ImportedTitles.Count);
      Assert.Equal("Fight Club", result.ImportedTitles[0]);
      Assert.Equal(0, result.FailedCount);

      _mockMovieRepo.Verify(r => r.CreateRangeAsync(It.IsAny<IEnumerable<Movie>>()), Times.Once);
      _mockUow.Verify(u => u.SaveAsync(), Times.AtLeastOnce);
    }

    #endregion
  }
}
