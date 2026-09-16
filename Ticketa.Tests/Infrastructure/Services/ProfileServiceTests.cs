using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using Ticketa.Core.DTOs.Profile;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Helpers;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Infrastructure.Service;
using Ticketa.Infrastructure.Specification;
using Ticketa.Tests.Helpers;
using Ticketa.Tests.TestBuilders;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class ProfileServiceTests
  {
    private readonly Mock<UserManager<AppUser>> _mockUserManager;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly TimeConversions _timeConversions;
    private readonly ProfileService _sut;

    private const string DefaultUserId = "user-123";
    private const string DefaultEmail = "user@example.com";

    public ProfileServiceTests()
    {
      _mockUserManager = IdentityMockHelper.MockUserManager();
      _mockUow = new Mock<IUnitOfWork>();
      _mockBookingRepo = new Mock<IBookingRepository>();
      _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);

      var inMemoryConfig = new Dictionary<string, string?>
      {
        { "AppTimeZone", "UTC" }
      };
      var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(inMemoryConfig)
          .Build();
      _timeConversions = new TimeConversions(configuration);

      _sut = new ProfileService(_mockUserManager.Object, _mockUow.Object, _timeConversions);
    }

    #region GetProfileAsync & UpdateProfileAsync Tests

    [Fact]
    public async Task GetProfileAsync_WhenUserNotFound_ReturnsNull()
    {
      // Arrange
      _mockUserManager
          .Setup(m => m.FindByIdAsync("non-existent-user"))
          .ReturnsAsync((AppUser?)null);

      // Act
      var result = await _sut.GetProfileAsync("non-existent-user");

      // Assert
      Assert.Null(result);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserExists_ReturnsMappedProfileDto()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithId(DefaultUserId)
          .WithEmail(DefaultEmail)
          .WithFirstName("Jane")
          .WithLastName("Doe")
          .Build();

      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync(user);

      // Act
      var result = await _sut.GetProfileAsync(DefaultUserId);

      // Assert
      Assert.NotNull(result);
      Assert.Equal("Jane", result.FirstName);
      Assert.Equal("Doe", result.LastName);
      Assert.Equal(DefaultEmail, result.Email);
      Assert.Equal(user.DateOfBirth, result.DateOfBirth);
      Assert.Equal("light", result.Theme);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsFailure()
    {
      // Arrange
      var dto = new ProfileUpdateDto
      {
        FirstName = "Jane",
        LastName = "Smith",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Theme = "dark"
      };

      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync((AppUser?)null);

      // Act
      var (success, errors) = await _sut.UpdateProfileAsync(DefaultUserId, dto);

      // Assert
      Assert.False(success);
      Assert.Contains("User not found.", errors);
      _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenValid_UpdatesFieldsAndReturnsSuccess()
    {
      // Arrange
      var user = new AppUserBuilder()
          .WithId(DefaultUserId)
          .WithFirstName("OldName")
          .WithLastName("OldLast")
          .Build();

      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync(user);

      var newDob = new DateOnly(1992, 8, 15);
      var dto = new ProfileUpdateDto
      {
        FirstName = "NewFirst",
        LastName = "NewLast",
        DateOfBirth = newDob,
        Theme = "dark"
      };

      // Act
      var (success, errors) = await _sut.UpdateProfileAsync(DefaultUserId, dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);
      Assert.Equal("NewFirst", user.FirstName);
      Assert.Equal("NewLast", user.LastName);
      Assert.Equal(newDob, user.DateOfBirth);
      Assert.Equal("dark", user.Theme);

      _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenIdentityFails_ReturnsFailureWithErrors()
    {
      // Arrange
      var user = new AppUserBuilder().WithId(DefaultUserId).Build();
      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync(user);

      var identityError = new IdentityError { Description = "Database concurrency error." };
      _mockUserManager
          .Setup(m => m.UpdateAsync(user))
          .ReturnsAsync(IdentityResult.Failed(identityError));

      var dto = new ProfileUpdateDto { FirstName = "A", LastName = "B" };

      // Act
      var (success, errors) = await _sut.UpdateProfileAsync(DefaultUserId, dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Database concurrency error.", errors);
    }

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsFailure()
    {
      // Arrange
      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync((AppUser?)null);

      var dto = new ChangePasswordDto { CurrentPassword = "Old", NewPassword = "New" };

      // Act
      var (success, errors) = await _sut.ChangePasswordAsync(DefaultUserId, dto);

      // Assert
      Assert.False(success);
      Assert.Contains("User not found.", errors);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenIdentityFails_ReturnsFailureWithErrors()
    {
      // Arrange (e.g. wrong current password)
      var user = new AppUserBuilder().WithId(DefaultUserId).Build();
      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync(user);

      var identityError = new IdentityError { Description = "Incorrect current password." };
      _mockUserManager
          .Setup(m => m.ChangePasswordAsync(user, "WrongPassword", "NewPassword123!"))
          .ReturnsAsync(IdentityResult.Failed(identityError));

      var dto = new ChangePasswordDto { CurrentPassword = "WrongPassword", NewPassword = "NewPassword123!" };

      // Act
      var (success, errors) = await _sut.ChangePasswordAsync(DefaultUserId, dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Incorrect current password.", errors);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenValid_SucceedsAndPreservesActiveRefreshTokens()
    {
      // Arrange: Security invariant: changing password does NOT kill active sessions
      const string activeRefreshToken = "active-session-refresh-token";
      var tokenExpiry = DateTime.UtcNow.AddDays(7);

      var user = new AppUserBuilder()
          .WithId(DefaultUserId)
          .WithRefreshToken(activeRefreshToken, tokenExpiry)
          .Build();

      _mockUserManager
          .Setup(m => m.FindByIdAsync(DefaultUserId))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.ChangePasswordAsync(user, "CorrectOldPassword123!", "BrandNewPassword123!"))
          .ReturnsAsync(IdentityResult.Success);

      var dto = new ChangePasswordDto
      {
        CurrentPassword = "CorrectOldPassword123!",
        NewPassword = "BrandNewPassword123!"
      };

      // Act
      var (success, errors) = await _sut.ChangePasswordAsync(DefaultUserId, dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);

      // Refresh token & expiry must be 100% preserved
      Assert.Equal(activeRefreshToken, user.RefreshToken);
      Assert.Equal(tokenExpiry, user.RefreshTokenExpiry);
    }

    #endregion

    #region GetBookingHistoryAsync Tests

    [Fact]
    public async Task GetBookingHistoryAsync_WhenPageSizeExceedsLimit_ClampsPageSizeTo25()
    {
      // Arrange: Caller requests 100 items per page
      _mockBookingRepo
          .Setup(r => r.CountAsync(It.IsAny<BookingHistoryCountSpecification>()))
          .ReturnsAsync(10);

      _mockBookingRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BookingHistorySpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      // Act
      var result = await _sut.GetBookingHistoryAsync(DefaultUserId, page: 1, pageSize: 100);

      // Assert
      Assert.Equal(25, result.PageSize);
    }

    [Fact]
    public async Task GetBookingHistoryAsync_WhenHasMorePages_ReturnsHasMoreTrue()
    {
      // Arrange: Total 30 items, page 1 with page size 10 (1 * 10 < 30 -> HasMore: true)
      _mockBookingRepo
          .Setup(r => r.CountAsync(It.IsAny<BookingHistoryCountSpecification>()))
          .ReturnsAsync(30);

      _mockBookingRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BookingHistorySpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      // Act
      var result = await _sut.GetBookingHistoryAsync(DefaultUserId, page: 1, pageSize: 10);

      // Assert
      Assert.True(result.HasMore);
      Assert.Equal(1, result.Page);
      Assert.Equal(10, result.PageSize);
      Assert.Equal(30, result.TotalCount);
    }

    [Fact]
    public async Task GetBookingHistoryAsync_WhenOnLastPage_ReturnsHasMoreFalse()
    {
      // Arrange: Total 30 items, page 3 with page size 10 (3 * 10 = 30 -> HasMore: false)
      _mockBookingRepo
          .Setup(r => r.CountAsync(It.IsAny<BookingHistoryCountSpecification>()))
          .ReturnsAsync(30);

      _mockBookingRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BookingHistorySpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      // Act
      var result = await _sut.GetBookingHistoryAsync(DefaultUserId, page: 3, pageSize: 10);

      // Assert
      Assert.False(result.HasMore);
    }

    [Fact]
    public async Task GetBookingHistoryAsync_WhenPastLastPage_ReturnsEmptyItemsAndHasMoreFalse()
    {
      // Arrange: Total 30 items, page 4 with page size 10 (4 * 10 > 30 -> HasMore: false)
      _mockBookingRepo
          .Setup(r => r.CountAsync(It.IsAny<BookingHistoryCountSpecification>()))
          .ReturnsAsync(30);

      _mockBookingRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BookingHistorySpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      // Act
      var result = await _sut.GetBookingHistoryAsync(DefaultUserId, page: 4, pageSize: 10);

      // Assert
      Assert.False(result.HasMore);
      Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetBookingHistoryAsync_ProjectsBookingFieldsAndSeatCountCorrectly()
    {
      // Arrange
      var movie = new Movie { Title = "Inception", PosterPath = "/poster.jpg" };
      var showtime = new Showtime { StartTime = DateTime.UtcNow.AddHours(2), Movie = movie };
      var seat1 = new BookedSeat { Row = 1, SeatNumber = 1 };
      var seat2 = new BookedSeat { Row = 1, SeatNumber = 2 };

      var booking = new Booking
      {
        Id = 1,
        UserId = DefaultUserId,
        BookingRefrence = "TKT-20260908-1111",
        TotalAmount = 250m,
        Status = BookingStatus.Confirmed,
        Showtime = showtime,
        BookedSeats = [seat1, seat2]
      };

      _mockBookingRepo
          .Setup(r => r.CountAsync(It.IsAny<BookingHistoryCountSpecification>()))
          .ReturnsAsync(1);

      _mockBookingRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BookingHistorySpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([booking]);

      // Act
      var result = await _sut.GetBookingHistoryAsync(DefaultUserId, page: 1, pageSize: 10);

      // Assert
      Assert.Single(result.Items);
      var item = result.Items.First();

      Assert.Equal("TKT-20260908-1111", item.BookingReference);
      Assert.Equal("Inception", item.MovieTitle);
      Assert.Equal("/poster.jpg", item.MoviePosterPath);
      Assert.Equal(2, item.SeatCount); // 2 BookedSeats
      Assert.Equal(250m, item.TotalAmount);
      Assert.Equal(BookingStatus.Confirmed, item.Status);
    }

    #endregion
  }
}
