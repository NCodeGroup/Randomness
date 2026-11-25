// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
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

        var beacons = provider.GetServices<IRandomnessBeacon>();
        var beacon = Assert.Single(beacons);
        var nistBeacon = provider.GetRequiredService<NistBeacon>();
        var status = provider.GetRequiredService<IBeaconStatus>();
        var statusBeacon = Assert.Single(status.Beacons);

        Assert.Equal(NistDefaults.BeaconName, beacon.Name);
        Assert.Same(beacon, nistBeacon);
        Assert.Equal(NistDefaults.BeaconName, statusBeacon.Name);
    }

    [Fact]
    public async Task AddNistBeacon_Always_RegistersTheNamedClientWithItsResilience()
    {
        using var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon();
        services
            .AddHttpClient(NistDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        HttpStandardResilienceOptions? resilience = null;
        services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.Delay = TimeSpan.Zero;
            resilience = options;
        });
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(NistDefaults.HttpClientName);
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
    public async Task AddNistBeacon_WithConfigure_AppliesTheOptions()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddNistBeacon(options => options.MaxResponseBytes = 4096);
        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<NistBeaconOptions>>();

        Assert.Equal(4096, options.Value.MaxResponseBytes);
    }

    [Fact]
    public async Task AddNistBeacon_WithConfiguration_BindsTheSection()
    {
        KeyValuePair<string, string?>[] entries =
        [
            KeyValuePair.Create("BaseAddress", (string?)"https://mirror.example/beacon/2.0/"),
            KeyValuePair.Create("ChainIndex", (string?)"3"),
            KeyValuePair.Create("CertificateIds:0", (string?)ConfiguredId),
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
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
        KeyValuePair<string, string?>[] entries =
        [
            KeyValuePair.Create("Randomness:Mode", (string?)"InitializeOnly"),
            KeyValuePair.Create("RandomnessBeacons:Nist:MaxResponseBytes", (string?)"8192"),
        ];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
        var randomnessSection = configuration.GetSection(RandomnessOptions.SectionName);
        var nistSection = configuration.GetSection(NistBeaconOptions.SectionName);
        var services = new ServiceCollection();
        services.AddRandomness(randomnessSection).AddNistBeacon(nistSection);
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
        KeyValuePair<string, string?>[] entries = [KeyValuePair.Create("Unknown", (string?)"1")];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();
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
