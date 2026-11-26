// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Drand;
using NCode.Randomness.Nist;

namespace SiteSamples.Start;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class QuickStartSamples
{
    [Fact]
    public void SiteQuickStartRegisterSample_Runs()
    {
        // begin-snippet: SiteQuickStartRegister
        var builder = Host.CreateApplicationBuilder();
        builder
            .Services.AddRandomness()
            .AddCurbyBeacon()
            .AddNistBeacon()
            .AddDrandBeacon();
        builder.Services.AddHealthChecks().AddRandomnessBeacons("ready");

        using var host = builder.Build();
        var random = host.Services.GetRequiredService<ISecureRandom>();
        // end-snippet

        var names = host
            .Services.GetServices<IRandomnessBeacon>()
            .Select(beacon => beacon.Name);
        Assert.Equal(["curby", "nist", "drand"], names);
        Assert.NotNull(random);
    }
}
