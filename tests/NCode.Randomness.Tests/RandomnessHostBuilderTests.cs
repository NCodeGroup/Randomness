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

        Assert.IsType<DefaultSecureRandom>(host.SecureRandom);
        Assert.Equal(32, host.SecureRandom.GetBytes(32).Length);
        Assert.Empty(host.BeaconStatus.Beacons);
    }

    [Fact]
    public async Task BuildAsync_WithABeacon_MixesItsPulseInTheBackground()
    {
        var builder = RandomnessHost.CreateBuilder();
        builder.AddBeacon<NamedBeacon>();

        await using var host = await builder.BuildAsync(CancellationToken.None);
        await host
            .BeaconStatus.WaitForPulseAsync(CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));

        var state = Assert.Single(host.BeaconStatus.Beacons);
        Assert.Equal("named", state.Name);
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
    public async Task BuildAsync_WhenAServiceFailsToStart_StopsTheServicesItStarted()
    {
        var recording = new RecordingHostedService();
        var builder = RandomnessHost.CreateBuilder();
        builder.Services.AddSingleton<IHostedService>(recording);
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );

        Assert.Equal(FailingHostedService.Message, exception.Message);
        Assert.Equal(1, recording.Starts);
        Assert.Equal(1, recording.Stops);
    }

    [Fact]
    public async Task BuildAsync_Twice_Throws()
    {
        var builder = RandomnessHost.CreateBuilder();
        await using var host = await builder.BuildAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await builder.BuildAsync(CancellationToken.None)
        );
    }

    #endregion

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

    private sealed class FailingHostedService : IHostedService
    {
        public const string Message = "The service failed to start.";

        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(Message));

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
