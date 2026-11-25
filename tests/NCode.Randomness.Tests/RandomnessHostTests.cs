// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests;

public class RandomnessHostTests
{
    #region CreateBuilder Tests

    [Fact]
    public async Task CreateBuilder_WithConfigure_AppliesTheOptions()
    {
        var builder = RandomnessHost.CreateBuilder(options => options.Mode = BeaconMode.Offline);

        await using var host = await builder.BuildAsync(CancellationToken.None);

        Assert.Equal(BeaconMode.Offline, host.BeaconStatus.Mode);
    }

    [Fact]
    public async Task CreateBuilder_WithMisspelledConfigurationKey_FailsTheBuild()
    {
        var configuration = Configure(("Mdoe", "Offline"));
        var builder = RandomnessHost.CreateBuilder(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Contains("Mdoe", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_Always_StopsTheServicesAndDisposesTheGenerator()
    {
        var recording = new RecordingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        var host = await builder.BuildAsync(CancellationToken.None);

        await host.DisposeAsync();

        Assert.Equal(1, recording.Stops);
        Assert.Throws<ObjectDisposedException>(() => host.SecureRandom.Fill(new byte[4]));
    }

    [Fact]
    public async Task DisposeAsync_WhenAServiceFailsToStop_StopsTheOthersAndDisposesTheGenerator()
    {
        var recording = new RecordingHostedService();
        var stopFailing = new StopFailingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        builder.Services.AddSingleton<IHostedService>(_ => stopFailing);
        var host = await builder.BuildAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await host.DisposeAsync()
        );

        Assert.Contains(exception.InnerExceptions, inner =>
            inner.Message == StopFailingHostedService.Message
        );
        Assert.Equal(1, recording.Stops);
        Assert.Throws<ObjectDisposedException>(() => host.SecureRandom.Fill(new byte[4]));
    }

    [Fact]
    public async Task DisposeAsync_Twice_StopsTheServicesOnce()
    {
        var recording = new RecordingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        var host = await builder.BuildAsync(CancellationToken.None);

        await host.DisposeAsync();
        await host.DisposeAsync();

        Assert.Equal(1, recording.Stops);
    }

    #endregion

    private static IConfiguration Configure(params (string Key, string Value)[] values)
    {
        var pairs = values.Select(value => KeyValuePair.Create(value.Key, (string?)value.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }
}
