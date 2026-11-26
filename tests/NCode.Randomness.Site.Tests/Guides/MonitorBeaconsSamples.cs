// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Nist;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class MonitorBeaconsSamples
{
    [Fact]
    public async Task SiteMonitorBeaconsHealthSample_Runs()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Randomness:Mode"] = "Offline";
        var cancellationToken = CancellationToken.None;

        // begin-snippet: SiteMonitorBeaconsHealth
        var section = builder.Configuration.GetSection("Randomness");
        builder.Services.AddRandomness(section).AddCurbyBeacon();
        builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");

        using var host = builder.Build();
        var health = host.Services.GetRequiredService<HealthCheckService>();
        var report = await health.CheckHealthAsync(
            check => check.Tags.Contains("ready"),
            cancellationToken
        );
        // report.Status: Healthy, or Degraded while a beacon is unavailable;
        // never Unhealthy
        // end-snippet

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains("randomness-beacons", report.Entries.Keys);
    }

    [Fact]
    public async Task SiteMonitorBeaconsStatusSample_Runs()
    {
        var builder = RandomnessHost.CreateBuilder(options =>
            options.Mode = BeaconMode.Offline
        );
        builder.AddCurbyBeacon().AddNistBeacon();
        await using var randomness = await builder.BuildAsync(CancellationToken.None);
        var lines = new List<string>();

        // begin-snippet: SiteMonitorBeaconsStatus
        var status = randomness.BeaconStatus;
        foreach (var beacon in status.Beacons)
        {
            var failures = beacon.ConsecutiveFailures;
            lines.Add($"{beacon.Name}: {beacon.Health}, failures {failures}");
        }
        // end-snippet

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("curby:", lines[0]);
        Assert.StartsWith("nist:", lines[1]);
    }
}
