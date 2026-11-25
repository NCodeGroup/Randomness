// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness.Curby.Tests.Fixtures;

namespace NCode.Randomness.Curby.Tests;

/// <summary>
/// End-to-end checks against the real CURBy API, run only on request (see <see cref="LiveFactAttribute"/>).
/// </summary>
public class CurbyLiveTests
{
    [LiveFact]
    public async Task GetLatestPulseAsync_AgainstCurby_ReturnsARecentVerifiedPulse()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<CurbyBeacon>();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);
        var round = await beacon.GetPulseAsync(pulse.Round - 10, CancellationToken.None);

        Assert.InRange(
            DateTimeOffset.UtcNow - pulse.Timestamp,
            TimeSpan.FromMinutes(-2),
            TimeSpan.FromMinutes(5)
        );
        Assert.Equal(64, pulse.Randomness.Length);
        Assert.Equal(pulse.Round - 10, round.Round);
    }

    [LiveFact]
    public async Task GetPulseAsync_AgainstCurbyQ_VerifiesAPublishedRound()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyQuantumBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<CurbyQuantumBeacon>();

        var pulse = await beacon.GetPulseAsync(CurbyQuantumFixtures.Round, CancellationToken.None);
        var latest = await beacon.GetLatestPulseAsync(CancellationToken.None);

        var randomness = Convert.ToHexStringLower(pulse.Randomness);

        Assert.Equal(CurbyQuantumFixtures.ResultId, pulse.Id);
        Assert.Equal(CurbyQuantumFixtures.Randomness, randomness);
        Assert.True(latest.Round >= CurbyQuantumFixtures.Round);
    }

    [LiveFact]
    public async Task Host_AgainstCurby_MixesAPulseBeforeItStarts()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.RequireInitialPulse = true)
            .AddCurbyBeacon();
        using var host = builder.Build();

        await host.StartAsync();
        var status = host.Services.GetRequiredService<IBeaconStatus>();
        var value = host.Services.GetRequiredService<ISecureRandom>().NextInteger(1, 7);
        await host.StopAsync();

        Assert.Equal(BeaconHealth.Healthy, Assert.Single(status.Beacons).Health);
        Assert.InRange(value, 1, 6);
    }
}
