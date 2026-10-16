using System.Net;
using System.Text;

namespace AgendaApi.Tests;

/// Answers every request with a canned response and remembers what was asked for.
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public List<Uri> RequestedUris { get; } = new();

    public static FakeHttpHandler Returning(HttpStatusCode status, string body, string mediaType = "text/plain") =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);
        return Task.FromResult(_respond(request));
    }
}
