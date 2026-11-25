// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Tests.Twine;

namespace NCode.Randomness.Curby.Tests;

public sealed class CurbyQuantumBeaconTests : IDisposable
{
    private const string ChainPath = "/chains/" + CurbyQuantumFixtures.ChainId;
    private const string BellChainPath = "/chains/" + CurbyQuantumFixtures.BellChainId;
    private const string SeedChainPath = "/chains/" + CurbyQuantumFixtures.SeedChainId;
    private const string RoundPath = "/curbyq/round/28297";
    private const string LatestPath = "/curbyq/round/latest";
    private const string ParametersPath = "/curbyq/round/28297/params";

    private StubTwineServer Server { get; } = new();

    private CurbyBeaconOptions Options { get; } = new();

    public void Dispose() => Server.Dispose();

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithCurbyQData_ReturnsTheVerifiedRound()
    {
        ServeCurbyQ();
        using var beacon = Create();

        var pulse = await beacon.GetPulseAsync(CurbyQuantumFixtures.Round, CancellationToken.None);

        AssertRound(pulse);
        Assert.Equal(
            [
                ChainPath,
                RoundPath,
                BellChainPath,
                BellChainPath + "/pulses/" + CurbyQuantumFixtures.BellResponseId,
                SeedChainPath,
                SeedPulsePath(1914068),
                SeedPulsePath(1914067),
                SeedPulsePath(1914066),
                ParametersPath,
            ],
            Server.Requests
        );
    }

    [Fact]
    public async Task GetPulseAsync_WithBuiltRound_ReturnsItsRandomness()
    {
        using var builder = new QuantumRoundBuilder();
        var round = ServeBuilt(builder);
        using var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        Assert.Equal(builder.Round, pulse.Round);
        Assert.Equal(builder.Quantum.ChainCid.ToString(), pulse.Chain);
        Assert.Equal(round.Result.Cid.ToString(), pulse.Id);
        Assert.Equal(round.Randomness, pulse.Randomness);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResponseIsThreePulsesBack_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { BellPulsesAfterResponse = 3 };
        var round = ServeBuilt(builder);
        using var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        Assert.Equal(round.Randomness, pulse.Randomness);
        Assert.Contains(BellPulsePath(builder, round, 0), Server.Requests);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResponseIsElevenPulsesBack_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { BellPulsesAfterResponse = 10 };
        ServeBuilt(builder);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("within 10 Bell pulses", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WithSeedCountOfThree_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 3 };
        var round = ServeBuilt(builder);
        using var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        Assert.Equal(round.Randomness, pulse.Randomness);
    }

