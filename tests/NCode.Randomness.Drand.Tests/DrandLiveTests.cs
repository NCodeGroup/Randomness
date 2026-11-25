// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness.Drand.Tests.Fixtures;

namespace NCode.Randomness.Drand.Tests;

/// <summary>
/// End-to-end checks against the real drand beacon, run only on request (see <see cref="LiveFactAttribute"/>).
/// </summary>
public class DrandLiveTests
{
    [LiveFact]
    public async Task GetLatestPulseAsync_AgainstDrand_ReturnsARecentVerifiedPulse()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<DrandBeacon>();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);
        var round = await beacon.GetPulseAsync(pulse.Round - 10, CancellationToken.None);

        Assert.InRange(
            DateTimeOffset.UtcNow - pulse.Timestamp,
            TimeSpan.FromMinutes(-2),
            TimeSpan.FromMinutes(5)
        );
        Assert.Equal(32, pulse.Randomness.Length);
        Assert.Equal(pulse.Round - 10, round.Round);
    }

    [LiveFact]
    public async Task GetPulseAsync_AgainstDrand_VerifiesAPublishedRound()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<DrandBeacon>();

        var pulse = await beacon.GetPulseAsync(1000, CancellationToken.None);

        Assert.Equal(
            DrandFixtures.FirstRandomness,
            Convert.ToHexStringLower(pulse.Randomness.ToArray())
        );
    }

    [LiveFact]
    public async Task Host_AgainstDrand_MixesAPulseBeforeItStarts()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.RequireInitialPulse = true)
            .AddDrandBeacon();
        using var host = builder.Build();

        await host.StartAsync();
        var status = host.Services.GetRequiredService<IBeaconStatus>();
        var value = host.Services.GetRequiredService<ISecureRandom>().NextInteger(1, 7);
        await host.StopAsync();

        Assert.Equal(BeaconHealth.Healthy, Assert.Single(status.Beacons).Health);
        Assert.InRange(value, 1, 6);
    }
}
