// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace NCode.Randomness.Diagnostics;

/// <summary>
/// The library's instruments, listed in docs/metrics.md.
/// </summary>
internal sealed class RandomnessMetrics
{
    public const string BeaconTag = "ncode.randomness.beacon";
    public const string OutcomeTag = "ncode.randomness.outcome";
    public const string ReasonTag = "ncode.randomness.reseed.reason";

    public RandomnessMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(RandomnessDiagnostics.MeterName);
        Pulses = meter.CreateCounter<long>(
            "ncode.randomness.beacon.pulses",
            "{pulse}",
            "Beacon fetches, by beacon and outcome."
        );
        Reseeds = meter.CreateCounter<long>(
            "ncode.randomness.generator.reseeds",
            "{reseed}",
            "Reseeds of the secure generator's shards, by reason."
        );
    }

    private Counter<long> Pulses { get; }

    private Counter<long> Reseeds { get; }

    public void RecordPulse(string beacon, string outcome) =>
        Pulses.Add(1, new(BeaconTag, beacon), new(OutcomeTag, outcome));

    public void RecordReseed(string reason) =>
        Reseeds.Add(1, new KeyValuePair<string, object?>(ReasonTag, reason));
}
