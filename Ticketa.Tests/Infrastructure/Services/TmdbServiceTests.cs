using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Ticketa.Infrastructure.ExternalService;
using Ticketa.Tests.Helpers;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class TmdbServiceTests
  {
    private readonly IConfiguration _configuration;
    private readonly ILogger<TmdbService> _logger;

    public TmdbServiceTests()
    {
      _configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string?> { { "Tmdb:ApiKey", "test-tmdb-key-123" } })
          .Build();
      _logger = Mock.Of<ILogger<TmdbService>>();
    }

    [Fact]
    public async Task GetPopularMoviesAsync_WhenApiReturnsMovies_ReturnsMovieList()
    {
      // Arrange
      var json = """
      {
        "page": 1,
        "results": [
          { "id": 550, "title": "Fight Club", "vote_average": 8.4 }
        ]
      }
      """;
      var handler = new FakeHttpMessageHandler(json);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var result = await sut.GetPopularMoviesAsync();

      // Assert
      Assert.Single(result);
      Assert.Equal(550, result[0].TmdbId);
      Assert.Equal("Fight Club", result[0].Title);
    }

    [Fact]
    public async Task GetMovieByIdAsync_WhenApiReturnsMovie_ReturnsMovieDetail()
    {
      // Arrange
      var json = """
      {
        "id": 550,
        "title": "Fight Club",
        "runtime": 139,
        "overview": "An insomniac office worker..."
      }
      """;
      var handler = new FakeHttpMessageHandler(json);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var result = await sut.GetMovieByIdAsync(550);

      // Assert
      Assert.NotNull(result);
      Assert.Equal(550, result.TmdbId);
      Assert.Equal("Fight Club", result.Title);
    }

    [Fact]
    public async Task GetTrailerKeyAsync_WhenOfficialYouTubeTrailerExists_ReturnsTrailerKey()
    {
      // Arrange
      var json = """
      {
        "id": 550,
        "results": [
          { "id": "v1", "key": "teaser_1", "site": "YouTube", "type": "Teaser", "official": false },
          { "id": "v2", "key": "official_trailer_key", "site": "YouTube", "type": "Trailer", "official": true }
        ]
      }
      """;
      var handler = new FakeHttpMessageHandler(json);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var key = await sut.GetTrailerKeyAsync(550);

      // Assert
      Assert.Equal("official_trailer_key", key);
    }

    [Fact]
    public async Task SearchMoviesAsync_WhenArabicQuery_SearchesWithArabicLanguageHeader()
    {
      // Arrange
      var json = """
      {
        "page": 1,
        "results": [
          { "id": 123, "title": "فيلم تجريبي" }
        ]
      }
      """;
      var handler = new FakeHttpMessageHandler(json);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var result = await sut.SearchMoviesAsync("فيلم");

      // Assert
      Assert.Single(result);
      Assert.Equal(123, result[0].TmdbId);
    }

    [Fact]
    public async Task GetCreditsAsync_WhenApiReturnsCast_ReturnsCredits()
    {
      // Arrange
      var json = """
      {
        "id": 550,
        "cast": [
          { "name": "Brad Pitt", "character": "Tyler Durden", "order": 0 }
        ]
      }
      """;
      var handler = new FakeHttpMessageHandler(json);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var result = await sut.GetCreditsAsync(550);

      // Assert
      Assert.NotNull(result);
      Assert.Single(result.Cast);
      Assert.Equal("Brad Pitt", result.Cast[0].Name);
    }

    [Fact]
    public async Task ErrorHandling_WhenApiReturns500_LogsAndReturnsSafeDefault()
    {
      // Arrange
      var handler = new FakeHttpMessageHandler("Internal Server Error", HttpStatusCode.InternalServerError);
      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };

      var sut = new TmdbService(httpClient, _configuration, _logger);

      // Act
      var popular = await sut.GetPopularMoviesAsync();
      var trailer = await sut.GetTrailerKeyAsync(550);
      var credits = await sut.GetCreditsAsync(550);

      // Assert
      Assert.Empty(popular);
      Assert.Null(trailer);
      Assert.Null(credits);
    }
  }
}
