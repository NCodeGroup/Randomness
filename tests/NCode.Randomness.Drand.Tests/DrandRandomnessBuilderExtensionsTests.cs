// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
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
        using var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddRandomness().AddDrandBeacon();
        services
            .AddHttpClient(DrandDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        HttpStandardResilienceOptions? resilience = null;
        services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.Delay = TimeSpan.Zero;
            resilience = options;
        });
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(DrandDefaults.HttpClientName);
        var address = new Uri("https://beacon.example/");

        using var response = await client.GetAsync(address, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, handler.Requests);
        Assert.NotNull(resilience);
        Assert.Equal(TimeSpan.FromSeconds(10), resilience.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(25), resilience.TotalRequestTimeout.Timeout);
        Assert.Equal(2, resilience.Retry.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(60), resilience.CircuitBreaker.SamplingDuration);
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

    /// <summary>
    /// A primary handler that counts its requests and answers each with 503.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets the number of requests the handler has received.
        /// </summary>
        public int Requests { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
