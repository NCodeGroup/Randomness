// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Tests.Twine;

namespace NCode.Randomness.Curby.Tests;

public sealed class CurbyQuantumBeaconTests : IDisposable
{
    private const string ChainPath = "/chains/" + CurbyQuantumFixtures.ChainId;
    private const string BellChainPath = "/chains/" + CurbyQuantumFixtures.BellChainId;
    private const string SeedChainPath = "/chains/" + CurbyQuantumFixtures.SeedChainId;
    private const string LatestPath = "/curbyq/round/latest";

    private static string RoundPath { get; } = "/curbyq/round/" + CurbyQuantumFixtures.Round;

    private static string ParametersPath { get; } = RoundPath + "/params";

    private static void AssertRound(BeaconPulse pulse)
    {
        Assert.Equal(CurbyDefaults.QuantumBeaconName, pulse.Beacon);
        Assert.Equal(CurbyQuantumFixtures.ChainId, pulse.Chain);
        Assert.Equal(CurbyQuantumFixtures.Round, pulse.Round);
        Assert.Equal(CurbyQuantumFixtures.ResultId, pulse.Id);
        Assert.Equal(CurbyQuantumFixtures.Timestamp, pulse.Timestamp);
        var randomness = Convert.ToHexStringLower(pulse.Randomness);
        Assert.Equal(CurbyQuantumFixtures.Randomness, randomness);
    }

    private static string SeedPulsePath(long index)
    {
        var json = CurbyQuantumFixtures.SeedPulse(index);
        var document = TwineVerificationTests.Document(json);
        return SeedChainPath + "/pulses/" + document.Cid;
    }

    private static string RoundPathOf(long round) => "/curbyq/round/" + round;

    private static string BellPulsePath(
        QuantumRoundBuilder builder,
        BuiltQuantumRound round,
        int index
    ) => "/chains/" + builder.Bell.ChainCid + "/pulses/" + round.BellPulses[index].Cid;

    private StubTwineServer Server { get; } = new();

    private CurbyBeaconOptions Options { get; } = new();

    private TwineTestServices Services { get; } = new();

    public void Dispose()
    {
        Services.Dispose();
        Server.Dispose();
    }

    #region GetPulseAsync Tests

