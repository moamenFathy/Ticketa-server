using System.Net;

namespace Ticketa.Tests.Helpers
{
  public class FakeHttpMessageHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
      _handler = handler;
    }

    public FakeHttpMessageHandler(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
      _handler = _ => new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
      };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      return Task.FromResult(_handler(request));
    }
  }
}
