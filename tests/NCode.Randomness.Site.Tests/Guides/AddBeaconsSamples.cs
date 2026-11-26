// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Drand;
using NCode.Randomness.Nist;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class AddBeaconsSamples
{
    [Fact]
    public void SiteAddBeaconsAllSample_Runs()
    {
        // begin-snippet: SiteAddBeaconsAll
        var services = new ServiceCollection();
        services
            .AddRandomness()
            .AddCurbyBeacon()
            .AddCurbyQuantumBeacon()
            .AddNistBeacon()
            .AddDrandBeacon();
        // end-snippet

        using var provider = services.BuildServiceProvider();
        var names = provider
            .GetServices<IRandomnessBeacon>()
            .Select(beacon => beacon.Name);
        Assert.Equal(["curby", "curby-q", "nist", "drand"], names);
    }

    [Fact]
    public void SiteAddBeaconsConfigurationSample_Runs()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["RandomnessBeacons:Curby:BaseAddress"] =
            "https://twine-mirror.example/";

        // begin-snippet: SiteAddBeaconsConfiguration
        var randomness = builder.Configuration.GetSection("Randomness");
        var curby = builder.Configuration.GetSection("RandomnessBeacons:Curby");
        builder.Services.AddRandomness(randomness).AddCurbyBeacon(curby);
        // end-snippet

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<CurbyBeaconOptions>>();
        Assert.Equal(new Uri("https://twine-mirror.example/"), options.Value.BaseAddress);
    }

    [Fact]
    public void SiteAddBeaconsHttpClientSample_Runs()
    {
        // begin-snippet: SiteAddBeaconsHttpClient
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon();
        services
            .AddHttpClient(CurbyDefaults.HttpClientName)
            .ConfigureHttpClient(client =>
                client.DefaultRequestHeaders.UserAgent.ParseAdd("my-service/1.0")
            );
        // end-snippet

        using var provider = services.BuildServiceProvider();
        var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(CurbyDefaults.HttpClientName);
        Assert.Contains(
            "my-service/1.0",
            client.DefaultRequestHeaders.UserAgent.ToString()
        );
    }
}
