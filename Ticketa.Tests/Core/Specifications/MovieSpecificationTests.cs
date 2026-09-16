using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Specifications;
using Xunit;

namespace Ticketa.Tests.Core.Specifications
{
  public class MovieSpecificationTests
  {
    private readonly List<Movie> _testMovies;

    public MovieSpecificationTests()
    {
      _testMovies =
      [
        new Movie
        {
          Id = 1,
          Title = "Inception",
          Status = MovieStatus.Active,
          IsArchived = false,
          VoteAverage = 8.8,
          ReleaseDate = new DateTime(2010, 7, 16, 0, 0, 0, DateTimeKind.Utc),
          ImportedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          RuntimeMinutes = 148
        },
        new Movie
        {
          Id = 2,
          Title = "Interstellar",
          Status = MovieStatus.Active,
          IsArchived = false,
          VoteAverage = 8.6,
          ReleaseDate = new DateTime(2014, 11, 7, 0, 0, 0, DateTimeKind.Utc),
          ImportedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
          RuntimeMinutes = 169
        },
        new Movie
        {
          Id = 3,
          Title = "Tenet",
          Status = MovieStatus.ComingSoon,
          IsArchived = false,
          VoteAverage = 7.3,
          ReleaseDate = new DateTime(2020, 8, 26, 0, 0, 0, DateTimeKind.Utc),
          ImportedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
          RuntimeMinutes = 150
        },
        new Movie
        {
          Id = 4,
          Title = "Memento",
          Status = MovieStatus.Archived,
          IsArchived = true,
          VoteAverage = 8.4,
          ReleaseDate = new DateTime(2000, 10, 11, 0, 0, 0, DateTimeKind.Utc),
          ImportedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc),
          RuntimeMinutes = 113
        }
      ];
    }

    [Fact]
    public void Constructor_Default_ExcludesArchivedMovies()
    {
      // Arrange
      var spec = new MovieSpecification(archivedOnly: false);

      // Act
      var result = _testMovies.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Equal(3, result.Count);
      Assert.DoesNotContain(result, m => m.IsArchived);
    }

    [Fact]
    public void Constructor_ArchivedOnlyTrue_IncludesOnlyArchivedMovies()
    {
      // Arrange
      var spec = new MovieSpecification(archivedOnly: true);

      // Act
      var result = _testMovies.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.True(result[0].IsArchived);
      Assert.Equal("Memento", result[0].Title);
    }

    [Fact]
    public void ApplyFilters_WithStatusAndSearch_CombinesBothConditions()
    {
      // Arrange: Active movies containing "Incep"
      var spec = new MovieSpecification(MovieStatus.Active, "Incep", archivedOnly: false);

      // Act
      var result = _testMovies.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal("Inception", result[0].Title);
    }

    [Fact]
    public void ApplyFilters_WithStatusOnly_FiltersByStatus()
    {
      // Arrange
      var spec = new MovieSpecification(MovieStatus.ComingSoon, null, archivedOnly: false);

      // Act
      var result = _testMovies.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal("Tenet", result[0].Title);
    }

    [Fact]
    public void ApplyFilters_WithSearchOnly_FiltersByTitleContains()
    {
      // Arrange
      var spec = new MovieSpecification(null, "Inter", archivedOnly: false);

      // Act
      var result = _testMovies.AsQueryable().Where(spec.Criteria!).ToList();

      // Assert
      Assert.Single(result);
      Assert.Equal("Interstellar", result[0].Title);
    }

    [Theory]
    [InlineData(3, "asc", "Tenet", "Inception")]      // VoteAverage: 7.3 -> 8.8
    [InlineData(3, "desc", "Inception", "Tenet")]     // VoteAverage: 8.8 -> 7.3
    [InlineData(4, "asc", "Inception", "Tenet")]      // ReleaseDate: 2010 -> 2020
    [InlineData(4, "desc", "Tenet", "Inception")]     // ReleaseDate: 2020 -> 2010
    [InlineData(5, "asc", "Inception", "Tenet")]      // ImportedAt: Jan 1 -> Jan 3
    [InlineData(5, "desc", "Tenet", "Inception")]     // ImportedAt: Jan 3 -> Jan 1
    [InlineData(6, "asc", "Inception", "Interstellar")]// Runtime: 148 -> 169
    [InlineData(6, "desc", "Interstellar", "Inception")]// Runtime: 169 -> 148
    [InlineData(99, "desc", "Tenet", "Inception")]    // Default: ImportedAt desc
    public void ApplyOrdering_OrdersCorrectlyByColumnAndDirection(int column, string direction, string expectedFirst, string expectedLast)
    {
      // Arrange (Filtering non-archived movies)
      var spec = new MovieSpecification(null, null, column, direction, 0, 10, archivedOnly: false);

      // Act
      var query = _testMovies.AsQueryable().Where(spec.Criteria!);
      if (spec.OrderByDesc != null)
      {
        query = query.OrderByDescending(spec.OrderByDesc);
      }
      else if (spec.OrderBy != null)
      {
        query = query.OrderBy(spec.OrderBy);
      }
      var result = query.ToList();

      // Assert
      Assert.Equal(expectedFirst, result.First().Title);
      Assert.Equal(expectedLast, result.Last().Title);
    }

    [Fact]
    public void Constructor_WithOrderByStatus_SortsByStatus()
    {
      // Arrange
      var spec = new MovieSpecification(null, null, 0, "asc", 0, 10, archivedOnly: false, orderByStatus: true);

      // Act
      var query = _testMovies.AsQueryable().Where(spec.Criteria!);
      if (spec.OrderBy != null)
      {
        query = query.OrderBy(spec.OrderBy);
      }
      var result = query.ToList();

      // Assert
      Assert.NotNull(spec.OrderBy);
      Assert.Equal(MovieStatus.Active, result.First().Status);
      Assert.Equal(MovieStatus.ComingSoon, result.Last().Status);
    }

    [Fact]
    public void Constructor_WithIncludes_AddsGenresAndCast()
    {
      // Arrange
      var spec = new MovieSpecification(1, includeGenres: true, includeCast: true);

      // Assert
      Assert.Equal(2, spec.Includes.Count); // Genres, Cast
      Assert.NotNull(spec.Criteria);
    }
  }
}
