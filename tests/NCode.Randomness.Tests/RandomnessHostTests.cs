// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests;

public class RandomnessHostTests
{
    private static IConfiguration Configure(params (string Key, string Value)[] values)
    {
        var pairs = values.Select(value => KeyValuePair.Create(value.Key, (string?)value.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }

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

    [Fact]
    public async Task CreateBuilder_WithConfiguration_AppliesTheOptions()
    {
        var configuration = Configure(("Mode", "Offline"));
        var builder = RandomnessHost.CreateBuilder(configuration);

        await using var host = await builder.BuildAsync(CancellationToken.None);

        Assert.Equal(BeaconMode.Offline, host.BeaconStatus.Mode);
    }

    [Fact]
    public void CreateBuilder_WithNullConfigure_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            RandomnessHost.CreateBuilder((Action<RandomnessOptions>)null!)
        );

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void CreateBuilder_WithNullConfiguration_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            RandomnessHost.CreateBuilder((IConfiguration)null!)
        );

        Assert.Equal("configuration", exception.ParamName);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_Always_StopsTheServices()
    {
        var recording = new RecordingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        var host = await builder.BuildAsync(CancellationToken.None);

        await host.DisposeAsync();

        Assert.Equal(1, recording.Stops);
    }

    [Fact]
    public async Task DisposeAsync_Always_DisposesTheGenerator()
    {
        var builder = RandomnessHost.CreateBuilder();
        var host = await builder.BuildAsync(CancellationToken.None);

        await host.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => host.SecureRandom.Fill(new byte[4]));
    }

    [Fact]
    public async Task DisposeAsync_WhenAServiceFailsToStop_ThrowsItsFailure()
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
    }

    [Fact]
    public async Task DisposeAsync_WhenAServiceFailsToStop_StopsTheOthers()
    {
        var recording = new RecordingHostedService();
        var stopFailing = new StopFailingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        builder.Services.AddSingleton<IHostedService>(_ => stopFailing);
        var host = await builder.BuildAsync(CancellationToken.None);

        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());

        Assert.Equal(1, recording.Stops);
    }

    [Fact]
    public async Task DisposeAsync_WhenAServiceFailsToStop_DisposesTheGenerator()
    {
        var recording = new RecordingHostedService();
        var stopFailing = new StopFailingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        builder.Services.AddSingleton<IHostedService>(_ => stopFailing);
        var host = await builder.BuildAsync(CancellationToken.None);

        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());

        Assert.Throws<ObjectDisposedException>(() => host.SecureRandom.Fill(new byte[4]));
    }

    [Fact]
    public async Task DisposeAsync_WithSeveralServices_StopsThemInReverseOrder()
    {
        var stopped = new List<string>();
        var first = new StopOrderHostedService("first", stopped);
        var second = new StopOrderHostedService("second", stopped);
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(first);
        builder.Services.AddSingleton<IHostedService>(second);
        var host = await builder.BuildAsync(CancellationToken.None);

        await host.DisposeAsync();

        string[] expected = [second.Name, first.Name];
        Assert.Equal(expected, stopped);
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

    private sealed class StopOrderHostedService(string name, List<string> stopped) : IHostedService
    {
        public string Name { get; } = name;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            stopped.Add(Name);
            return Task.CompletedTask;
        }
    }
}