    [Fact]
    public async Task GetPulseAsync_WhenNineSeedPulsesMixInThePrecommit_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 8 };
        var round = ServeBuilt(builder);
        using var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        Assert.Equal(round.Randomness, pulse.Randomness);
        Assert.Equal(10, round.SeedPulses.Count);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTenSeedPulsesMixInThePrecommit_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder
        {
            SeedCount = 1,
            SeedPulsesMixingPrecommit = 10,
        };
        ServeBuilt(builder);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("seed pulses in a row", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenThePrecommitDoesNotMixInTheBellChain_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { PrecommitMixesBell = false };
        ServeBuilt(builder);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains(
            "does not mix in the Bell chain",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResultDoesNotMixInTheSeedChain_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { ResultMixesSeed = false };
        ServeBuilt(builder);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains(
            "does not mix in the seed chain",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheBellChainIsAnotherFamily_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        ServeBuilt(builder);
        Options.BellChainId = builder.Seed.ChainCid.ToString();
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("not twine/1.x/bell/1.x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheRoundFailed_IsNotFound()
    {
        const string reason = "the Bell test failed";
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        Server.Serve(RoundPathOf(builder.Round), builder.ErrorRoundJson(reason));
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.PulseNotFound, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheRoundIsIncomplete_IsNotFound()
    {
        using var builder = new QuantumRoundBuilder();
        var round = builder.Build();
        ServeChains(builder);
        Server.Serve(RoundPathOf(builder.Round), $"[{round.Request.Json},{round.Precommit.Json}]");
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.PulseNotFound, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("has not completed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenNoRoundIsPublished_IsNotFound()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        Server.Serve(RoundPathOf(builder.Round), "[]");
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.PulseNotFound, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("has not published round", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerAnswersAnotherRound_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var round = ServeBuilt(builder);
        Server.Serve(RoundPathOf(builder.Round + 1), round.RoundJson);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round + 1, token)
        );

        Assert.Contains(
            $"answered round {builder.Round + 1} with round {builder.Round}",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheParametersAreMissing_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        ServeBuilt(builder, serveParameters: false);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("parameters of round", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerSwapsABellPulse_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var round = ServeBuilt(builder);
        Server.Serve(BellPulsePath(builder, round, 1), round.BellPulses[0].Json);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.InvalidPulse, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.Contains("answered pulse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerFails_IsUnavailable()
    {
        using var builder = new QuantumRoundBuilder();
        Options.QuantumChainId = builder.Quantum.ChainCid.ToString();
        Server.Answer("/chains/" + builder.Quantum.ChainCid, HttpStatusCode.ServiceUnavailable);
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(BeaconError.Unavailable, token =>
            beacon.GetPulseAsync(builder.Round, token)
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetPulseAsync_WithNegativeRound_IsNotFound()
    {
        using var beacon = Create();

        await AssertBeaconErrorAsync(BeaconError.PulseNotFound, token =>
            beacon.GetPulseAsync(-1, token)
        );

        Assert.Empty(Server.Requests);
    }

    #endregion

    #region GetLatestPulseAsync Tests

    [Fact]
    public async Task GetLatestPulseAsync_WithCurbyQData_ReturnsTheLatestRound()
    {
        ServeCurbyQ();
        using var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        AssertRound(pulse);
        Assert.Equal(LatestPath, Server.Requests[1]);
    }

    [Fact]
    public async Task GetLatestPulseAsync_ForTheSameRound_ReusesTheVerifiedPulse()
    {
        ServeCurbyQ();
        using var beacon = Create();
        var first = await beacon.GetLatestPulseAsync(CancellationToken.None);
        Server.Requests.Clear();

        var second = await beacon.GetLatestPulseAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal([LatestPath], Server.Requests);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenTheRoundFailed_IsUnavailable()
    {
        const string reason = "the Bell test failed";
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        Server.Serve(LatestPath, builder.ErrorRoundJson(reason));
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(
            BeaconError.Unavailable,
            beacon.GetLatestPulseAsync
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThereIsNoRound_IsUnavailable()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        Server.Serve(LatestPath, "[]");
        using var beacon = Create();

        var exception = await AssertBeaconErrorAsync(
            BeaconError.Unavailable,
            beacon.GetLatestPulseAsync
        );

        Assert.Contains("no latest round", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private static void AssertRound(BeaconPulse pulse)
    {
        Assert.Equal(CurbyDefaults.QuantumBeaconName, pulse.Beacon);
        Assert.Equal(CurbyQuantumFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyQuantumFixtures.Round, pulse.Round);
        Assert.Equal(CurbyQuantumFixtures.ResultId, pulse.Id);
        Assert.Equal(CurbyQuantumFixtures.Timestamp, pulse.Timestamp);
        Assert.Equal(CurbyQuantumFixtures.Randomness, Convert.ToHexStringLower(pulse.Randomness));
    }

    private static string SeedPulsePath(long index) =>
        SeedChainPath
        + "/pulses/"
        + TwineVerificationTests.Document(CurbyQuantumFixtures.SeedPulse(index)).Cid;

    private static string RoundPathOf(long round) => "/curbyq/round/" + round;

    private static string BellPulsePath(
        QuantumRoundBuilder builder,
        BuiltQuantumRound round,
        int index
    ) => "/chains/" + builder.Bell.ChainCid + "/pulses/" + round.BellPulses[index].Cid;

    private static async Task<BeaconException> AssertBeaconErrorAsync(
        BeaconError error,
        Func<CancellationToken, ValueTask<BeaconPulse>> fetch
    )
    {
        var exception = await Assert.ThrowsAsync<BeaconException>(async () =>
            await fetch(CancellationToken.None)
        );
        Assert.Equal(error, exception.Error);
        Assert.Equal(CurbyDefaults.QuantumBeaconName, exception.Beacon);
        return exception;
    }

    // Pins the builder's three chains and serves them.
    private void ServeChains(QuantumRoundBuilder builder)
    {
        Options.QuantumChainId = builder.Quantum.ChainCid.ToString();
        Options.BellChainId = builder.Bell.ChainCid.ToString();
        Server
            .Serve("/chains/" + builder.Quantum.ChainCid, builder.Quantum.ChainJson)
            .Serve("/chains/" + builder.Bell.ChainCid, builder.Bell.ChainJson)
            .Serve("/chains/" + builder.Seed.ChainCid, builder.Seed.ChainJson);
    }

    // Serves a built round: its chains, the round and latest round, every Bell and seed pulse, and the parameters.
    private BuiltQuantumRound ServeBuilt(QuantumRoundBuilder builder, bool serveParameters = true)
    {
        var round = builder.Build();
        ServeChains(builder);
        Server
            .Serve(RoundPathOf(builder.Round), round.RoundJson)
            .Serve(LatestPath, round.RoundJson);
        for (var i = 0; i < round.BellPulses.Count; i++)
        {
            Server.Serve(BellPulsePath(builder, round, i), round.BellPulses[i].Json);
        }

        foreach (var (json, cid) in round.SeedPulses)
        {
            Server.Serve("/chains/" + builder.Seed.ChainCid + "/pulses/" + cid, json);
        }

        if (serveParameters)
        {
            Server.Serve(
                RoundPathOf(builder.Round) + "/params",
                round.Parameters,
                "application/octet-stream"
            );
        }

        return round;
    }

    private void ServeCurbyQ()
    {
        Server
            .Serve(ChainPath, CurbyQuantumFixtures.Chain)
            .Serve(RoundPath, CurbyQuantumFixtures.RoundJson)
            .Serve(LatestPath, CurbyQuantumFixtures.RoundJson)
            .Serve(BellChainPath, CurbyQuantumFixtures.BellChain)
            .Serve(
                BellChainPath + "/pulses/" + CurbyQuantumFixtures.BellResponseId,
                CurbyQuantumFixtures.BellPulse(59313)
            )
            .Serve(SeedChainPath, CurbyQuantumFixtures.SeedChain)
            .Serve(ParametersPath, CurbyQuantumFixtures.Parameters, "application/octet-stream");
        foreach (var index in new long[] { 1914068, 1914067, 1914066 })
        {
            Server.Serve(SeedPulsePath(index), CurbyQuantumFixtures.SeedPulse(index));
        }
    }

    private CurbyQuantumBeacon Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        return new CurbyQuantumBeacon(new TwineApi(Server, options), options);
    }
}
