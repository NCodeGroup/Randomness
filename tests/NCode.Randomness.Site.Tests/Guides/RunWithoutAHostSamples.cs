// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Randomness;
using NCode.Randomness.Curby;
using NCode.Randomness.Drand;
using NCode.Randomness.Nist;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class RunWithoutAHostSamples
{
    [Fact]
    public async Task WithoutDependencyInjectionSample_Runs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Randomness:Mode"] = "Offline" }
            )
            .Build();
        var cancellationToken = CancellationToken.None;

        // begin-snippet: WithoutDependencyInjection
        var builder = RandomnessHost.CreateBuilder(
            configuration.GetSection(RandomnessOptions.SectionName)
        );
        builder.AddCurbyBeacon().AddNistBeacon().AddDrandBeacon();
        await using var randomness = await builder.BuildAsync(cancellationToken);

        Guid id = randomness.SecureRandom.NextGuid();
        // end-snippet

        Assert.Equal(4, id.Version);
        Assert.Equal(BeaconMode.Offline, randomness.BeaconStatus.Mode);
    }

    [Fact]
    public async Task SiteRunWithoutAHostOptionsSample_Runs()
    {
        // begin-snippet: SiteRunWithoutAHostOptions
        var builder = RandomnessHost.CreateBuilder(options =>
        {
            options.Mode = BeaconMode.Offline;
            options.ReseedInterval = TimeSpan.FromSeconds(30);
        });
        await using var randomness = await builder.BuildAsync(CancellationToken.None);
        // end-snippet

        Assert.Equal(BeaconMode.Offline, randomness.BeaconStatus.Mode);
    }

    [Fact]
    public async Task SiteRunWithoutAHostLoggingSample_Runs()
    {
        var builder = RandomnessHost.CreateBuilder(options =>
            options.Mode = BeaconMode.Offline
        );

        // begin-snippet: SiteRunWithoutAHostLogging
        builder.Services.AddLogging(logging => logging.AddConsole());
        await using var randomness = await builder.BuildAsync(CancellationToken.None);
        // end-snippet

        Assert.NotNull(randomness);
        Assert.Equal(BeaconMode.Offline, randomness.BeaconStatus.Mode);
    }

    [Fact]
    public async Task SiteRunWithoutAHostDisposedSample_Runs()
    {
        // begin-snippet: SiteRunWithoutAHostDisposed
        var builder = RandomnessHost.CreateBuilder();
        var randomness = await builder.BuildAsync(CancellationToken.None);
        var random = randomness.SecureRandom;
        Guid id = random.NextGuid();

        await randomness.DisposeAsync();

        // random.NextGuid() now throws ObjectDisposedException: disposing zeroed
        // the generator's state, so keep the host for as long as you draw values.
        // end-snippet

        Assert.Equal(4, id.Version);
        Assert.Throws<ObjectDisposedException>(() => random.NextGuid());
    }
}
