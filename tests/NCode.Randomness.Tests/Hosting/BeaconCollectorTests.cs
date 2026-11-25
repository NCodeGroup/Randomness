// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;
using NCode.Randomness.Hosting;
using NCode.Randomness.Logging;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Hosting;

public sealed class BeaconCollectorTests : IDisposable
{
    private static TimeSpan Step { get; } = TimeSpan.FromSeconds(1);

    private Telemetry Telemetry { get; } = new();

    private FakeTimeProvider Time { get; } = new(Pulses.Epoch);

    private FakeLogger<BeaconCollector> Logger { get; } = new();

    private RandomnessOptions Options { get; } = new();

    // All-ones jitter scales each back-off by the largest factor, just under one.
    private ScriptedRandomSource Jitter { get; } = new([.. Enumerable.Repeat((byte)0xFF, 8 * 64)]);

    private BeaconTracker? Tracker { get; set; }

    public void Dispose() => Telemetry.Dispose();

    #region Continuous Tests

    [Fact]
    public async Task ExecuteAsync_InContinuousMode_FetchesEveryRefreshInterval()
    {
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1)).Returns(Pulse("a", 2));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Tracker?.Beacons[0].LastPulse?.Round == 1);
        var elapsed = await AdvanceUntilAsync(() => Tracker?.Beacons[0].LastPulse?.Round == 2);

        AssertBetween(Options.RefreshInterval - Step, Options.RefreshInterval + Step, elapsed);
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithAcceptedPulse_RecordsTheOutcome()
    {
        using var pulses = new MetricCollector<long>(
            Telemetry.MeterFactory,
            RandomnessDiagnostics.MeterName,
            "ncode.randomness.beacon.pulses"
        );
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => pulses.GetMeasurementSnapshot().Count == 1);

        var measurement = Assert.Single(pulses.GetMeasurementSnapshot());
        Assert.Equal("a", measurement.Tags[RandomnessMetrics.BeaconTag]);
        Assert.Equal(PulseOutcomes.Accepted, measurement.Tags[RandomnessMetrics.OutcomeTag]);
        Assert.Contains(Logger.Collector.GetSnapshot(), record =>
            record.Id.Id == EventIds.PulseMixed
        );
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBeaconFails_BacksOffExponentially()
    {
        var failure = new BeaconException(BeaconError.Unavailable, "a", "down");
        var beacon = new FakeBeacon("a").Throws(failure).Throws(failure).Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Tracker?.Beacons[0].ConsecutiveFailures == 1);
        var first = await AdvanceUntilAsync(() => beacon.Fetches == 2);
        await WaitUntilAsync(() => Tracker?.Beacons[0].ConsecutiveFailures == 2);
        var second = await AdvanceUntilAsync(() => beacon.Fetches == 3);

        AssertBetween(Options.RefreshInterval - (2 * Step), Options.RefreshInterval + Step, first);
        AssertBetween(
            (2 * Options.RefreshInterval) - (2 * Step),
            (2 * Options.RefreshInterval) + Step,
            second
        );
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBeaconFailsRepeatedly_LogsTheOutageOnceAndTheRecovery()
    {
        var failure = new BeaconException(BeaconError.InvalidPulse, "a", "bad signature");
        var beacon = new FakeBeacon("a").Throws(failure).Throws(failure).Returns(Pulse("a", 1));
        Options.MaxRetryDelay = Options.RefreshInterval;
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await AdvanceUntilAsync(() => Tracker?.Beacons[0].Health == BeaconHealth.Healthy);

        var records = Logger.Collector.GetSnapshot();
        var outage = Assert.Single(records, record => record.Id.Id == EventIds.BeaconUnavailable);
        Assert.Equal(LogLevel.Warning, outage.Level);
        Assert.Same(failure, outage.Exception);
        Assert.Single(records, record => record.Id.Id == EventIds.BeaconFetchFailed);
        Assert.Single(records, record => record.Id.Id == EventIds.BeaconRecovered);
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAdapterThrowsUnexpectedly_RecordsAnOutageAndKeepsRunning()
    {
        var beacon = new FakeBeacon("a")
            .Throws(new InvalidOperationException("bug"))
            .Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Tracker?.Beacons[0].LastError == BeaconError.Unavailable);
        await AdvanceUntilAsync(() => Tracker?.Beacons[0].Health == BeaconHealth.Healthy);

        Assert.Equal(2, beacon.Fetches);
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFetchHangs_TimesItOut()
    {
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1));
        beacon.Hold = new TaskCompletionSource();
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        var elapsed = await AdvanceUntilAsync(() =>
            Tracker?.Beacons[0].LastError == BeaconError.Unavailable
        );

        AssertBetween(Options.FetchTimeout - Step, Options.FetchTimeout + Step, elapsed);
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithTwoBeacons_OneHangingDoesNotDelayTheOther()
    {
        var slow = new FakeBeacon("slow").Returns(Pulse("slow", 1));
        slow.Hold = new TaskCompletionSource();
        var fast = new FakeBeacon("fast").Returns(Pulse("fast", 1));
        using var collector = Create(slow, fast);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Tracker?.Beacons[1].Health == BeaconHealth.Healthy);

        Assert.Equal(BeaconHealth.Pending, Tracker?.Beacons[0].Health);
        await collector.StopAsync(CancellationToken.None);
    }

    #endregion

    #region InitializeOnly Tests

    [Fact]
    public async Task ExecuteAsync_InInitializeOnlyMode_StopsAfterTheFirstPulse()
    {
        Options.Mode = BeaconMode.InitializeOnly;
        var failure = new BeaconException(BeaconError.Unavailable, "a", "down");
        var beacon = new FakeBeacon("a").Throws(failure).Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await AdvanceUntilAsync(() => collector.ExecuteTask?.IsCompleted == true);
        Time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(2, beacon.Fetches);
        Assert.Contains(Logger.Collector.GetSnapshot(), record =>
            record.Id.Id == EventIds.BeaconInitialized
        );
    }

    #endregion

    #region Offline Tests

    [Fact]
    public async Task ExecuteAsync_InOfflineMode_ContactsNoBeacon()
    {
        Options.Mode = BeaconMode.Offline;
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => collector.ExecuteTask?.IsCompleted == true);
        Time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, beacon.Fetches);
        Assert.Equal(EventIds.BeaconOffline, Logger.LatestRecord.Id.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoBeacons_RunsOnTheOperatingSystemAlone()
    {
        using var collector = Create();

        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => collector.ExecuteTask?.IsCompleted == true);

        Assert.Equal(EventIds.NoBeacons, Logger.LatestRecord.Id.Id);
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenInitialPulseIsRequiredAndArrives_Completes()
    {
        Options.RequireInitialPulse = true;
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1));
        using var collector = Create(beacon);

        await collector.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(BeaconHealth.Healthy, Tracker?.Beacons[0].Health);
        await collector.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenInitialPulseIsRequiredAndMissing_FailsTheHost()
    {
        Options.RequireInitialPulse = true;
        var beacon = new FakeBeacon("a").Throws(
            new BeaconException(BeaconError.Unavailable, "a", "down")
        );
        using var collector = Create(beacon);

        var start = collector.StartAsync(CancellationToken.None);
        await AdvanceUntilAsync(() => start.IsCompleted);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => start);
        Assert.Contains("RequireInitialPulse", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Logger.Collector.GetSnapshot(), record =>
            record.Id.Id == EventIds.InitialPulseMissing
        );
        await collector.StopAsync(CancellationToken.None);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhileWaiting_EndsTheLoops()
    {
        var beacon = new FakeBeacon("a").Returns(Pulse("a", 1));
        using var collector = Create(beacon);
        await collector.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Tracker?.Beacons[0].Health == BeaconHealth.Healthy);

        await collector.StopAsync(CancellationToken.None);

        Assert.True(collector.ExecuteTask?.IsCompletedSuccessfully);
    }

    #endregion

    private static void AssertBetween(TimeSpan minimum, TimeSpan maximum, TimeSpan actual) =>
        Assert.True(
            actual >= minimum && actual <= maximum,
            $"{actual} is not between {minimum} and {maximum}."
        );

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), "The condition was not met.");
            await Task.Delay(5);
        }
    }

    private BeaconPulse Pulse(string beacon, long round) =>
        Pulses.Create(beacon, round, Time.GetUtcNow());

    /// <summary>
    /// Advances the fake clock a step at a time, giving the collector's continuations a moment to run between steps,
    /// until <paramref name="condition"/> holds, and returns how far the clock moved.
    /// </summary>
    private async Task<TimeSpan> AdvanceUntilAsync(Func<bool> condition)
    {
        var start = Time.GetUtcNow();
        var elapsed = Stopwatch.StartNew();
        await Task.Delay(20);
        while (!condition())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), "The condition was not met.");
            Time.Advance(Step);
            await Task.Delay(5);
        }

        return Time.GetUtcNow() - start;
    }

    private BeaconCollector Create(params FakeBeacon[] beacons)
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        Tracker = new BeaconTracker(beacons, new BeaconEntropyPool(), options, Time);
        return new BeaconCollector(
            beacons,
            Tracker,
            options,
            Time,
            Jitter,
            Telemetry.Metrics,
            Logger
        );
    }
}
