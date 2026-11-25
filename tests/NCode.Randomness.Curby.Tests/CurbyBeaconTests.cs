// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;

namespace NCode.Randomness.Curby.Tests;

public sealed class CurbyBeaconTests : IDisposable
{
    private const string ChainPath = "/chains/" + CurbyFixtures.ChainId;
    private const string PulsesPath = ChainPath + "/pulses/";

    private StubTwineServer Server { get; } = new();

    private CurbyBeaconOptions Options { get; } = new();

    private TwineTestServices Services { get; } = new();

    public void Dispose()
    {
        Services.Dispose();
        Server.Dispose();
    }

    #region GetLatestPulseAsync Tests

    [Fact]
    public async Task GetLatestPulseAsync_WithCurbyData_ReturnsTheVerifiedPulse()
    {
        ServeCurby();
        var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal(CurbyDefaults.BeaconName, pulse.Beacon);
        Assert.Equal(CurbyFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyFixtures.LatestRound, pulse.Round);
        Assert.Equal(CurbyFixtures.LatestPulseId, pulse.Id);
        Assert.Equal(CurbyFixtures.LatestTimestamp, pulse.Timestamp);
        Assert.Equal(CurbyFixtures.LatestRandomness, Convert.ToHexStringLower(pulse.Randomness));
        Assert.Equal(
            [ChainPath, PulsesPath + "latest", PulsesPath + Previous(CurbyFixtures.LatestRound)],
            Server.Requests
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_ForTheNextRound_ReusesTheChainAndTheLastPulse()
    {
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "latest", CurbyFixtures.Pulse(1620422))
            .Serve(PulsesPath + Previous(1620422), CurbyFixtures.Pulse(1620421));
        var beacon = Create();
        await beacon.GetLatestPulseAsync(CancellationToken.None);
        Server.Requests.Clear();
        Server.Serve(PulsesPath + "latest", CurbyFixtures.Pulse(1620423));

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Equal(1620423, pulse.Round);
        Assert.Equal([PulsesPath + "latest"], Server.Requests);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenPredecessorIsMissing_IsInvalid()
    {
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "latest", CurbyFixtures.Pulse(CurbyFixtures.LatestRound));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not serve pulse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenPredecessorIsNotTheLinkedPulse_IsInvalid()
    {
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "latest", CurbyFixtures.Pulse(CurbyFixtures.LatestRound))
            .Serve(PulsesPath + Previous(CurbyFixtures.LatestRound), CurbyFixtures.Pulse(1620421));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("does not precede", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheServerSwapsTheChain_IsInvalid()
    {
        using var impostor = new TwineBuilder();
        Server.Serve(ChainPath, impostor.ChainJson);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("pinned", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheChainIsMissing_IsInvalid()
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThereIsNoLatestPulse_IsUnavailable()
    {
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheServerFails_IsUnavailable()
    {
        Server.Answer(ChainPath, HttpStatusCode.ServiceUnavailable);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheConnectionFails_IsUnavailable()
    {
        Server.Fail(ChainPath, new HttpRequestException("connection refused"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheRequestTimesOut_IsUnavailable()
    {
        Server.Fail(ChainPath, new TaskCanceledException("timeout"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCircuitIsOpen_IsUnavailable()
    {
        Server.Fail(ChainPath, new Polly.CircuitBreaker.BrokenCircuitException("open"));
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        ServeCurby();
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
        Server.Serve(ChainPath, CurbyFixtures.Chain, "text/html");
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsMalformedJson_IsInvalid()
    {
        Server.Serve(ChainPath, "{");
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerHasAnInvalidEscape_IsInvalid()
    {
        Server.Serve(ChainPath, """{"cid":{"/":"x"},"data":{"content":"\ud800","signature":""}}""");
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheAnswerIsTooLarge_IsInvalid()
    {
        Options.MaxResponseBytes = 1024;
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("1024 bytes", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithPublishedRound_ReturnsIt()
    {
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "1620422", CurbyFixtures.Pulse(1620422))
            .Serve(PulsesPath + Previous(1620422), CurbyFixtures.Pulse(1620421));
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(1620422, CancellationToken.None);

        Assert.Equal(CurbyFixtures.CidOf(CurbyFixtures.Pulse(1620422)), pulse.Id);
    }

    [Fact]
    public async Task GetPulseAsync_WhenServerAnswersAnotherRound_IsInvalid()
    {
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "1620000", CurbyFixtures.Pulse(1620422));
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(1620000, token),
            CurbyDefaults.BeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("round 1620422", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WithUnpublishedRound_IsNotFound()
    {
        Server.Serve(ChainPath, CurbyFixtures.Chain);
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(99_999_999, token),
            CurbyDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task GetPulseAsync_WithRoundWithoutRandomness_IsNotFound(long round)
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(round, token),
            CurbyDefaults.BeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Empty(Server.Requests);
    }

    #endregion

    private static string Previous(long round) =>
        System
            .Text.Json.Nodes.JsonNode.Parse(CurbyFixtures.Pulse(round))
            ?["data"]?["content"]?["links"]?[0]?["/"]?.GetValue<string>()
        ?? throw new InvalidOperationException("No link.");

    private void ServeCurby() =>
        Server
            .Serve(ChainPath, CurbyFixtures.Chain)
            .Serve(PulsesPath + "latest", CurbyFixtures.Pulse(CurbyFixtures.LatestRound))
            .Serve(
                PulsesPath + Previous(CurbyFixtures.LatestRound),
                CurbyFixtures.Pulse(CurbyFixtures.LatestRound - 1)
            );

    private CurbyBeacon Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var (api, chains) = Services.Create(Server, options);
        return new CurbyBeacon(api, chains, options);
    }
}
