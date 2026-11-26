// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Nist;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class ChooseAModeSamples
{
    [Fact]
    public void SiteChooseAModeInitializeOnlySample_Runs()
    {
        // begin-snippet: SiteChooseAModeInitializeOnly
        var services = new ServiceCollection();
        services.AddRandomness(options =>
        {
            options.Mode = BeaconMode.InitializeOnly;
            options.RequireInitialPulse = true;
            options.InitialPulseTimeout = TimeSpan.FromSeconds(45);
        });
        // end-snippet

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;
        Assert.Equal(BeaconMode.InitializeOnly, options.Mode);
        Assert.True(options.RequireInitialPulse);
        Assert.Equal(TimeSpan.FromSeconds(45), options.InitialPulseTimeout);
    }

    [Fact]
    public async Task SiteChooseAModeOfflineSample_Runs()
    {
        // begin-snippet: SiteChooseAModeOffline
        var builder = RandomnessHost.CreateBuilder(options =>
            options.Mode = BeaconMode.Offline
        );
        builder.AddCurbyBeacon().AddNistBeacon();
        await using var host = await builder.BuildAsync(CancellationToken.None);
        // end-snippet

        Assert.Equal(BeaconMode.Offline, host.BeaconStatus.Mode);
    }

    [Fact]
    public void SiteChooseAModeMaxPulseAgeSample_Runs()
    {
        // begin-snippet: SiteChooseAModeMaxPulseAge
        var services = new ServiceCollection();
        // curby-q gets 30 minutes from AddCurbyQuantumBeacon(), as it names no limit here.
        services
            .AddRandomness(options =>
                options.MaxPulseAgeByBeacon["nist"] = TimeSpan.FromMinutes(20)
            )
            .AddNistBeacon()
            .AddCurbyQuantumBeacon();
        // end-snippet

        using var provider = services.BuildServiceProvider();
        var ages = provider
            .GetRequiredService<IOptions<RandomnessOptions>>()
            .Value.MaxPulseAgeByBeacon;
        Assert.Equal(TimeSpan.FromMinutes(20), ages["nist"]);
        Assert.Equal(TimeSpan.FromMinutes(30), ages["curby-q"]);
    }
}
