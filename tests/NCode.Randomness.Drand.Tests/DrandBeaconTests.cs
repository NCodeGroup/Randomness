// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Randomness.Drand.Http;
using NCode.Randomness.Drand.Rounds;
using NCode.Randomness.Drand.Tests.Fixtures;
using NCode.Randomness.Drand.Tests.TestDrand;

namespace NCode.Randomness.Drand.Tests;

public sealed class DrandBeaconTests : IDisposable
{
    private static string QuicknetPath { get; } = $"/{DrandDefaults.ChainHash}/public/";

    private StubDrandServer Server { get; } = new();

    private DrandBeaconOptions Options { get; } = new();

    public void Dispose() => Server.Dispose();

    #region GetLatestPulseAsync Tests

    [Fact]
    public async Task GetLatestPulseAsync_WithDrandData_ReturnsTheVerifiedPulse()
    {
        Server.Serve(QuicknetPath + "latest", DrandFixtures.Round(DrandFixtures.LatestRound));
        var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        var document = System.Text.Json.JsonElement.Parse(
            DrandFixtures.Round(DrandFixtures.LatestRound)
        );
        var signature = document.GetProperty("signature").GetString();
        Assert.Equal(DrandDefaults.BeaconName, pulse.Beacon);
        Assert.Equal(DrandDefaults.ChainHash, pulse.Chain);
        Assert.Equal(DrandFixtures.LatestRound, pulse.Round);
        Assert.Equal(signature, pulse.Id);
        Assert.Equal(DrandFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(DrandFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Randomness));
        Assert.Equal([QuicknetPath + "latest"], Server.Requests);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WithRoundOfTheTestChain_ReturnsIt()
    {
        var path = $"/{DrandTestChain.Chain.Hash}/public/";
        Server.Serve(path + "latest", DrandTestChain.Json(5));
        var beacon = Create(DrandTestChain.Chain);

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal(5, pulse.Round);
        Assert.Equal(DrandTestChain.Chain.Hash, pulse.Chain);
        Assert.Equal(DrandTestChain.Chain.TimeOf(5), pulse.Timestamp);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheSignatureIsNotTheChains_IsInvalid()
    {
        Server.Serve(QuicknetPath + "latest", DrandTestChain.Json(5));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThereIsNoLatestRound_IsUnavailable()
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheServerFails_IsUnavailable()
    {
        Server.Answer(QuicknetPath + "latest", HttpStatusCode.ServiceUnavailable);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheConnectionFails_IsUnavailable()
    {
        Server.Fail(QuicknetPath + "latest", new HttpRequestException("connection refused"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheRequestTimesOut_IsUnavailable()
    {
        Server.Fail(QuicknetPath + "latest", new TaskCanceledException("timeout"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCircuitIsOpen_IsUnavailable()
    {
        Server.Fail(
            QuicknetPath + "latest",
            new Polly.CircuitBreaker.BrokenCircuitException("open")
        );
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        Server.Serve(QuicknetPath + "latest", DrandFixtures.Round(DrandFixtures.LatestRound));
        var beacon = Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await beacon.GetLatestPulseAsync(cancellation.Token)
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsNotJson_IsInvalid()
    {
        Server.Serve(
            QuicknetPath + "latest",
            DrandFixtures.Round(DrandFixtures.LatestRound),
            "text/html"
        );
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsTooLarge_IsInvalid()
    {
        Options.MaxResponseBytes = 100;
        Server.Serve(QuicknetPath + "latest", DrandFixtures.Round(DrandFixtures.LatestRound));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            DrandDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("100 bytes", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithPublishedRound_ReturnsIt()
    {
        Server.Serve(QuicknetPath + "1000", DrandFixtures.Round(1000));
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(1000, CancellationToken.None);

        Assert.Equal(1000, pulse.Round);
        Assert.Equal(DrandFixtures.FirstRandomness, Convert.ToHexStringLower(pulse.Randomness));
        Assert.Equal(DrandChain.Quicknet.TimeOf(1000), pulse.Timestamp);
        Assert.Equal([QuicknetPath + "1000"], Server.Requests);
    }

    [Fact]
    public async Task GetPulseAsync_WhenServerAnswersAnotherRound_IsInvalid()
    {
        Server.Serve(QuicknetPath + "1000", DrandFixtures.Round(DrandFixtures.LatestRound));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(1000, token),
            DrandDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("round 34271222", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheSignatureIsOfAnotherRound_IsInvalid()
    {
        var forged = DrandFixtures
            .Round(1000)
            .Replace("\"round\":1000", "\"round\":1001", StringComparison.Ordinal);
        Server.Serve(QuicknetPath + "1001", forged);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(1001, token),
            DrandDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not verify", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData((HttpStatusCode)425)]
    public async Task GetPulseAsync_WithUnpublishedRound_IsNotFound(HttpStatusCode status)
    {
        Server.Answer(QuicknetPath + "99999999", status);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(99_999_999, token),
            DrandDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains("has not published round", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task GetPulseAsync_WithRoundBelowOne_IsNotFound(long round)
    {
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(round, token),
            DrandDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains("start at 1", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Server.Requests);
    }

    #endregion

    private DrandBeacon Create(DrandChain? chain = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var api = new DrandApi(Server, options);
        return new DrandBeacon(api, chain ?? DrandChain.Quicknet);
    }
}
