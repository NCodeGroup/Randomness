// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Randomness.Generation;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.DependencyInjection;

public class RandomnessServiceCollectionExtensionsTests
{
    #region AddRandomness Tests

    [Fact]
    public async Task AddRandomness_Always_RegistersTheSecureGeneratorOnlyUnderItsSecureContract()
    {
        var services = new ServiceCollection();
        services.AddRandomness();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        var secure = provider.GetRequiredService<ISecureRandom>();
        Assert.IsType<DefaultSecureRandom>(secure);
        Assert.Null(provider.GetService<IRandomSource>());
        Assert.NotNull(provider.GetRequiredService<IBeaconStatus>());
        Assert.NotNull(provider.GetRequiredService<IVerifiableRandomFactory>());
        Assert.Single(provider.GetServices<IHostedService>());
        Assert.Equal(16, secure.GetBytes(16).Length);
    }

    [Fact]
    public async Task AddRandomness_WhenConsumerRegisteredAGenerator_KeepsIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISecureRandom>(SystemRandomSource.Instance);
        services.AddRandomness();
        await using var provider = services.BuildServiceProvider();

        Assert.Same(SystemRandomSource.Instance, provider.GetRequiredService<ISecureRandom>());
    }

    [Fact]
    public async Task AddRandomness_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness(options => options.Mode = BeaconMode.Offline);
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(BeaconMode.Offline, provider.GetRequiredService<IBeaconStatus>().Mode);
    }

    [Fact]
    public async Task AddRandomness_WithConfiguration_BindsTheSection()
    {
        var configuration = Configure(("Mode", "InitializeOnly"), ("RefreshInterval", "00:00:30"));
        var services = new ServiceCollection();
        services.AddRandomness(configuration);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;

        Assert.Equal(BeaconMode.InitializeOnly, options.Mode);
        Assert.Equal(TimeSpan.FromSeconds(30), options.RefreshInterval);
    }

    [Fact]
    public async Task AddRandomness_WithConfiguration_BindsBeaconLimits()
    {
        var configuration = Configure(("MaxPulseAgeByBeacon:b", "00:30:00"));
        var services = new ServiceCollection();
        services.AddRandomness(configuration);
        services.AddSingleton<IRandomnessBeacon>(new FakeBeacon("b"));
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(30), options.MaxPulseAgeByBeacon["b"]);
    }

    [Fact]
    public async Task AddRandomness_WithMisspelledConfigurationKey_FailsOnRead()
    {
        var configuration = Configure(("Mdoe", "Offline"));
        var services = new ServiceCollection();
        services.AddRandomness(configuration);
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptions<RandomnessOptions>>().Value
        );

        Assert.Contains("Mdoe", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddRandomness_WithInvalidOptions_FailsTheHostAtStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddRandomness(options => options.RefreshInterval = TimeSpan.Zero);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains("Randomness:RefreshInterval", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddRandomness_InHost_StartsAndStopsInOfflineMode()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder
            .Services.AddRandomness(options => options.Mode = BeaconMode.Offline)
            .AddBeacon<NamedBeacon>();
        using var host = builder.Build();

        await host.StartAsync();
        var value = host.Services.GetRequiredService<ISecureRandom>().NextInteger(1, 7);
        await host.StopAsync();

        Assert.InRange(value, 1, 6);
    }

    #endregion

    #region AddBeacon Tests

    [Fact]
    public async Task AddBeacon_Twice_RegistersTheBeaconOnce()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddBeacon<NamedBeacon>().AddBeacon<NamedBeacon>();
        await using var provider = services.BuildServiceProvider();

        var beacon = Assert.Single(provider.GetServices<IRandomnessBeacon>());
        Assert.Equal("named", beacon.Name);
        Assert.Equal(
            "named",
            Assert.Single(provider.GetRequiredService<IBeaconStatus>().Beacons).Name
        );
    }

    #endregion

    #region AddRandomnessBeacons Tests

    [Fact]
    public async Task AddRandomnessBeacons_Always_RegistersTheCheckWithItsTags()
    {
        var services = new ServiceCollection();
        services.AddRandomness();
        services.AddHealthChecks().AddRandomnessBeacons("ready");
        await using var provider = services.BuildServiceProvider();

        var registration = Assert.Single(
            provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
        );
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(RandomnessHealthChecksBuilderExtensions.BeaconsCheckName, registration.Name);
        Assert.Contains("ready", registration.Tags);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    #endregion

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.Select(value => KeyValuePair.Create(value.Key, (string?)value.Value))
            )
            .Build();

    private sealed class NamedBeacon : IRandomnessBeacon
    {
        public string Name => "named";

        public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Pulses.Create(Name, 1, DateTimeOffset.UtcNow));

        public ValueTask<BeaconPulse> GetPulseAsync(
            long round,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(Pulses.Create(Name, round, DateTimeOffset.UtcNow));
    }
}
