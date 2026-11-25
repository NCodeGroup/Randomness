// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NCode.Randomness.Hosting;

/// <summary>
/// Validates the registered beacons' names when they are composed.
/// </summary>
internal static class BeaconNames
{
    public static ImmutableArray<string> Validate(IEnumerable<IRandomnessBeacon> beacons)
    {
        var names = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var beacon in beacons)
        {
            var name = beacon.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"The randomness beacon {beacon.GetType().FullName} has an empty name."
                );
            }

            if (!seen.Add(name))
            {
                throw new InvalidOperationException(
                    $"Two randomness beacons are registered under the name '{name}'."
                );
            }

            names.Add(name);
        }

        return names.ToImmutable();
    }
}
