// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Http;

public sealed class TwineApiTests : IDisposable
{
    private const string Path = "/curbyq/round/28297";

    private StubTwineServer Server { get; } = new();

    private CurbyBeaconOptions Options { get; } = new();

    public void Dispose() => Server.Dispose();

    #region GetDocumentsAsync Tests

    [Fact]
    public async Task GetDocumentsAsync_WithArray_ParsesEachDocument()
    {
        var pulseJson = CurbyFixtures.Pulse(1620423);
        Server.Serve(Path, "[" + CurbyFixtures.Chain + "," + pulseJson + "]");
        var api = Create();

        var documents = await api.GetDocumentsAsync(Path, CancellationToken.None);
        var chainCid = CurbyFixtures.CidOf(CurbyFixtures.Chain);
        var pulseCid = CurbyFixtures.CidOf(pulseJson);
        var firstCid = documents[0].Cid.ToString();
        var secondCid = documents[1].Cid.ToString();

        Assert.Equal(2, documents.Length);
        Assert.Equal(chainCid, firstCid);
        Assert.Equal(pulseCid, secondCid);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithEmptyArray_ReturnsEmpty()
    {
        Server.Serve(Path, "[]");
        var api = Create();

        var documents = await api.GetDocumentsAsync(Path, CancellationToken.None);

        Assert.Empty(documents);
    }

    [Fact]
    public async Task GetDocumentsAsync_WhenNotFound_ReturnsEmpty()
    {
        var api = Create();

        var documents = await api.GetDocumentsAsync(Path, CancellationToken.None);

        Assert.Empty(documents);
        Assert.Equal([Path], Server.Requests);
    }

    [Fact]
    public async Task GetDocumentsAsync_WithObject_Throws()
    {
        Server.Serve(Path, CurbyFixtures.Chain);
        var api = Create();

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await api.GetDocumentsAsync(Path, CancellationToken.None)
        );

        Assert.Contains("not an array", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GetBytesAsync Tests

    [Fact]
    public async Task GetBytesAsync_WithOctetStream_ReturnsTheBody()
    {
        Server.Serve(Path, "raw bytes", "application/octet-stream");
        var api = Create();

        var body = await api.GetBytesAsync(Path, CancellationToken.None);

        var expected = "raw bytes"u8.ToArray();

        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task GetBytesAsync_WhenNotFound_ReturnsNull()
    {
        var api = Create();

        var body = await api.GetBytesAsync(Path, CancellationToken.None);

        Assert.Null(body);
    }

    [Fact]
    public async Task GetBytesAsync_WithOtherMediaType_Throws()
    {
        Server.Serve(Path, "<html></html>", "text/html");
        var api = Create();

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await api.GetBytesAsync(Path, CancellationToken.None)
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
        Assert.Contains("application/octet-stream", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBytesAsync_AtTheLimit_ReturnsTheBody()
    {
        Options.MaxResponseBytes = 1024;
        var text = new string('a', 1024);
        Server.Serve(Path, text, "application/octet-stream");
        var api = Create();

        var body = await api.GetBytesAsync(Path, CancellationToken.None);

        Assert.NotNull(body);
        Assert.Equal(1024, body.Length);
    }

    [Fact]
    public async Task GetBytesAsync_OneByteOverTheLimit_Throws()
    {
        Options.MaxResponseBytes = 1024;
        var text = new string('a', 1025);
        Server.Serve(Path, text, "application/octet-stream");
        var api = Create();

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await api.GetBytesAsync(Path, CancellationToken.None)
        );

        Assert.Contains("more than", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBytesAsync_OverTheLimit_Throws()
    {
        Options.MaxResponseBytes = 1024;
        var text = new string('a', 2000);
        Server.Serve(Path, text, "application/octet-stream");
        var api = Create();

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await api.GetBytesAsync(Path, CancellationToken.None)
        );

        Assert.Contains("more than", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private TwineApi Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        return new TwineApi(Server, options);
    }
}
