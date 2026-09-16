using Microsoft.AspNetCore.Identity;
using Moq;
using Ticketa.Core.Entities;

namespace Ticketa.Tests.Helpers
{
  public static class IdentityMockHelper
  {
    public static Mock<UserManager<AppUser>> MockUserManager(List<AppUser>? users = null)
    {
      var userList = users ?? new List<AppUser>();
      var store = new Mock<IUserStore<AppUser>>();
      var mgr = new Mock<UserManager<AppUser>>(
          store.Object,
          null!,
          null!,
          null!,
          null!,
          null!,
          null!,
          null!,
          null!);

      mgr.Setup(m => m.Users).Returns(userList.AsQueryable());

      mgr.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
          .ReturnsAsync(IdentityResult.Success);

      mgr.Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
          .ReturnsAsync(IdentityResult.Success);

      mgr.Setup(m => m.UpdateAsync(It.IsAny<AppUser>()))
          .ReturnsAsync(IdentityResult.Success);

      mgr.Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
          .ReturnsAsync(new List<string>());

      return mgr;
    }

    public static Mock<RoleManager<AppRole>> MockRoleManager()
    {
      var store = new Mock<IRoleStore<AppRole>>();
      var mgr = new Mock<RoleManager<AppRole>>(
          store.Object,
          null!,
          null!,
          null!,
          null!);

      return mgr;
    }
  }
}
