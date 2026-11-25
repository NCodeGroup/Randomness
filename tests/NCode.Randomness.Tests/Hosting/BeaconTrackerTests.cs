// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;
using NCode.Randomness.Hosting;
using NCode.Randomness.Tests.TestDoubles;

namespace NCode.Randomness.Tests.Hosting;

public class BeaconTrackerTests
{
    private FakeTimeProvider Time { get; } = new(Pulses.Epoch);

    private BeaconEntropyPool Pool { get; } = new();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithBeacons_StartsEachPending()
    {
        var tracker = Create("a", "b");

        Assert.Equal(["a", "b"], tracker.Beacons.Select(beacon => beacon.Name));
        Assert.All(tracker.Beacons, beacon => Assert.Equal(BeaconHealth.Pending, beacon.Health));
        Assert.Equal(BeaconMode.Continuous, tracker.Mode);
    }

    [Fact]
    public void Constructor_WithDuplicateNames_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Create("a", "a"));

        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Create(" "));
    }

    [Fact]
    public void Constructor_WithLimitForUnknownBeacon_Throws()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["z"] = TimeSpan.FromMinutes(30);

        var exception = Assert.Throws<InvalidOperationException>(() => Create(options, "a", "b"));

        Assert.Contains("'z'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "registered beacons are a, b.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Accept Tests

    [Fact]
    public void Accept_WithFreshPulse_MixesItIn()
    {
        var tracker = Create("a");
        var pulse = Pulses.Create("a", 5, Pulses.Epoch);

        var result = tracker.Accept("a", pulse);

        Assert.Equal(new TrackResult(PulseOutcomes.Accepted, null, Transition.None), result);
        Assert.Equal(1, Pool.Current.Epoch);
        var state = Assert.Single(tracker.Beacons);
        Assert.Equal(BeaconHealth.Healthy, state.Health);
        Assert.Same(pulse, state.LastPulse);
        Assert.Equal(Pulses.Epoch, state.LastAttempt);
    }

    [Fact]
    public void Accept_WithSamePulseAgain_IsUnchanged()
    {
        var tracker = Create("a");
        tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Equal(PulseOutcomes.Unchanged, result.Outcome);
        Assert.Equal(1, Pool.Current.Epoch);
    }

    [Fact]
    public void Accept_WithEarlierRound_IsRolledBack()
    {
        var tracker = Create("a");
        tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        var result = tracker.Accept("a", Pulses.Create("a", 4, Pulses.Epoch));

        Assert.Equal(BeaconError.RolledBack, result.Error);
        Assert.Equal(Transition.BecameUnavailable, result.Transition);
        Assert.Equal(1, Pool.Current.Epoch);
    }

    [Fact]
    public void Accept_WithPulseOfAnotherChain_IsInvalid()
    {
        var tracker = Create("a");
        tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        var result = tracker.Accept("a", Pulses.Create("a", 6, Pulses.Epoch, chain: "chain-2"));

        Assert.Equal(BeaconError.InvalidPulse, result.Error);
        Assert.Equal(1, Pool.Current.Epoch);
    }

    [Fact]
    public void Accept_WithSameRoundAndOtherContent_IsInvalid()
    {
        var tracker = Create("a");
        tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch, id: "forged"));

        Assert.Equal(BeaconError.InvalidPulse, result.Error);
    }

    [Fact]
    public void Accept_WithPulseOfAnotherBeacon_IsInvalid()
    {
        var tracker = Create("a", "b");

        var result = tracker.Accept("a", Pulses.Create("b", 5, Pulses.Epoch));

        Assert.Equal(BeaconError.InvalidPulse, result.Error);
        Assert.Equal(0, Pool.Current.Epoch);
    }

    [Fact]
    public void Accept_WithPulseOlderThanMaxAge_IsStale()
    {
        var tracker = Create("a");
        Time.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromTicks(1));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Equal(BeaconError.StalePulse, result.Error);
    }

    [Fact]
    public void Accept_WithPulseAtMaxAge_IsAccepted()
    {
        var tracker = Create("a");
        Time.Advance(TimeSpan.FromMinutes(10));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Null(result.Error);
    }

    [Fact]
    public void Accept_WithPulseWithinItsBeaconsLimit_IsAccepted()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(30);
        var tracker = Create(options, "a", "b");
        Time.Advance(TimeSpan.FromMinutes(20));

        var accepted = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));
        var refused = tracker.Accept("b", Pulses.Create("b", 5, Pulses.Epoch));

        Assert.Null(accepted.Error);
        Assert.Equal(BeaconError.StalePulse, refused.Error);
    }

    [Fact]
    public void Accept_WithPulseAtItsBeaconsLimit_IsAccepted()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(30);
        var tracker = Create(options, "a");
        Time.Advance(TimeSpan.FromMinutes(30));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Null(result.Error);
    }

    [Fact]
    public void Accept_WithPulseOneTickPastItsBeaconsLimit_IsStale()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(30);
        var tracker = Create(options, "a");
        Time.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromTicks(1));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Equal(BeaconError.StalePulse, result.Error);
    }

    [Fact]
    public void Accept_WithPulseOlderThanItsBeaconsLimit_IsStale()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(5);
        var tracker = Create(options, "a");
        Time.Advance(TimeSpan.FromMinutes(6));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Equal(BeaconError.StalePulse, result.Error);
    }

    [Fact]
    public void Accept_AfterTheLimitsChange_KeepsTheLimitsItStartedWith()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(30);
        var tracker = Create(options, "a");
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromMinutes(1);
        Time.Advance(TimeSpan.FromMinutes(20));

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Null(result.Error);
    }

    [Fact]
    public void Accept_WithPulseFromTheFuture_IsStale()
    {
        var tracker = Create("a");

        var result = tracker.Accept(
            "a",
            Pulses.Create("a", 5, Pulses.Epoch.AddMinutes(2).AddTicks(1))
        );

        Assert.Equal(BeaconError.StalePulse, result.Error);
    }

    [Fact]
    public void Accept_AfterFailure_Recovers()
    {
        var tracker = Create("a");
        tracker.Fail("a", BeaconError.Unavailable);

        var result = tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        Assert.Equal(Transition.Recovered, result.Transition);
        Assert.Equal(0, Assert.Single(tracker.Beacons).ConsecutiveFailures);
    }

    [Fact]
    public void Accept_WithUnknownBeacon_Throws()
    {
        var tracker = Create("a");

        var exception = Assert.Throws<ArgumentException>(() =>
            tracker.Accept("b", Pulses.Create("b", 1, Pulses.Epoch))
        );

        Assert.Equal("beacon", exception.ParamName);
    }

    #endregion

    #region Fail Tests

    [Fact]
    public void Fail_Repeatedly_ReportsTheOutageOnce()
    {
        var tracker = Create("a");

        var first = tracker.Fail("a", BeaconError.Unavailable);
        var second = tracker.Fail("a", BeaconError.InvalidPulse);

        Assert.Equal(Transition.BecameUnavailable, first.Transition);
        Assert.Equal(Transition.None, second.Transition);
        Assert.Equal(PulseOutcomes.Invalid, second.Outcome);
        var state = Assert.Single(tracker.Beacons);
        Assert.Equal(2, state.ConsecutiveFailures);
        Assert.Equal(BeaconError.InvalidPulse, state.LastError);
        Assert.Equal(BeaconHealth.Unavailable, state.Health);
    }

    [Fact]
    public void Fail_AfterPulse_KeepsTheLastPulse()
    {
        var tracker = Create("a");
        var pulse = Pulses.Create("a", 5, Pulses.Epoch);
        tracker.Accept("a", pulse);

        tracker.Fail("a", BeaconError.PulseNotFound);

        Assert.Same(pulse, Assert.Single(tracker.Beacons).LastPulse);
    }

    #endregion

    #region WaitForPulseAsync Tests

    [Fact]
    public async Task WaitForPulseAsync_BeforeAnyPulse_WaitsUntilOneIsMixed()
    {
        var tracker = Create("a");
        var wait = tracker.WaitForPulseAsync(CancellationToken.None).AsTask();
        Assert.False(wait.IsCompleted);

        tracker.Accept("a", Pulses.Create("a", 5, Pulses.Epoch));

        await wait.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WaitForPulseAsync_WhenCancelled_Throws()
    {
        var tracker = Create("a");
        using var cancellation = new CancellationTokenSource();
        var wait = tracker.WaitForPulseAsync(cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => wait);
    }

    #endregion

    private BeaconTracker Create(params string[] names) => Create(new RandomnessOptions(), names);

    private BeaconTracker Create(RandomnessOptions options, params string[] names) =>
        new(names.Select(name => new FakeBeacon(name)), Pool, Options.Create(options), Time);
}
