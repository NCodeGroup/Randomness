// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Http;

namespace NCode.Randomness.Curby.Tests;

public class CurbyRandomnessBuilderExtensionsTests
{
    #region AddCurbyBeacon Tests

    [Fact]
    public async Task AddCurbyBeacon_Twice_RegistersOneBeaconNamedCurby()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon().AddCurbyBeacon();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true }
        );

        var beacon = Assert.Single(provider.GetServices<IRandomnessBeacon>());

        Assert.Equal(CurbyDefaults.BeaconName, beacon.Name);
        Assert.Same(beacon, provider.GetRequiredService<CurbyBeacon>());
        Assert.Equal(
            CurbyDefaults.BeaconName,
            Assert.Single(provider.GetRequiredService<IBeaconStatus>().Beacons).Name
        );
    }

    [Fact]
    public async Task AddCurbyBeacon_Always_RegistersTheNamedClientWithItsResilience()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon();
        await using var provider = services.BuildServiceProvider();

        using var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(CurbyDefaults.HttpClientName);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddCurbyBeacon_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon(options => options.MaxResponseBytes = 4096);
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(
            4096,
            provider.GetRequiredService<IOptions<CurbyBeaconOptions>>().Value.MaxResponseBytes
        );
    }

    [Fact]
    public async Task AddCurbyBeacon_WithConfiguration_BindsTheSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("BaseAddress", (string?)"https://mirror.example/twine/"),
            ])
            .Build();
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon(configuration);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CurbyBeaconOptions>>().Value;

        Assert.Equal("https://mirror.example/twine/", options.BaseAddress.OriginalString);
    }

    [Fact]
    public async Task AddCurbyBeacon_WithOneConfiguration_BindsBothSectionsStrictly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("Randomness:Mode", (string?)"InitializeOnly"),
                KeyValuePair.Create("RandomnessBeacons:Curby:MaxResponseBytes", (string?)"8192"),
            ])
            .Build();
        var services = new ServiceCollection();
        services
            .AddRandomness(configuration.GetSection(RandomnessOptions.SectionName))
            .AddCurbyBeacon(configuration.GetSection(CurbyBeaconOptions.SectionName));
        await using var provider = services.BuildServiceProvider();

        var randomness = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;
        var curby = provider.GetRequiredService<IOptions<CurbyBeaconOptions>>().Value;

        Assert.Equal(BeaconMode.InitializeOnly, randomness.Mode);
        Assert.Equal(8192, curby.MaxResponseBytes);
    }

    [Fact]
    public async Task AddCurbyBeacon_WithInvalidOptions_FailsTheHostAtStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddCurbyBeacon(options => options.BaseAddress = new Uri("http://api.entwine.me/"));
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains(
            "RandomnessBeacons:Curby:BaseAddress",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region AddCurbyQuantumBeacon Tests

    [Fact]
    public async Task AddCurbyQuantumBeacon_Twice_RegistersOneBeaconNamedCurbyQ()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyQuantumBeacon().AddCurbyQuantumBeacon();
        var providerOptions = new ServiceProviderOptions { ValidateOnBuild = true };
        await using var provider = services.BuildServiceProvider(providerOptions);

        var beacons = provider.GetServices<IRandomnessBeacon>();
        var beacon = Assert.Single(beacons);
        var resolved = provider.GetRequiredService<CurbyQuantumBeacon>();
        var status = provider.GetRequiredService<IBeaconStatus>();
        var beaconStatus = Assert.Single(status.Beacons);

        Assert.Equal(CurbyDefaults.QuantumBeaconName, beacon.Name);
        Assert.Same(beacon, resolved);
        Assert.Equal(CurbyDefaults.QuantumBeaconName, beaconStatus.Name);
    }

    [Fact]
    public async Task AddCurbyBeacon_WithTheQuantumBeacon_RegistersBothBeaconsOverOneClient()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon().AddCurbyQuantumBeacon();
        var providerOptions = new ServiceProviderOptions { ValidateOnBuild = true };
        await using var provider = services.BuildServiceProvider(providerOptions);

        var names = provider.GetServices<IRandomnessBeacon>().Select(beacon => beacon.Name);
        var orderedNames = names.Order(StringComparer.Ordinal);

        Assert.Equal([CurbyDefaults.BeaconName, CurbyDefaults.QuantumBeaconName], orderedNames);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TwineApi));
    }

    [Fact]
    public async Task AddCurbyBeacon_WithTheQuantumBeacon_SharesOneChainCache()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon().AddCurbyQuantumBeacon();
        var providerOptions = new ServiceProviderOptions { ValidateOnBuild = true };
        await using var provider = services.BuildServiceProvider(providerOptions);

        var chains = provider.GetRequiredService<TwineChainCache>();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TwineChainCache));
        var again = provider.GetRequiredService<TwineChainCache>();
        Assert.Same(chains, again);
    }

    [Fact]
    public async Task AddCurbyQuantumBeacon_WithConfiguration_BindsTheSection()
    {
        var chainSetting = KeyValuePair.Create(
            "QuantumChainId",
            (string?)CurbyDefaults.QuantumChainId
        );
        var maxBytesSetting = KeyValuePair.Create("MaxResponseBytes", (string?)"8192");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([chainSetting, maxBytesSetting])
            .Build();
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyQuantumBeacon(configuration);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CurbyBeaconOptions>>().Value;

        Assert.Equal(CurbyDefaults.QuantumChainId, options.QuantumChainId);
        Assert.Equal(8192, options.MaxResponseBytes);
    }

    [Fact]
    public async Task AddCurbyQuantumBeacon_ByDefault_AllowsThirtyMinuteOldPulses()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddCurbyBeacon().AddCurbyQuantumBeacon();
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;

        var limit = Assert.Single(options.MaxPulseAgeByBeacon);
        var expectedLimit = TimeSpan.FromMinutes(30);
        Assert.Equal(CurbyDefaults.QuantumBeaconName, limit.Key);
        Assert.Equal(expectedLimit, limit.Value);
        Assert.DoesNotContain(CurbyDefaults.BeaconName, options.MaxPulseAgeByBeacon.Keys);
    }

    [Fact]
    public async Task AddCurbyQuantumBeacon_WithHostLimit_KeepsIt()
    {
        var services = new ServiceCollection();
        var hostLimit = TimeSpan.FromMinutes(45);
        services
            .AddRandomness(options =>
                options.MaxPulseAgeByBeacon[CurbyDefaults.QuantumBeaconName] = hostLimit
            )
            .AddCurbyQuantumBeacon();
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;

        var limit = options.MaxPulseAgeByBeacon[CurbyDefaults.QuantumBeaconName];
        Assert.Equal(hostLimit, limit);
    }

    [Fact]
    public async Task AddCurbyQuantumBeacon_WithLongerMaxPulseAge_AddsNoLimit()
    {
        var services = new ServiceCollection();
        var hostMaxAge = TimeSpan.FromHours(1);
        services.AddRandomness(options => options.MaxPulseAge = hostMaxAge).AddCurbyQuantumBeacon();
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;

        Assert.Empty(options.MaxPulseAgeByBeacon);
    }

    #endregion
}
