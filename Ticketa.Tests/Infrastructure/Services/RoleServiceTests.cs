using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ticketa.Core.DTOs.Roles;
using Ticketa.Core.Entities;
using Ticketa.Core.Helpers;
using Ticketa.Infrastructure.Data;
using Ticketa.Infrastructure.Service;
using Ticketa.Tests.Helpers;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class RoleServiceTests
  {
    private readonly Mock<RoleManager<AppRole>> _mockRoleManager;
    private readonly ApplicationDbContext _context;
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
      _mockRoleManager = IdentityMockHelper.MockRoleManager();

      var options = new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseInMemoryDatabase(Guid.NewGuid().ToString())
          .Options;
      _context = new ApplicationDbContext(options);

      _sut = new RoleService(_mockRoleManager.Object, _context);
    }

    #region GetAllAsync & GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenRoleNotFound_ReturnsNull()
    {
      // Arrange
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("non-existent-id"))
          .ReturnsAsync((AppRole?)null);

      // Act
      var result = await _sut.GetByIdAsync("non-existent-id");

      // Assert
      Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRoleExists_ReturnsRoleWithSelectedPermissions()
    {
      // Arrange
      var role = new AppRole("ContentManager")
      {
        Id = "role-1",
        IsAdminRole = true
      };

      _mockRoleManager
          .Setup(r => r.FindByIdAsync("role-1"))
          .ReturnsAsync(role);

      var claims = new List<Claim>
      {
        new("permission", Permissions.Movies.View),
        new("permission", Permissions.Movies.Import),
        new("custom", "other-claim")
      };

      _mockRoleManager
          .Setup(r => r.GetClaimsAsync(role))
          .ReturnsAsync(claims);

      // Act
      var result = await _sut.GetByIdAsync("role-1");

      // Assert
      Assert.NotNull(result);
      Assert.Equal("role-1", result.Id);
      Assert.Equal("ContentManager", result.Name);
      Assert.True(result.IsAdminRole);
      Assert.Equal(2, result.SelectedPermissions.Count);
      Assert.Contains(Permissions.Movies.View, result.SelectedPermissions);
      Assert.Contains(Permissions.Movies.Import, result.SelectedPermissions);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenRoleNameAlreadyExists_ReturnsFailure()
    {
      // Arrange
      var dto = new RoleUpsertDto { Name = "Admin" };
      _mockRoleManager
          .Setup(r => r.RoleExistsAsync("Admin"))
          .ReturnsAsync(true);

      // Act
      var (success, errors) = await _sut.CreateAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Role name already exists.", errors);
      _mockRoleManager.Verify(r => r.CreateAsync(It.IsAny<AppRole>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenIdentityCreateFails_ReturnsIdentityErrors()
    {
      // Arrange
      var dto = new RoleUpsertDto { Name = "InvalidRole" };
      _mockRoleManager
          .Setup(r => r.RoleExistsAsync(dto.Name))
          .ReturnsAsync(false);

      var identityError = new IdentityError { Description = "Database error creating role." };
      _mockRoleManager
          .Setup(r => r.CreateAsync(It.IsAny<AppRole>()))
          .ReturnsAsync(IdentityResult.Failed(identityError));

      // Act
      var (success, errors) = await _sut.CreateAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Database error creating role.", errors);
      _mockRoleManager.Verify(r => r.AddClaimAsync(It.IsAny<AppRole>(), It.IsAny<Claim>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesRoleAndAddsAllPermissionClaims()
    {
      // Arrange
      var dto = new RoleUpsertDto
      {
        Name = "Scheduler",
        IsAdminRole = false,
        SelectedPermissions = [Permissions.Showtimes.Create, Permissions.Showtimes.Edit]
      };

      _mockRoleManager
          .Setup(r => r.RoleExistsAsync(dto.Name))
          .ReturnsAsync(false);

      _mockRoleManager
          .Setup(r => r.CreateAsync(It.IsAny<AppRole>()))
          .ReturnsAsync(IdentityResult.Success);

      // Act
      var (success, errors) = await _sut.CreateAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);

      _mockRoleManager.Verify(r => r.CreateAsync(It.Is<AppRole>(a => a.Name == "Scheduler" && !a.IsAdminRole)), Times.Once);
      _mockRoleManager.Verify(r => r.AddClaimAsync(It.IsAny<AppRole>(), It.Is<Claim>(c => c.Type == "permission" && c.Value == Permissions.Showtimes.Create)), Times.Once);
      _mockRoleManager.Verify(r => r.AddClaimAsync(It.IsAny<AppRole>(), It.Is<Claim>(c => c.Type == "permission" && c.Value == Permissions.Showtimes.Edit)), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenRoleNotFound_ReturnsFailure()
    {
      // Arrange
      var dto = new RoleUpsertDto { Id = "missing-id", Name = "NewName" };
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("missing-id"))
          .ReturnsAsync((AppRole?)null);

      // Act
      var (success, errors) = await _sut.UpdateAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Role not found.", errors);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameCollidesWithExistingRole_ReturnsFailure()
    {
      // Arrange
      var existingRole = new AppRole("OldName") { Id = "role-1" };
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("role-1"))
          .ReturnsAsync(existingRole);

      _mockRoleManager
          .Setup(r => r.RoleExistsAsync("DuplicateName"))
          .ReturnsAsync(true);

      var dto = new RoleUpsertDto { Id = "role-1", Name = "DuplicateName" };

      // Act
      var (success, errors) = await _sut.UpdateAsync(dto);

      // Assert
      Assert.False(success);
      Assert.Contains("Role name already exists.", errors);
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_SynchronizesPermissionClaims()
    {
      // Arrange
      var role = new AppRole("Scheduler") { Id = "role-1", IsAdminRole = false };
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("role-1"))
          .ReturnsAsync(role);

      _mockRoleManager
          .Setup(r => r.UpdateAsync(role))
          .ReturnsAsync(IdentityResult.Success);

      var oldClaims = new List<Claim>
      {
        new("permission", Permissions.Movies.View),
        new("permission", Permissions.Showtimes.View)
      };
      _mockRoleManager
          .Setup(r => r.GetClaimsAsync(role))
          .ReturnsAsync(oldClaims);

      var dto = new RoleUpsertDto
      {
        Id = "role-1",
        Name = "LeadScheduler",
        IsAdminRole = true,
        SelectedPermissions = [Permissions.Showtimes.Create, Permissions.Showtimes.Edit]
      };

      // Act
      var (success, errors) = await _sut.UpdateAsync(dto);

      // Assert
      Assert.True(success);
      Assert.Empty(errors);
      Assert.Equal("LeadScheduler", role.Name);
      Assert.True(role.IsAdminRole);

      // Verify old claims removed
      _mockRoleManager.Verify(r => r.RemoveClaimAsync(role, It.Is<Claim>(c => c.Value == Permissions.Movies.View)), Times.Once);
      _mockRoleManager.Verify(r => r.RemoveClaimAsync(role, It.Is<Claim>(c => c.Value == Permissions.Showtimes.View)), Times.Once);

      // Verify new claims added
      _mockRoleManager.Verify(r => r.AddClaimAsync(role, It.Is<Claim>(c => c.Value == Permissions.Showtimes.Create)), Times.Once);
      _mockRoleManager.Verify(r => r.AddClaimAsync(role, It.Is<Claim>(c => c.Value == Permissions.Showtimes.Edit)), Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WhenRoleNotFound_ReturnsFailure()
    {
      // Arrange
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("missing-id"))
          .ReturnsAsync((AppRole?)null);

      // Act
      var (success, error) = await _sut.DeleteAsync("missing-id");

      // Assert
      Assert.False(success);
      Assert.Equal("Role not found.", error);
    }

    [Fact]
    public async Task DeleteAsync_WhenUsersAreAssignedToRole_BlocksDeletion()
    {
      // Arrange: Add 2 user-role assignments in the in-memory DbContext
      var role = new AppRole("BoxOffice") { Id = "role-box-office" };
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("role-box-office"))
          .ReturnsAsync(role);

      _context.UserRoles.Add(new IdentityUserRole<string> { UserId = "u1", RoleId = "role-box-office" });
      _context.UserRoles.Add(new IdentityUserRole<string> { UserId = "u2", RoleId = "role-box-office" });
      await _context.SaveChangesAsync();

      // Act
      var (success, error) = await _sut.DeleteAsync("role-box-office");

      // Assert
      Assert.False(success);
      Assert.Equal("Cannot delete role 'BoxOffice' — 2 user(s) are assigned to it.", error);
      _mockRoleManager.Verify(r => r.DeleteAsync(It.IsAny<AppRole>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenZeroUsersAssigned_DeletesRoleSuccessfully()
    {
      // Arrange: Role with zero assigned users
      var role = new AppRole("EmptyRole") { Id = "role-empty" };
      _mockRoleManager
          .Setup(r => r.FindByIdAsync("role-empty"))
          .ReturnsAsync(role);

      _mockRoleManager
          .Setup(r => r.DeleteAsync(role))
          .ReturnsAsync(IdentityResult.Success);

      // Act
      var (success, error) = await _sut.DeleteAsync("role-empty");

      // Assert
      Assert.True(success);
      Assert.Null(error);
      _mockRoleManager.Verify(r => r.DeleteAsync(role), Times.Once);
    }

    #endregion

    #region GetAllPermissions Tests

    [Fact]
    public void GetAllPermissions_ReturnsAllDiscoveredPermissions()
    {
      // Act
      var permissions = _sut.GetAllPermissions();

      // Assert
      Assert.NotEmpty(permissions);
      Assert.Contains(Permissions.Movies.Import, permissions);
      Assert.Contains(Permissions.Payments.Refund, permissions);
      Assert.Contains(Permissions.Dashboard.View, permissions);
    }

    #endregion
  }
}
