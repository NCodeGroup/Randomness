// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace NCode.Randomness.Nist.Tests.TestNist;

/// <summary>
/// Answers NIST API requests by path from documents a test registers, records every request, and answers 404 for any
/// other path.
/// </summary>
internal sealed class StubNistServer : HttpMessageHandler, IHttpClientFactory
{
    private Dictionary<string, Func<HttpResponseMessage>> Routes { get; } =
        new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    private Dictionary<string, Task> Holds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Makes requests for <paramref name="path"/> wait until <paramref name="release"/> completes, or until their token
    /// is cancelled.
    /// </summary>
    public StubNistServer Hold(string path, Task release)
    {
        Holds[path] = release;
        return this;
    }

    public StubNistServer Serve(string path, string body, string mediaType = "application/json")
    {
        Routes[path] = () =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
        return this;
    }

    public StubNistServer Answer(string path, HttpStatusCode status)
    {
        Routes[path] = () => new HttpResponseMessage(status);
        return this;
    }

    public StubNistServer Fail(string path, Exception exception)
    {
        Routes[path] = () => throw exception;
        return this;
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        Requests.Add(path);
        if (Holds.TryGetValue(path, out var release))
        {
            await release.WaitAsync(cancellationToken);
        }

        return Routes.TryGetValue(path, out var route)
            ? route()
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
