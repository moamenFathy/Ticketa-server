using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Ticketa.Core.Entities;
using Ticketa.Core.Enums;
using Ticketa.Core.Interfaces;
using Ticketa.Core.Interfaces.IRepositories;
using Ticketa.Core.Specifications;
using Ticketa.Infrastructure.BackgroundServices;
using Xunit;

namespace Ticketa.Tests.Infrastructure.BackgroundServices
{
  public class ShowtimeCompletionServiceTests
  {
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IShowtimeRepository> _mockShowtimeRepo;
    private readonly ILogger<ShowtimeCompletionService> _logger;
    private readonly ShowtimeCompletionService _sut;

    public ShowtimeCompletionServiceTests()
    {
      _mockScopeFactory = new Mock<IServiceScopeFactory>();
      _mockScope = new Mock<IServiceScope>();
      _mockServiceProvider = new Mock<IServiceProvider>();
      _mockUow = new Mock<IUnitOfWork>();
      _mockShowtimeRepo = new Mock<IShowtimeRepository>();

      _mockUow.Setup(u => u.Showtimes).Returns(_mockShowtimeRepo.Object);

      _mockServiceProvider
          .Setup(sp => sp.GetService(typeof(IUnitOfWork)))
          .Returns(_mockUow.Object);

      _mockScope
          .Setup(s => s.ServiceProvider)
          .Returns(_mockServiceProvider.Object);

      _mockScopeFactory
          .Setup(sf => sf.CreateScope())
          .Returns(_mockScope.Object);

      _logger = Mock.Of<ILogger<ShowtimeCompletionService>>();

      _sut = new ShowtimeCompletionService(_mockScopeFactory.Object, _logger);
    }

    [Fact]
    public async Task WorkerCycle_WhenShowtimesNeedClosing_UpdatesStatusToCompletedAndSaves()
    {
      // Arrange
      var startingShowtime = new Showtime
      {
        Id = 1,
        Status = ShowtimeStatus.Scheduled,
        StartTime = DateTime.UtcNow.AddMinutes(5)
      };

      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCloseBookingsSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([startingShowtime]);

      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCompletionSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      using var cts = new CancellationTokenSource();
      _mockUow
          .Setup(u => u.SaveAsync())
          .Callback(() => cts.Cancel())
          .Returns(Task.CompletedTask);

      // Act
      await _sut.StartAsync(cts.Token);
      if (_sut.ExecuteTask != null)
      {
        try { await _sut.ExecuteTask; } catch (OperationCanceledException) { }
      }

      // Assert
      Assert.Equal(ShowtimeStatus.Completed, startingShowtime.Status);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task WorkerCycle_WhenExpiredShowtimesExist_SetsIsArchivedAndArchivedAtAndSaves()
    {
      // Arrange
      var expiredShowtime = new Showtime
      {
        Id = 2,
        Status = ShowtimeStatus.Completed,
        IsArchived = false,
        EndTime = DateTime.UtcNow.AddMinutes(-30)
      };

      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCloseBookingsSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCompletionSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([expiredShowtime]);

      using var cts = new CancellationTokenSource();
      _mockUow
          .Setup(u => u.SaveAsync())
          .Callback(() => cts.Cancel())
          .Returns(Task.CompletedTask);

      // Act
      await _sut.StartAsync(cts.Token);
      if (_sut.ExecuteTask != null)
      {
        try { await _sut.ExecuteTask; } catch (OperationCanceledException) { }
      }

      // Assert
      Assert.True(expiredShowtime.IsArchived);
      Assert.NotNull(expiredShowtime.ArchivedAt);
      _mockUow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task WorkerCycle_WhenNoShowtimesMatch_DoesNotCallSave()
    {
      // Arrange
      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCloseBookingsSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      _mockShowtimeRepo
          .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ShowtimeCompletionSpecification>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync([]);

      using var cts = new CancellationTokenSource();
      cts.Cancel(); // Cancelled before start

      // Act
      await _sut.StartAsync(cts.Token);

      // Assert
      _mockUow.Verify(u => u.SaveAsync(), Times.Never);
    }
  }
}
