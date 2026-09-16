using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Ticketa.Core.Settings;
using Ticketa.Infrastructure.ExternalService;
using Ticketa.Tests.Helpers;
using Xunit;

namespace Ticketa.Tests.Infrastructure.Services
{
  public class VercelAnalyticsServiceTests
  {
    private readonly ILogger<VercelAnalyticsService> _logger = Mock.Of<ILogger<VercelAnalyticsService>>();

    [Fact]
    public void Constructor_WhenTokenOrProjectIdMissing_ThrowsInvalidOperationException()
    {
      var options = Options.Create(new VercelAnalyticsOptions { Token = null!, ProjectId = null! });
      var httpClient = new HttpClient(new FakeHttpMessageHandler(""));

      Assert.Throws<InvalidOperationException>(() => new VercelAnalyticsService(httpClient, options, _logger));
    }

    [Fact]
    public async Task GetSummaryAsync_WhenApiReturnsData_ParsesSummaryAndAggregates()
    {
      // Arrange
      var options = Options.Create(new VercelAnalyticsOptions
      {
        Token = "vercel-token-123",
        ProjectId = "prj_123",
        TeamId = "team_123"
      });

      var handler = new FakeHttpMessageHandler(req =>
      {
        var path = req.RequestUri!.ToString();
        if (path.Contains("visits/count"))
        {
          var countJson = """{ "data": { "visitors": 1500, "pageviews": 4500 } }""";
          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(countJson, System.Text.Encoding.UTF8, "application/json")
          };
        }
        else if (path.Contains("by=country"))
        {
          var countryJson = """
          {
            "data": [
              { "visitors": 800, "pageviews": 2400, "country": "EG" },
              { "visitors": 700, "pageviews": 2100, "country": "AE" }
            ]
          }
          """;
          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(countryJson, System.Text.Encoding.UTF8, "application/json")
          };
        }
        else
        {
          var genericJson = """{ "data": [] }""";
          return new HttpResponseMessage(HttpStatusCode.OK)
          {
            Content = new StringContent(genericJson, System.Text.Encoding.UTF8, "application/json")
          };
        }
      });

      var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.vercel.com/") };
      var sut = new VercelAnalyticsService(httpClient, options, _logger);

      // Act
      var result = await sut.GetSummaryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 30));

      // Assert
      Assert.NotNull(result);
      Assert.Equal(1500, result.Visitors);
      Assert.Equal(4500, result.PageViews);
      Assert.Equal(2, result.ByCountry.Count);
      Assert.Equal("EG", result.ByCountry[0].Label);
      Assert.Equal(800, result.ByCountry[0].Visitors);
    }
  }
}
