// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Http;

public sealed class TwineChainCacheTests : IDisposable
{
    private const string ChainPath = "/chains/" + CurbyFixtures.ChainId;

    private StubTwineServer Server { get; } = new();

    private TwineChainCache Cache { get; }

    public TwineChainCacheTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CurbyBeaconOptions());
        var api = new TwineApi(Server, options);
        Cache = new TwineChainCache(api);
    }

    public void Dispose()
    {
        Cache.Dispose();
        Server.Dispose();
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_TwiceForOneChain_FetchesItOnce()
    {
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        var first = await Cache.GetAsync(cid, "rng", CancellationToken.None);
        var second = await Cache.GetAsync(cid, "rng", CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal([ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTheChainIsMissing_Throws()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await Cache.GetAsync(cid, "rng", CancellationToken.None)
        );

        Assert.Contains("does not serve chain", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_WithAFamilyTheChainDoesNotFollow_Throws()
    {
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await Cache.GetAsync(cid, "bell", CancellationToken.None)
        );

        Assert.Contains("not twine/1.x/bell/1.x", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
