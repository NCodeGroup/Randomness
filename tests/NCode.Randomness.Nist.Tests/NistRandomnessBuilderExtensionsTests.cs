// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NCode.Randomness.Nist.Tests;

public class NistRandomnessBuilderExtensionsTests
{
    private static readonly string ConfiguredId = new('b', 128);

    #region AddNistBeacon Tests

    [Fact]
    public async Task AddNistBeacon_Twice_RegistersOneBeaconNamedNist()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon().AddNistBeacon();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true }
        );

        var beacon = Assert.Single(provider.GetServices<IRandomnessBeacon>());

        Assert.Equal(NistDefaults.BeaconName, beacon.Name);
        Assert.Same(beacon, provider.GetRequiredService<NistBeacon>());
        Assert.Equal(
            NistDefaults.BeaconName,
            Assert.Single(provider.GetRequiredService<IBeaconStatus>().Beacons).Name
        );
    }

    [Fact]
    public async Task AddNistBeacon_Always_RegistersTheNamedClientWithItsResilience()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon();
        await using var provider = services.BuildServiceProvider();

        using var client = provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(NistDefaults.HttpClientName);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddNistBeacon_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon(options => options.MaxResponseBytes = 4096);
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(
            4096,
            provider.GetRequiredService<IOptions<NistBeaconOptions>>().Value.MaxResponseBytes
        );
    }

    [Fact]
    public async Task AddNistBeacon_WithConfiguration_BindsTheSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("BaseAddress", (string?)"https://mirror.example/beacon/2.0/"),
                KeyValuePair.Create("ChainIndex", (string?)"3"),
                KeyValuePair.Create("CertificateIds:0", (string?)ConfiguredId),
            ])
            .Build();
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon(configuration);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<NistBeaconOptions>>().Value;

        Assert.Equal("https://mirror.example/beacon/2.0/", options.BaseAddress.OriginalString);
        Assert.Equal(3, options.ChainIndex);
        Assert.Equal([ConfiguredId], options.CertificateIds);
    }

    [Fact]
    public async Task AddNistBeacon_WithOneConfiguration_BindsBothSectionsStrictly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                KeyValuePair.Create("Randomness:Mode", (string?)"InitializeOnly"),
                KeyValuePair.Create("RandomnessBeacons:Nist:MaxResponseBytes", (string?)"8192"),
            ])
            .Build();
        var services = new ServiceCollection();
        services
            .AddRandomness(configuration.GetSection(RandomnessOptions.SectionName))
            .AddNistBeacon(configuration.GetSection(NistBeaconOptions.SectionName));
        await using var provider = services.BuildServiceProvider();

        var randomness = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;
        var nist = provider.GetRequiredService<IOptions<NistBeaconOptions>>().Value;

        Assert.Equal(BeaconMode.InitializeOnly, randomness.Mode);
        Assert.Equal(8192, nist.MaxResponseBytes);
    }

    [Fact]
    public async Task AddNistBeacon_WithInvalidOptions_FailsTheHostAtStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddNistBeacon(options => options.BaseAddress = new Uri("http://beacon.nist.gov/"));
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains(
            "RandomnessBeacons:Nist:BaseAddress",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AddNistBeacon_WithAnUnknownConfigurationKey_FailsTheHostAtStart()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create("Unknown", (string?)"1")])
            .Build();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddNistBeacon(configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.StartAsync()
        );

        Assert.Contains("Unknown", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
