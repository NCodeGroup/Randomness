// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Generation;
using NCode.Randomness.Logging;
using NCode.Randomness.Sampling;

namespace NCode.Randomness.Hosting;

/// <summary>
/// Fetches pulses from every registered beacon in the background, each beacon on its own loop, so a slow or failed
/// beacon delays no other and no caller. A failing beacon is retried with exponential back-off and jitter.
/// </summary>
internal sealed class BeaconCollector(
    IEnumerable<IRandomnessBeacon> beacons,
    BeaconTracker tracker,
    IOptions<RandomnessOptions> options,
    TimeProvider time,
    IEntropyInput jitter,
    RandomnessMetrics metrics,
    ILogger<BeaconCollector> logger
) : BackgroundService
{
    private IReadOnlyList<IRandomnessBeacon> Beacons { get; } = [.. beacons];

    private BeaconTracker Tracker { get; } = tracker;

    private RandomnessOptions Options { get; } = options.Value;

    private TimeProvider Time { get; } = time;

    private IEntropyInput Jitter { get; } = jitter;

    private RandomnessMetrics Metrics { get; } = metrics;

    private ILogger Logger { get; } = logger;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        if (!Options.RequireInitialPulse)
        {
            return;
        }

        try
        {
            await Tracker
                .WaitForPulseAsync(cancellationToken)
                .AsTask()
                .WaitAsync(Options.InitialPulseTimeout, Time, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            Logger.InitialPulseMissing(Options.InitialPulseTimeout);
            throw new InvalidOperationException(
                $"No randomness beacon gave a pulse within {Options.InitialPulseTimeout}, and Randomness:RequireInitialPulse is set.",
                exception
            );
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Options.Mode == BeaconMode.Offline)
        {
            Logger.BeaconOffline();
            return Task.CompletedTask;
        }

        if (Beacons.Count == 0)
        {
            Logger.NoBeacons();
            return Task.CompletedTask;
        }

        var loops = new Task[Beacons.Count];
        for (var i = 0; i < loops.Length; i++)
        {
            loops[i] = RunAsync(Beacons[i], stoppingToken);
        }

        return Task.WhenAll(loops);
    }

    private async Task RunAsync(IRandomnessBeacon beacon, CancellationToken stoppingToken)
    {
        var name = beacon.Name;
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var mixed = await CollectAsync(beacon, name, stoppingToken).ConfigureAwait(false);
            failures = mixed ? 0 : failures + 1;
            if (mixed && Options.Mode == BeaconMode.InitializeOnly)
            {
                Logger.BeaconInitialized(name);
                return;
            }

            try
            {
                await Task.Delay(NextDelay(failures), Time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<bool> CollectAsync(
        IRandomnessBeacon beacon,
        string name,
        CancellationToken stoppingToken
    )
    {
        using var timeout = new CancellationTokenSource(Options.FetchTimeout, Time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            timeout.Token
        );
        TrackResult result;
        Exception? failure = null;
        try
        {
            var pulse = await beacon.GetLatestPulseAsync(linked.Token).ConfigureAwait(false);
            result = Tracker.Accept(name, pulse);
            if (result.Error is null && result.Outcome == PulseOutcomes.Accepted)
            {
                Logger.PulseMixed(name, pulse.Round, pulse.Id);
            }
        }
        catch (BeaconException exception) when (!stoppingToken.IsCancellationRequested)
        {
            failure = exception;
            result = Tracker.Fail(name, exception.Error);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // A timeout or any fault of an adapter is an outage of that beacon, never a reason to stop the host.
            failure = exception;
            result = Tracker.Fail(name, BeaconError.Unavailable);
        }

        Metrics.RecordPulse(name, result.Outcome);
        Report(name, result, failure);
        return result.Error is null;
    }

    private void Report(string name, TrackResult result, Exception? failure)
    {
        switch (result.Transition)
        {
            case Transition.BecameUnavailable when result.Error is { } error:
                Logger.BeaconUnavailable(name, error, failure);
                break;
            case Transition.Recovered:
                Logger.BeaconRecovered(name);
                break;
            default:
                if (result.Error is { } repeated)
                {
                    Logger.BeaconFetchFailed(name, repeated, failure);
                }

                break;
        }
    }

    private TimeSpan NextDelay(int failures)
    {
        if (failures == 0)
        {
            return Options.RefreshInterval;
        }

        // Exponential back-off from the refresh interval, capped, then scaled by a random factor in [0.5, 1).
        var exponent = Math.Min(failures - 1, 30);
        var ceiling = Math.Min(
            Options.RefreshInterval.TotalMilliseconds * Math.Pow(2, exponent),
            Options.MaxRetryDelay.TotalMilliseconds
        );
        var factor = 0.5 + (UniformSampler.NextDouble(Jitter) / 2);
        return TimeSpan.FromMilliseconds(Math.Max(1, ceiling * factor));
    }
}
