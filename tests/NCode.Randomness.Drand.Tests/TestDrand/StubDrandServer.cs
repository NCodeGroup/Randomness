// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace NCode.Randomness.Drand.Tests.TestDrand;

/// <summary>
/// Answers drand API requests by path from documents a test registers, records every request, and answers 404 for any
/// other path.
/// </summary>
internal sealed class StubDrandServer : HttpMessageHandler, IHttpClientFactory
{
    private Dictionary<string, Func<HttpResponseMessage>> Routes { get; } =
        new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public StubDrandServer Serve(string path, string body, string mediaType = "application/json")
    {
        Routes[path] = () =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
        return this;
    }

    public StubDrandServer Answer(string path, HttpStatusCode status)
    {
        Routes[path] = () => new HttpResponseMessage(status);
        return this;
    }

    public StubDrandServer Fail(string path, Exception exception)
    {
        Routes[path] = () => throw exception;
        return this;
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        Requests.Add(path);
        var response = Routes.TryGetValue(path, out var route)
            ? route()
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
