// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Diagnostics;

/// <summary>
/// The values of the <c>ncode.randomness.outcome</c> attribute.
/// </summary>
internal static class PulseOutcomes
{
    public const string Accepted = "accepted";
    public const string Unchanged = "unchanged";
    public const string Unavailable = "unavailable";
    public const string NotFound = "not_found";
    public const string Invalid = "invalid";
    public const string Stale = "stale";
    public const string RolledBack = "rolled_back";

    public static string For(BeaconError error) =>
        error switch
        {
            BeaconError.PulseNotFound => NotFound,
            BeaconError.InvalidPulse => Invalid,
            BeaconError.StalePulse => Stale,
            BeaconError.RolledBack => RolledBack,
            _ => Unavailable,
        };
}
