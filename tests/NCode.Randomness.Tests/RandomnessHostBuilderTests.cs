// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Randomness.Generation;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests;

public class RandomnessHostBuilderTests
{
    #region BuildAsync Tests

    [Fact]
    public async Task BuildAsync_WithNoBeacon_ServesSecureBytes()
    {
        var builder = RandomnessHost.CreateBuilder();

        await using var host = await builder.BuildAsync(CancellationToken.None);

        var bytes = host.SecureRandom.GetBytes(32);

        Assert.IsType<DefaultSecureRandom>(host.SecureRandom);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public async Task BuildAsync_WithNoBeacon_TracksNoBeacon()
    {
        var builder = RandomnessHost.CreateBuilder();

        await using var host = await builder.BuildAsync(CancellationToken.None);

        Assert.Empty(host.BeaconStatus.Beacons);
    }

    [Fact]
    public async Task BuildAsync_WithABeacon_MixesItsPulseInTheBackground()
    {
        var builder = RandomnessHost.CreateBuilder();
        builder.AddBeacon<NamedBeacon>();

        var timeout = TimeSpan.FromSeconds(10);

        await using var host = await builder.BuildAsync(CancellationToken.None);
        await host
            .BeaconStatus.WaitForPulseAsync(CancellationToken.None)
            .AsTask()
            .WaitAsync(timeout);

        var state = Assert.Single(host.BeaconStatus.Beacons);
        Assert.Equal(NamedBeacon.BeaconName, state.Name);
        Assert.Equal(BeaconHealth.Healthy, state.Health);
    }

    [Fact]
    public async Task BuildAsync_WithInvalidOptions_ThrowsAndNamesTheSetting()
    {
        var builder = RandomnessHost.CreateBuilder(options =>
            options.RefreshInterval = TimeSpan.Zero
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Contains("Randomness:RefreshInterval", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_WhenARequiredPulseNeverArrives_Throws()
    {
        var builder = RandomnessHost.CreateBuilder(options =>
        {
            options.RequireInitialPulse = true;
            options.InitialPulseTimeout = TimeSpan.FromMilliseconds(10);
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Contains("RequireInitialPulse", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_WhenAServiceCannotBeConstructed_ThrowsAndNamesItsDependency()
    {
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<UnresolvableService>();

        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        var inner = Assert.Single(exception.InnerExceptions);
        Assert.IsType<InvalidOperationException>(inner);
        Assert.Contains(nameof(IUnregisteredDependency), inner.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_WhenAServiceFailsToStart_RethrowsItsFailure()
    {
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Equal(FailingHostedService.Message, exception.Message);
    }

    [Fact]
    public async Task BuildAsync_WhenAServiceFailsToStart_StopsTheServicesItStarted()
    {
        var recording = new RecordingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Equal(1, recording.Starts);
        Assert.Equal(1, recording.Stops);
    }

    [Fact]
    public async Task BuildAsync_WhenAServiceFailsToStartAndAnotherFailsToStop_ThrowsBothFailures()
    {
        var stopFailing = new StopFailingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(_ => stopFailing);
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Contains(exception.InnerExceptions, inner =>
            inner is InvalidOperationException && inner.Message == FailingHostedService.Message
        );
        Assert.Contains(exception.InnerExceptions, inner =>
            inner is InvalidOperationException && inner.Message == StopFailingHostedService.Message
        );
    }

    [Fact]
    public async Task BuildAsync_WhenAServiceFailsToStartAndAnotherFailsToStop_DisposesTheProvider()
    {
        var stopFailing = new StopFailingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(_ => stopFailing);
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        await Assert.ThrowsAsync<AggregateException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.True(stopFailing.Disposed);
    }

    [Fact]
    public async Task BuildAsync_Twice_Throws()
    {
        var builder = RandomnessHost.CreateBuilder();
        await using var host = await builder.BuildAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Contains("already built", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private interface IUnregisteredDependency;

    private sealed class UnresolvableService
    {
        public UnresolvableService(IUnregisteredDependency dependency) => Dependency = dependency;

        public IUnregisteredDependency Dependency { get; }
    }

    private sealed class NamedBeacon : IRandomnessBeacon
    {
        public const string BeaconName = "named";

        public string Name => BeaconName;

        public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken)
        {
            var pulse = Pulses.Create(Name, 1, DateTimeOffset.UtcNow);
            return ValueTask.FromResult(pulse);
        }

        public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken)
        {
            var pulse = Pulses.Create(Name, round, DateTimeOffset.UtcNow);
            return ValueTask.FromResult(pulse);
        }
    }

    private sealed class FailingHostedService : IHostedService
    {
        public const string Message = "The service failed to start.";

        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(Message));

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
