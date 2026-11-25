// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using NCode.Randomness.Drand.Http;
using NCode.Randomness.Drand.Rounds;
using NCode.Randomness.Drand.Tests.Fixtures;
using NCode.Randomness.Drand.Tests.TestDrand;

namespace NCode.Randomness.Drand.Tests.Http;

public sealed class DrandApiTests : IDisposable
{
    // HttpStatusCode has no member for 425 Too Early.
    private const HttpStatusCode TooEarly = (HttpStatusCode)425;

    private static string Path { get; } = DrandDefaults.ChainHash + "/public/latest";
    private static string Route { get; } = "/" + Path;

    private StubDrandServer Server { get; } = new();

    private DrandBeaconOptions Options { get; } = new();

    public void Dispose() => Server.Dispose();

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithJson_ReturnsTheDocument()
    {
        var json = DrandFixtures.Round(DrandFixtures.LatestRound);
        Server.Serve(Route, json);
        var api = Create();

        var document = await api.GetAsync(Path, CancellationToken.None);

        Assert.NotNull(document);
        var round = document.Value.GetProperty("round").GetInt64();

        Assert.Equal(DrandFixtures.LatestRound, round);
    }

    [Fact]
    public async Task GetAsync_WithDefaultOptions_RequestsTheDefaultPath()
    {
        var json = DrandFixtures.Round(DrandFixtures.LatestRound);
        Server.Serve(Route, json);
        var api = Create();

        await api.GetAsync(Path, CancellationToken.None);

        Assert.Equal([Route], Server.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(TooEarly)]
    public async Task GetAsync_WithNotFoundOrTooEarly_ReturnsNull(HttpStatusCode status)
    {
        Server.Answer(Route, status);
        var api = Create();

        var document = await api.GetAsync(Path, CancellationToken.None);

        Assert.Null(document);
        Assert.Equal([Route], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithOtherMediaType_Throws()
    {
        Server.Serve(Route, "<html></html>", "text/html");
        var api = Create();

        var exception = await Assert.ThrowsAsync<DrandFormatException>(async () =>
            await api.GetAsync(Path, CancellationToken.None)
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
        Assert.Contains("application/json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_OverTheLimit_Throws()
    {
        Options.MaxResponseBytes = 1024;
        var json = "\"" + new string('a', 2000) + "\"";
        Server.Serve(Route, json);
        var api = Create();

        var exception = await Assert.ThrowsAsync<DrandFormatException>(async () =>
            await api.GetAsync(Path, CancellationToken.None)
        );

        Assert.Contains("1024 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_AtTheLimit_ReturnsTheDocument()
    {
        Options.MaxResponseBytes = 1024;
        var json = "\"" + new string('a', 1022) + "\"";
        Server.Serve(Route, json);
        var api = Create();

        var document = await api.GetAsync(Path, CancellationToken.None);

        Assert.NotNull(document);
        var text = document.Value.GetString();
        Assert.Equal(1022, text?.Length);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"a\":1,\"a\":2}")]
    public async Task GetAsync_WithMalformedJson_Throws(string json)
    {
        Server.Serve(Route, json);
        var api = Create();

        var exception = await Assert.ThrowsAsync<DrandFormatException>(async () =>
            await api.GetAsync(Path, CancellationToken.None)
        );

        Assert.Contains("malformed JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithTruncatedJson_KeepsTheParserException()
    {
        Server.Serve(Route, "{");
        var api = Create();

        var exception = await Assert.ThrowsAsync<DrandFormatException>(async () =>
            await api.GetAsync(Path, CancellationToken.None)
        );

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetAsync_WithServiceUnavailable_Throws()
    {
        Server.Answer(Route, HttpStatusCode.ServiceUnavailable);
        var api = Create();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await api.GetAsync(Path, CancellationToken.None)
        );

        Assert.Contains("503", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private DrandApi Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        return new DrandApi(Server, options);
    }
}
