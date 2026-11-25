// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Entropy;

namespace NCode.Randomness.Hosting;

/// <summary>
/// Decides whether each fetched pulse is mixed in, and keeps every beacon's state. A pulse is refused when it claims
/// another beacon, is older than the last one accepted, is out of date by its own timestamp, or reuses an accepted
/// round with different content, which only an equivocating beacon or a forged mirror would serve.
/// </summary>
internal sealed class BeaconTracker : IBeaconStatus
{
    public BeaconTracker(
        IEnumerable<IRandomnessBeacon> beacons,
        BeaconEntropyPool pool,
        IOptions<RandomnessOptions> options,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(beacons);
        ArgumentNullException.ThrowIfNull(options);
        Names = BeaconNames.Validate(beacons);
        Indexes = Names
            .Select((name, index) => KeyValuePair.Create(name, index))
            .ToFrozenDictionary(StringComparer.Ordinal);
        States = [.. Names.Select(name => new BeaconState { Name = name })];
        Pool = pool;
        Options = options.Value;
        Time = time;
    }

    public BeaconMode Mode => Options.Mode;

    public IReadOnlyList<BeaconState> Beacons
    {
        get
        {
            lock (Gate)
            {
                return [.. States];
            }
        }
    }

    private ImmutableArray<string> Names { get; }

    private FrozenDictionary<string, int> Indexes { get; }

    private BeaconState[] States { get; }

    private BeaconEntropyPool Pool { get; }

    private RandomnessOptions Options { get; }

    private TimeProvider Time { get; }

    private Lock Gate { get; } = new();

    private TaskCompletionSource FirstPulse { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask WaitForPulseAsync(CancellationToken cancellationToken) =>
        new(FirstPulse.Task.WaitAsync(cancellationToken));

    public TrackResult Accept(string beacon, BeaconPulse pulse)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        var index = IndexOf(beacon);
        var now = Time.GetUtcNow();
        lock (Gate)
        {
            var state = States[index];
            var error = Judge(beacon, pulse, state.LastPulse, now);
            if (error is { } refused)
            {
                return Record(index, now, refused);
            }

            var unchanged = IsSame(pulse, state.LastPulse);
            if (!unchanged)
            {
                Pool.Mix(pulse);
            }

            States[index] = new BeaconState
            {
                Name = beacon,
                Health = BeaconHealth.Healthy,
                LastPulse = unchanged ? state.LastPulse : pulse,
                LastAttempt = now,
            };
            FirstPulse.TrySetResult();
            var transition =
                state.Health == BeaconHealth.Unavailable ? Transition.Recovered : Transition.None;
            var outcome = unchanged ? PulseOutcomes.Unchanged : PulseOutcomes.Accepted;
            return new TrackResult(outcome, null, transition);
        }
    }

    public TrackResult Fail(string beacon, BeaconError error)
    {
        var index = IndexOf(beacon);
        var now = Time.GetUtcNow();
        lock (Gate)
        {
            return Record(index, now, error);
        }
    }

    private static bool IsSame(BeaconPulse pulse, BeaconPulse? last) =>
        last is not null
        && string.Equals(pulse.Chain, last.Chain, StringComparison.Ordinal)
        && pulse.Round == last.Round
        && string.Equals(pulse.Id, last.Id, StringComparison.Ordinal);

    private BeaconError? Judge(
        string beacon,
        BeaconPulse pulse,
        BeaconPulse? last,
        DateTimeOffset now
    )
    {
        if (!string.Equals(pulse.Beacon, beacon, StringComparison.Ordinal))
        {
            return BeaconError.InvalidPulse;
        }

        if (last is not null && string.Equals(pulse.Chain, last.Chain, StringComparison.Ordinal))
        {
            if (pulse.Round < last.Round)
            {
                return BeaconError.RolledBack;
            }

            if (
                pulse.Round == last.Round
                && !string.Equals(pulse.Id, last.Id, StringComparison.Ordinal)
            )
            {
                return BeaconError.InvalidPulse;
            }
        }

        var age = now - pulse.Timestamp;
        return age > Options.MaxPulseAge || -age > Options.MaxClockSkew
            ? BeaconError.StalePulse
            : null;
    }

    private TrackResult Record(int index, DateTimeOffset now, BeaconError error)
    {
        var state = States[index];
        States[index] = new BeaconState
        {
            Name = state.Name,
            Health = BeaconHealth.Unavailable,
            LastPulse = state.LastPulse,
            LastAttempt = now,
            LastError = error,
            ConsecutiveFailures = state.ConsecutiveFailures + 1,
        };
        var transition =
            state.Health == BeaconHealth.Unavailable
                ? Transition.None
                : Transition.BecameUnavailable;
        return new TrackResult(PulseOutcomes.For(error), error, transition);
    }

    private int IndexOf(string beacon) =>
        Indexes.TryGetValue(beacon, out var index)
            ? index
            : throw new ArgumentException(
                $"No beacon is registered under the name '{beacon}'.",
                nameof(beacon)
            );
}
