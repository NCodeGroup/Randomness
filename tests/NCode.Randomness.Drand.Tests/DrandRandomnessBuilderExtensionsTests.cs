// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Randomness.Drand.Rounds;

namespace NCode.Randomness.Drand.Tests;

public class DrandRandomnessBuilderExtensionsTests
{
    #region AddDrandBeacon Tests

    [Fact]
    public async Task AddDrandBeacon_Twice_RegistersOneBeaconNamedDrand()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true }
        );

        var beacons = provider.GetServices<IRandomnessBeacon>();
        var beacon = Assert.Single(beacons);
        var drandBeacon = provider.GetRequiredService<DrandBeacon>();
        var status = provider.GetRequiredService<IBeaconStatus>();
        var statusBeacon = Assert.Single(status.Beacons);

        Assert.Equal(DrandDefaults.BeaconName, beacon.Name);
        Assert.Same(beacon, drandBeacon);
        Assert.Equal(DrandDefaults.BeaconName, statusBeacon.Name);
    }

    [Fact]
    public async Task AddDrandBeacon_Always_RegistersTheQuicknetChain()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();

        var chain = provider.GetRequiredService<DrandChain>();

        Assert.Same(DrandChain.Quicknet, chain);
    }

    [Fact]
    public async Task AddDrandBeacon_Always_RegistersTheNamedClientWithItsResilience()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        using var client = factory.CreateClient(DrandDefaults.HttpClientName);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddDrandBeacon_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon(options => options.MaxResponseBytes = 4096);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DrandBeaconOptions>>();

        Assert.Equal(4096, options.Value.MaxResponseBytes);
    }

    [Fact]
    public async Task AddDrandBeacon_WithConfiguration_BindsTheSection()
    {
        KeyValuePair<string, string?>[] entries =
        [
            KeyValuePair.Create("BaseAddress", (string?)"https://mirror.example/"),
            KeyValuePair.Create("MaxResponseBytes", (string?)"4096"),
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon(configuration);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DrandBeaconOptions>>().Value;

        Assert.Equal("https://mirror.example/", options.BaseAddress.OriginalString);
        Assert.Equal(4096, options.MaxResponseBytes);
    }

    [Fact]
    public async Task AddDrandBeacon_WithOneConfiguration_BindsBothSectionsStrictly()
    {
        KeyValuePair<string, string?>[] entries =
        [
            KeyValuePair.Create("Randomness:Mode", (string?)"InitializeOnly"),
            KeyValuePair.Create("RandomnessBeacons:Drand:MaxResponseBytes", (string?)"8192"),
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        var randomnessSection = configuration.GetSection(RandomnessOptions.SectionName);
        var drandSection = configuration.GetSection(DrandBeaconOptions.SectionName);
        var services = new ServiceCollection();
        services.AddRandomness(randomnessSection).AddDrandBeacon(drandSection);
        await using var provider = services.BuildServiceProvider();

        var randomness = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;
        var drand = provider.GetRequiredService<IOptions<DrandBeaconOptions>>().Value;

        Assert.Equal(BeaconMode.InitializeOnly, randomness.Mode);
        Assert.Equal(8192, drand.MaxResponseBytes);
    }

    [Fact]
    public async Task AddDrandBeacon_WithInvalidOptions_FailsTheHostAtStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddDrandBeacon(options => options.BaseAddress = new Uri("http://api.drand.sh/"));
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains(
            "RandomnessBeacons:Drand:BaseAddress",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AddDrandBeacon_WithAnUnknownConfigurationKey_FailsTheHostAtStart()
    {
        KeyValuePair<string, string?>[] entries = [KeyValuePair.Create("Unknown", (string?)"1")];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddDrandBeacon(configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.StartAsync()
        );

        Assert.Contains("Unknown", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
