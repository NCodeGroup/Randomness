// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Logging;

/// <summary>
/// The event IDs of the core library's log entries: the 1000 band. Append only; never renumber or reuse a value.
/// </summary>
internal static class EventIds
{
    private const int Base = 1000;

    public const int BeaconOffline = Base + 1;
    public const int NoBeacons = Base + 2;
    public const int PulseMixed = Base + 3;
    public const int BeaconUnavailable = Base + 4;
    public const int BeaconRecovered = Base + 5;
    public const int BeaconFetchFailed = Base + 6;
    public const int BeaconInitialized = Base + 7;
    public const int InitialPulseMissing = Base + 8;
}
