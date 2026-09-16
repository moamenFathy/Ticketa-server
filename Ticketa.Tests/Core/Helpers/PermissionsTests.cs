using Ticketa.Core.Helpers;
using Xunit;

namespace Ticketa.Tests.Core.Helpers
{
  public class PermissionsTests
  {
    [Fact]
    public void GetAll_DiscoversAllNestedConstants_WithoutDuplicates()
    {
      // Act
      var allPermissions = Permissions.GetAll().ToList();

      // Assert
      Assert.NotEmpty(allPermissions);
      Assert.Equal(allPermissions.Distinct().Count(), allPermissions.Count); // Zero duplicates

      // Verify core module permissions are discovered
      Assert.Contains(Permissions.Movies.View, allPermissions);
      Assert.Contains(Permissions.Movies.Import, allPermissions);
      Assert.Contains(Permissions.Showtimes.Create, allPermissions);
      Assert.Contains(Permissions.Payments.Refund, allPermissions);
      Assert.Contains(Permissions.Bookings.Scan, allPermissions);
      Assert.Contains(Permissions.Users.Create, allPermissions);
      Assert.Contains(Permissions.Roles.Edit, allPermissions);
      Assert.Contains(Permissions.Dashboard.View, allPermissions);
    }

    [Fact]
    public void GetAll_FormatConvention_AllPermissionsFollowModuleColonActionPattern()
    {
      // Act
      var allPermissions = Permissions.GetAll().ToList();

      // Assert: Every permission must follow "module:action" (lowercase, contains ':')
      foreach (var permission in allPermissions)
      {
        Assert.Contains(":", permission);
        var parts = permission.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.False(string.IsNullOrWhiteSpace(parts[0]), $"Module in '{permission}' should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(parts[1]), $"Action in '{permission}' should not be empty");
      }
    }
  }
}
