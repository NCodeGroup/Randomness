// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
}
