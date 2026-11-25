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

        var beacon = Assert.Single(provider.GetServices<IRandomnessBeacon>());

        Assert.Equal(DrandDefaults.BeaconName, beacon.Name);
        Assert.Same(beacon, provider.GetRequiredService<DrandBeacon>());
        Assert.Equal(
            DrandDefaults.BeaconName,
            Assert.Single(provider.GetRequiredService<IBeaconStatus>().Beacons).Name
        );
    }

    [Fact]
    public async Task AddDrandBeacon_Always_RegistersTheQuicknetChain()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();

        Assert.Same(DrandChain.Quicknet, provider.GetRequiredService<DrandChain>());
    }

    [Fact]
    public async Task AddDrandBeacon_Always_RegistersTheNamedClientWithItsResilience()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        await using var provider = services.BuildServiceProvider();

        using var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(DrandDefaults.HttpClientName);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddDrandBeacon_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon(options => options.MaxResponseBytes = 4096);
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(
            4096,
            provider.GetRequiredService<IOptions<DrandBeaconOptions>>().Value.MaxResponseBytes
        );
    }

    [Fact]
    public async Task AddDrandBeacon_WithConfiguration_BindsTheSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("BaseAddress", (string?)"https://mirror.example/"),
                KeyValuePair.Create("MaxResponseBytes", (string?)"4096"),
            ])
            .Build();
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("Randomness:Mode", (string?)"InitializeOnly"),
                KeyValuePair.Create("RandomnessBeacons:Drand:MaxResponseBytes", (string?)"8192"),
            ])
            .Build();
        var services = new ServiceCollection();
        services
            .AddRandomness(configuration.GetSection(RandomnessOptions.SectionName))
            .AddDrandBeacon(configuration.GetSection(DrandBeaconOptions.SectionName));
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create("Unknown", (string?)"1")])
            .Build();
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
