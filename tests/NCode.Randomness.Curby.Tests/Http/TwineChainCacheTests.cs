// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Http;

public sealed class TwineChainCacheTests : IDisposable
{
    private const string ChainPath = "/chains/" + CurbyFixtures.ChainId;

    private StubTwineServer Server { get; } = new();

    private FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

    private TwineChainCache Cache { get; }

    public TwineChainCacheTests()
    {
        var curbyOptions = new CurbyBeaconOptions();
        var options = Options.Create(curbyOptions);
        var api = new TwineApi(Server, options);
        Cache = new TwineChainCache(api, options, Time);
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

    [Fact]
    public async Task GetAsync_AfterAFailedFetch_FetchesAgain()
    {
        Server.Answer(ChainPath, HttpStatusCode.ServiceUnavailable);
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await Cache.GetAsync(cid, "rng", CancellationToken.None)
        );
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Time.Advance(new CurbyBeaconOptions().ChainRetryDelay);
        var chain = await Cache.GetAsync(cid, "rng", CancellationToken.None);

        Assert.NotNull(chain);
        Assert.Equal([ChainPath, ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTwoCallersAskAtOnce_FetchesOnce()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Server.Hold(ChainPath, release.Task);

        var first = Cache.GetAsync(cid, "rng", CancellationToken.None).AsTask();
        var second = Cache.GetAsync(cid, "rng", CancellationToken.None).AsTask();
        var waiting = !first.IsCompleted && !second.IsCompleted;
        release.SetResult();
        var firstChain = await first;
        var secondChain = await second;

        Assert.True(waiting);
        Assert.Same(firstChain, secondChain);
        Assert.Equal([ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTheFirstCallerCancels_StillServesTheSecond()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Server.Hold(ChainPath, release.Task);
        using var cancellation = new CancellationTokenSource();

        var first = Cache.GetAsync(cid, "rng", cancellation.Token).AsTask();
        var second = Cache.GetAsync(cid, "rng", CancellationToken.None).AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult();
        var chain = await second;

        Assert.NotNull(chain);
        Assert.Equal([ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WithinTheRetryDelayAfterAFailure_FailsWithoutARequest()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);
        await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await Cache.GetAsync(cid, "rng", CancellationToken.None)
        );
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Time.Advance(TimeSpan.FromSeconds(59));

        var exception = await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await Cache.GetAsync(cid, "rng", CancellationToken.None)
        );

        Assert.Contains("does not serve chain", exception.Message, StringComparison.Ordinal);
        Assert.Equal([ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_AfterTheRetryDelay_FetchesAgain()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);
        await Assert.ThrowsAsync<TwineFormatException>(async () =>
            await Cache.GetAsync(cid, "rng", CancellationToken.None)
        );
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Time.Advance(TimeSpan.FromMinutes(1));

        var chain = await Cache.GetAsync(cid, "rng", CancellationToken.None);

        Assert.NotNull(chain);
        Assert.Equal([ChainPath, ChainPath], Server.Requests);
    }

    [Fact]
    public async Task GetAsync_WhenTheFetchOutlastsItsTimeout_IsCancelled()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Server.Hold(ChainPath, release.Task);

        var pending = Cache.GetAsync(cid, "rng", CancellationToken.None).AsTask();
        Time.Advance(TwineChainCache.FetchTimeout);
        // The real 10-second bound only stops a broken fetch from hanging the run; it ends in a TimeoutException,
        // which fails the assertion.
        var bounded = pending.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bounded);
    }

    [Fact]
    public async Task GetAsync_ForTwoChains_KeepsEachOnce()
    {
        const string bellPath = "/chains/" + CurbyQuantumFixtures.BellChainId;
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        Server.Serve(bellPath, CurbyQuantumFixtures.BellChain);
        var rngCid = Cid.Parse(CurbyFixtures.ChainId);
        var bellCid = Cid.Parse(CurbyQuantumFixtures.BellChainId);

        var rngFirst = await Cache.GetAsync(rngCid, "rng", CancellationToken.None);
        var bellFirst = await Cache.GetAsync(bellCid, "bell", CancellationToken.None);
        var rngSecond = await Cache.GetAsync(rngCid, "rng", CancellationToken.None);
        var bellSecond = await Cache.GetAsync(bellCid, "bell", CancellationToken.None);

        Assert.Same(rngFirst, rngSecond);
        Assert.Same(bellFirst, bellSecond);
        Assert.Equal([ChainPath, bellPath], Server.Requests);
    }

    #endregion
}
