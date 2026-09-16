using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Ticketa.Core.Helpers;
using Ticketa.Infrastructure.Authorization;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Authorization
{
  public class PermissionAuthorizationHandlerTests
  {
    private readonly PermissionAuthorizationHandler _sut;

    public PermissionAuthorizationHandlerTests()
    {
      _sut = new PermissionAuthorizationHandler();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasMatchingPermissionClaim_SucceedsRequirement()
    {
      // Arrange
      var user = new ClaimsPrincipal(new ClaimsIdentity(
      [
        new Claim("permission", Permissions.Movies.Import),
        new Claim("permission", Permissions.Movies.View)
      ], "TestAuth"));

      var requirement = new PermissionRequirement(Permissions.Movies.Import);
      var context = new AuthorizationHandlerContext([requirement], user, null);

      // Act
      await _sut.HandleAsync(context);

      // Assert
      Assert.True(context.HasSucceeded);
      Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasDifferentPermissionClaim_FailsExplicitly()
    {
      // Arrange: User has "movies:view" but endpoint requires "movies:delete"
      var user = new ClaimsPrincipal(new ClaimsIdentity(
      [
        new Claim("permission", Permissions.Movies.View)
      ], "TestAuth"));

      var requirement = new PermissionRequirement(Permissions.Movies.Delete);
      var context = new AuthorizationHandlerContext([requirement], user, null);

      // Act
      await _sut.HandleAsync(context);

      // Assert
      Assert.False(context.HasSucceeded);
      Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasNoPermissionClaims_FailsExplicitly()
    {
      // Arrange
      var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
      var requirement = new PermissionRequirement(Permissions.Dashboard.View);
      var context = new AuthorizationHandlerContext([requirement], user, null);

      // Act
      await _sut.HandleAsync(context);

      // Assert
      Assert.False(context.HasSucceeded);
      Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserIsUnauthenticated_FailsExplicitly()
    {
      // Arrange: Anonymous principal
      var user = new ClaimsPrincipal(new ClaimsIdentity());
      var requirement = new PermissionRequirement(Permissions.Payments.Refund);
      var context = new AuthorizationHandlerContext([requirement], user, null);

      // Act
      await _sut.HandleAsync(context);

      // Assert
      Assert.False(context.HasSucceeded);
      Assert.True(context.HasFailed);
    }
  }
}
