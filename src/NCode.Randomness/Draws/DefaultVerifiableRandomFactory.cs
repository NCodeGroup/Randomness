// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Randomness.Hosting;

namespace NCode.Randomness.Draws;

/// <summary>
/// Resolves a beacon by its registered name and derives a draw from the pulse it verifies.
/// </summary>
internal sealed class DefaultVerifiableRandomFactory : IVerifiableRandomFactory
{
    public DefaultVerifiableRandomFactory(IEnumerable<IRandomnessBeacon> beacons)
    {
        ArgumentNullException.ThrowIfNull(beacons);
        var list = beacons.ToList();
        BeaconNames.Validate(list);
        Beacons = list.ToFrozenDictionary(beacon => beacon.Name, StringComparer.Ordinal);
    }

    private FrozenDictionary<string, IRandomnessBeacon> Beacons { get; }

    public async ValueTask<VerifiableRandom> CreateAsync(
        string beacon,
        long round,
        string context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(beacon);
        ArgumentOutOfRangeException.ThrowIfNegative(round);
        ArgumentException.ThrowIfNullOrEmpty(context);
        if (!Beacons.TryGetValue(beacon, out var source))
        {
            throw new ArgumentException(
                $"No randomness beacon is registered under the name '{beacon}'.",
                nameof(beacon)
            );
        }

        var pulse = await source.GetPulseAsync(round, cancellationToken).ConfigureAwait(false);
        if (pulse.Round != round || !string.Equals(pulse.Beacon, beacon, StringComparison.Ordinal))
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                beacon,
                $"The beacon answered round {round} with pulse {pulse}."
            );
        }

        return VerifiableRandom.Create(pulse, context);
    }
}
