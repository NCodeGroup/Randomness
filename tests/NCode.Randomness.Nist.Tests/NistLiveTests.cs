// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness.Nist.Tests.Fixtures;

namespace NCode.Randomness.Nist.Tests;

/// <summary>
/// End-to-end checks against the real NIST Randomness Beacon, run only on request (see <see cref="LiveFactAttribute"/>).
/// </summary>
public class NistLiveTests
{
    [LiveFact]
    public async Task GetLatestPulseAsync_AgainstNist_ReturnsARecentVerifiedPulse()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<NistBeacon>();

        var pulse = await beacon.GetLatestPulseAsync(CancellationToken.None);
        var round = await beacon.GetPulseAsync(pulse.Round - 10, CancellationToken.None);

        var age = DateTimeOffset.UtcNow - pulse.Timestamp;
        var earliest = TimeSpan.FromMinutes(-2);
        var latest = TimeSpan.FromMinutes(5);

        Assert.InRange(age, earliest, latest);
        Assert.Equal(64, pulse.Randomness.Length);
        Assert.Equal(pulse.Round - 10, round.Round);
    }

    [LiveFact]
    public async Task GetPulseAsync_AgainstNist_VerifiesAPublishedPulse()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon();
        await using var provider = services.BuildServiceProvider();
        var beacon = provider.GetRequiredService<NistBeacon>();

        var pulse = await beacon.GetPulseAsync(NistFixtures.LatestIndex, CancellationToken.None);

        Assert.Equal(NistFixtures.LatestOutput, pulse.Id);
    }

    [LiveFact]
    public async Task Host_AgainstNist_MixesAPulseBeforeItStarts()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.RequireInitialPulse = true)
            .AddNistBeacon();
        using var host = builder.Build();

        await host.StartAsync();
        var status = host.Services.GetRequiredService<IBeaconStatus>();
        var value = host.Services.GetRequiredService<ISecureRandom>().NextInteger(1, 7);
        await host.StopAsync();

        var onlyBeacon = Assert.Single(status.Beacons);

        Assert.Equal(BeaconHealth.Healthy, onlyBeacon.Health);
        Assert.InRange(value, 1, 6);
    }
}