    [Fact]
    public async Task GetPulseAsync_WithCurbyQData_ReturnsTheVerifiedRound()
    {
        ServeCurbyQ();
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(CurbyQuantumFixtures.Round, CancellationToken.None);

        var firstSeedPulsePath = SeedPulsePath(CurbyQuantumFixtures.ThirdSeedPulseIndex);
        var secondSeedPulsePath = SeedPulsePath(CurbyQuantumFixtures.SecondSeedPulseIndex);
        var thirdSeedPulsePath = SeedPulsePath(CurbyQuantumFixtures.FirstSeedPulseIndex);
        AssertRound(pulse);
        Assert.Equal(
            [
                ChainPath,
                RoundPath,
                BellChainPath,
                BellChainPath + "/pulses/" + CurbyQuantumFixtures.BellResponseId,
                SeedChainPath,
                firstSeedPulsePath,
                secondSeedPulsePath,
                thirdSeedPulsePath,
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
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        var chainId = builder.Quantum.ChainCid.ToString();
        var resultId = round.Result.Cid.ToString();

        Assert.Equal(builder.Round, pulse.Round);
        Assert.Equal(chainId, pulse.Chain);
        Assert.Equal(resultId, pulse.Id);
        Assert.Equal(round.Randomness, pulse.Randomness);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResponseIsThreePulsesBack_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { BellPulsesAfterResponse = 3 };
        var round = ServeBuilt(builder);
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        var expected = new List<string>();
        for (var i = 3; i >= 0; i--)
        {
            expected.Add(BellPulsePath(builder, round, i));
        }

        Assert.Equal(round.Randomness, pulse.Randomness);
        Assert.Equal(expected, BellPulseRequests(builder));
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResponseIsTenPulsesBack_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { BellPulsesAfterResponse = 9 };
        var round = ServeBuilt(builder);
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        var expected = new List<string>();
        for (var i = 9; i >= 0; i--)
        {
            expected.Add(BellPulsePath(builder, round, i));
        }

        Assert.Equal(round.Randomness, pulse.Randomness);
        Assert.Equal(expected, BellPulseRequests(builder));
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheResponseIsElevenPulsesBack_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { BellPulsesAfterResponse = 10 };
        ServeBuilt(builder);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("within 10 Bell pulses", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheBellChainHasNoResponse_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { OmitBellResponse = true };
        ServeBuilt(builder);
        var beacon = Create();
        Func<CancellationToken, ValueTask<BeaconPulse>> fetch = token =>
            beacon.GetPulseAsync(builder.Round, token);

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            fetch,
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains(
            "The Bell chain starts before",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetPulseAsync_WhenEverySeedPulseMixesInThePrecommit_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { OmitSeedPulseZero = true };
        ServeBuilt(builder);
        var beacon = Create();
        Func<CancellationToken, ValueTask<BeaconPulse>> fetch = token =>
            beacon.GetPulseAsync(builder.Round, token);

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            fetch,
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains(
            "The seed chain starts before",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerThrowsUnexpectedly_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        var roundPath = RoundPathOf(builder.Round);
        var boom = new InvalidOperationException("boom");
        Server.Fail(roundPath, boom);
        var beacon = Create();
        Func<CancellationToken, ValueTask<BeaconPulse>> fetch = token =>
            beacon.GetPulseAsync(builder.Round, token);

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            fetch,
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("cannot be read", exception.Message, StringComparison.Ordinal);
        Assert.Same(boom, exception.InnerException);
    }

    [Fact]
    public async Task GetPulseAsync_WithSeedCountOfThree_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 3 };
        var round = ServeBuilt(builder);
        var beacon = Create();

        var pulse = await beacon.GetPulseAsync(builder.Round, CancellationToken.None);

        Assert.Equal(round.Randomness, pulse.Randomness);
    }

    [Fact]
    public async Task GetPulseAsync_WhenNineSeedPulsesMixInThePrecommit_Succeeds()
    {
        using var builder = new QuantumRoundBuilder { SeedCount = 8 };
        var round = ServeBuilt(builder);
        var beacon = Create();

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
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("seed pulses in a row", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenThePrecommitDoesNotMixInTheBellChain_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder { PrecommitMixesBell = false };
        ServeBuilt(builder);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
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
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
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
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("not twine/1.x/bell/1.x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheRoundFailed_IsNotFound()
    {
        const string reason = "the Bell test failed";
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        var roundPath = RoundPathOf(builder.Round);
        var errorRoundJson = builder.ErrorRoundJson(reason);
        Server.Serve(roundPath, errorRoundJson);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheRoundIsIncomplete_IsNotFound()
    {
        using var builder = new QuantumRoundBuilder();
        var round = builder.Build();
        ServeChains(builder);
        var roundPath = RoundPathOf(builder.Round);
        Server.Serve(roundPath, $"[{round.Request.Json},{round.Precommit.Json}]");
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains("has not completed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenNoRoundIsPublished_IsNotFound()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        var roundPath = RoundPathOf(builder.Round);
        Server.Serve(roundPath, "[]");
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Contains("has not published round", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerAnswersAnotherRound_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var round = ServeBuilt(builder);
        var nextRoundPath = RoundPathOf(builder.Round + 1);
        Server.Serve(nextRoundPath, round.RoundJson);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round + 1, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
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
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("parameters of round", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerSwapsABellPulse_IsInvalid()
    {
        using var builder = new QuantumRoundBuilder();
        var round = ServeBuilt(builder);
        var bellPulsePath = BellPulsePath(builder, round, 1);
        Server.Serve(bellPulsePath, round.BellPulses[0].Json);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.InvalidPulse,
            CancellationToken.None
        );

        Assert.Contains("answered pulse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheServerFails_IsUnavailable()
    {
        using var builder = new QuantumRoundBuilder();
        Options.QuantumChainId = builder.Quantum.ChainCid.ToString();
        Server.Answer("/chains/" + builder.Quantum.ChainCid, HttpStatusCode.ServiceUnavailable);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheBodyIsCorrupt_IsUnavailable()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        var roundPath = RoundPathOf(builder.Round);
        var corrupt = new InvalidDataException("corrupt");
        Server.Fail(roundPath, corrupt);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(builder.Round, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.Same(corrupt, exception.InnerException);
    }

    [Fact]
    public async Task GetPulseAsync_WithNegativeRound_IsNotFound()
    {
        var beacon = Create();

        await BeaconAssert.AssertBeaconErrorAsync(
            token => beacon.GetPulseAsync(-1, token),
            CurbyDefaults.QuantumBeaconName,
            BeaconError.PulseNotFound,
            CancellationToken.None
        );

        Assert.Empty(Server.Requests);
    }

    [Fact]
    public async Task GetPulseAsync_WhenTheCallerCancels_Throws()
    {
        using var builder = new QuantumRoundBuilder();
        ServeBuilt(builder);
        var beacon = Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var token = cancellation.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await beacon.GetPulseAsync(builder.Round, token)
        );
    }

    [Fact]
    public async Task GetPulseAsync_ForTheSameRoundTwice_ReusesTheVerifiedPulse()
    {
        ServeCurbyQ();
        var beacon = Create();
        var first = await beacon.GetPulseAsync(CurbyQuantumFixtures.Round, CancellationToken.None);
        Server.Requests.Clear();

        var second = await beacon.GetPulseAsync(CurbyQuantumFixtures.Round, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal([RoundPath], Server.Requests);
    }

    #endregion

    #region GetLatestPulseAsync Tests

    [Fact]
    public async Task GetLatestPulseAsync_WithCurbyQData_ReturnsTheLatestRound()
    {
        ServeCurbyQ();
        var beacon = Create();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);

        AssertRound(pulse);
        Assert.Equal(LatestPath, Server.Requests[1]);
    }

    [Fact]
    public async Task GetLatestPulseAsync_ForTheSameRound_ReusesTheVerifiedPulse()
    {
        ServeCurbyQ();
        var beacon = Create();
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
        var errorRoundJson = builder.ErrorRoundJson(reason);
        Server.Serve(LatestPath, errorRoundJson);
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.QuantumBeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestPulseAsync_WhenThereIsNoRound_IsUnavailable()
    {
        using var builder = new QuantumRoundBuilder();
        ServeChains(builder);
        Server.Serve(LatestPath, "[]");
        var beacon = Create();

        var exception = await BeaconAssert.AssertBeaconErrorAsync(
            beacon.GetLatestPulseAsync,
            CurbyDefaults.QuantumBeaconName,
            BeaconError.Unavailable,
            CancellationToken.None
        );

        Assert.Contains("no latest round", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    // The requests for the pulses of the builder's Bell chain, in the order they were made.
    private List<string> BellPulseRequests(QuantumRoundBuilder builder)
    {
        var prefix = "/chains/" + builder.Bell.ChainCid + "/pulses/";
        return
        [
            .. Server.Requests.Where(path => path.StartsWith(prefix, StringComparison.Ordinal)),
        ];
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
        var roundPath = RoundPathOf(builder.Round);
        Server.Serve(roundPath, round.RoundJson).Serve(LatestPath, round.RoundJson);
        for (var i = 0; i < round.BellPulses.Count; i++)
        {
            var bellPulsePath = BellPulsePath(builder, round, i);
            Server.Serve(bellPulsePath, round.BellPulses[i].Json);
        }

        foreach (var (json, cid) in round.SeedPulses)
        {
            Server.Serve("/chains/" + builder.Seed.ChainCid + "/pulses/" + cid, json);
        }

        if (serveParameters)
        {
            Server.Serve(roundPath + "/params", round.Parameters, "application/octet-stream");
        }

        return round;
    }

    private void ServeCurbyQ()
    {
        var bellResponse = CurbyQuantumFixtures.BellPulse(CurbyQuantumFixtures.BellResponseIndex);
        Server
            .Serve(ChainPath, CurbyQuantumFixtures.Chain)
            .Serve(RoundPath, CurbyQuantumFixtures.RoundJson)
            .Serve(LatestPath, CurbyQuantumFixtures.RoundJson)
            .Serve(BellChainPath, CurbyQuantumFixtures.BellChain)
            .Serve(BellChainPath + "/pulses/" + CurbyQuantumFixtures.BellResponseId, bellResponse)
            .Serve(SeedChainPath, CurbyQuantumFixtures.SeedChain)
            .Serve(ParametersPath, CurbyQuantumFixtures.Parameters, "application/octet-stream");
        long[] indexes =
        [
            CurbyQuantumFixtures.ThirdSeedPulseIndex,
            CurbyQuantumFixtures.SecondSeedPulseIndex,
            CurbyQuantumFixtures.FirstSeedPulseIndex,
        ];
        foreach (var index in indexes)
        {
            var seedPulsePath = SeedPulsePath(index);
            var seedPulse = CurbyQuantumFixtures.SeedPulse(index);
            Server.Serve(seedPulsePath, seedPulse);
        }
    }

    private CurbyQuantumBeacon Create()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var (api, chains) = Services.Create(Server, options);
        return new CurbyQuantumBeacon(api, chains, options);
    }
}
