using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ticketa.Core.DTOs;
using Ticketa.Core.Entities;
using Ticketa.Infrastructure.Data;
using Ticketa.Infrastructure.Service;
using Ticketa.Tests.Helpers;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class AdminManagementServiceTests
  {
    private readonly Mock<UserManager<AppUser>> _mockUserManager;
    private readonly ApplicationDbContext _context;
    private readonly AdminManagementService _sut;

    public AdminManagementServiceTests()
    {
      _mockUserManager = IdentityMockHelper.MockUserManager();

      var options = new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseInMemoryDatabase(Guid.NewGuid().ToString())
          .Options;
      _context = new ApplicationDbContext(options);

      _sut = new AdminManagementService(_mockUserManager.Object, _context);
    }

    [Fact]
    public async Task CreateUserAsync_WhenPasswordMissing_ReturnsFailure()
    {
      // Arrange
      var dto = new AdminUserUpsertDto { Email = "admin@example.com", Password = null };

      // Act
      var (success, errors) = await _sut.CreateUserAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Password is required for new users.", errors);
    }

    [Fact]
    public async Task CreateUserAsync_WhenValid_CreatesUserAndAssignsRole()
    {
      // Arrange
      var dto = new AdminUserUpsertDto
      {
        Email = "admin@example.com",
        Password = "Password123!",
        FirstName = "Super",
        LastName = "Admin",
        Role = "Admin"
      };

      _mockUserManager
          .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), dto.Password))
          .ReturnsAsync(IdentityResult.Success);

      _mockUserManager
          .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), "Admin"))
          .ReturnsAsync(IdentityResult.Success);

      // Act
      var (success, errors) = await _sut.CreateUserAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);
      _mockUserManager.Verify(m => m.CreateAsync(It.Is<AppUser>(u => u.Email == dto.Email && u.EmailConfirmed), dto.Password), Times.Once);
      _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), "Admin"), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserNotFound_ReturnsFailure()
    {
      // Arrange
      _mockUserManager
          .Setup(m => m.FindByIdAsync("non-existent"))
          .ReturnsAsync((AppUser?)null);

      var dto = new AdminUserUpsertDto { Id = "non-existent", Email = "a@b.com" };

      // Act
      var (success, errors) = await _sut.UpdateUserAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("User not found.", errors);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenValid_UpdatesFieldsRolesAndResetsPasswordIfProvided()
    {
      // Arrange
      var user = new AppUser
      {
        Id = "user-1",
        Email = "old@example.com",
        FirstName = "Old",
        LastName = "Name"
      };

      _mockUserManager
          .Setup(m => m.FindByIdAsync("user-1"))
          .ReturnsAsync(user);

      _mockUserManager
          .Setup(m => m.UpdateAsync(user))
          .ReturnsAsync(IdentityResult.Success);

      _mockUserManager
          .Setup(m => m.GetRolesAsync(user))
          .ReturnsAsync(["Staff"]);

      _mockUserManager
          .Setup(m => m.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
          .ReturnsAsync(IdentityResult.Success);

      _mockUserManager
          .Setup(m => m.AddToRoleAsync(user, "Manager"))
          .ReturnsAsync(IdentityResult.Success);

      _mockUserManager
          .Setup(m => m.GeneratePasswordResetTokenAsync(user))
          .ReturnsAsync("reset-token");

      _mockUserManager
          .Setup(m => m.ResetPasswordAsync(user, "reset-token", "NewPass123!"))
          .ReturnsAsync(IdentityResult.Success);

      var dto = new AdminUserUpsertDto
      {
        Id = "user-1",
        Email = "new@example.com",
        FirstName = "New",
        LastName = "Name",
        Role = "Manager",
        Password = "NewPass123!"
      };

      // Act
      var (success, errors) = await _sut.UpdateUserAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);
      Assert.Equal("new@example.com", user.Email);
      Assert.Equal("New", user.FirstName);

      _mockUserManager.Verify(m => m.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Contains("Staff"))), Times.Once);
      _mockUserManager.Verify(m => m.AddToRoleAsync(user, "Manager"), Times.Once);
      _mockUserManager.Verify(m => m.ResetPasswordAsync(user, "reset-token", "NewPass123!"), Times.Once);
    }

    [Fact]
    public async Task GetAdminUsersAsync_ReturnsOnlyUsersWithAdminRoles()
    {
      // Arrange: Seed users and roles
      var adminRole = new AppRole("Admin") { Id = "r-admin", IsAdminRole = true };
      var customerRole = new AppRole("Customer") { Id = "r-cust", IsAdminRole = false };
      _context.Roles.AddRange(adminRole, customerRole);

      var adminUser = new AppUser { Id = "u1", FirstName = "Alice", LastName = "Admin", Email = "admin@example.com" };
      var customerUser = new AppUser { Id = "u2", FirstName = "Bob", LastName = "Customer", Email = "bob@example.com" };
      _context.Users.AddRange(adminUser, customerUser);

      _context.UserRoles.Add(new IdentityUserRole<string> { UserId = "u1", RoleId = "r-admin" });
      _context.UserRoles.Add(new IdentityUserRole<string> { UserId = "u2", RoleId = "r-cust" });
      await _context.SaveChangesAsync();

      // Act
      var result = await _sut.GetAdminUsersAsync();

      // Assert
      Assert.Single(result);
      Assert.Equal("Alice Admin", result[0].FullName);
      Assert.Equal("Admin", result[0].Role);
    }
  }
}
