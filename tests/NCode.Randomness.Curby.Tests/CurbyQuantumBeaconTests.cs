// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
