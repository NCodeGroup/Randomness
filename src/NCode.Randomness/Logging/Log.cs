// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace NCode.Randomness.Logging;

/// <summary>
/// The core library's log entries.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.BeaconOffline,
        Level = LogLevel.Information,
        Message = "Randomness beacons are offline by configuration; the generator is seeded by the operating system alone"
    )]
    public static partial void BeaconOffline(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.NoBeacons,
        Level = LogLevel.Information,
        Message = "No randomness beacon is registered; the generator is seeded by the operating system alone"
    )]
    public static partial void NoBeacons(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.PulseMixed,
        Level = LogLevel.Debug,
        Message = "Mixed pulse {Round} of beacon {Beacon} ({PulseId}) into the generator"
    )]
    public static partial void PulseMixed(
        this ILogger logger,
        string beacon,
        long round,
        string pulseId
    );

    [LoggerMessage(
        EventId = EventIds.BeaconUnavailable,
        Level = LogLevel.Warning,
        Message = "Randomness beacon {Beacon} is unavailable ({Error}); random values stay secure from the operating system's entropy"
    )]
    public static partial void BeaconUnavailable(
        this ILogger logger,
        string beacon,
        BeaconError error,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.BeaconRecovered,
        Level = LogLevel.Information,
        Message = "Randomness beacon {Beacon} is available again"
    )]
    public static partial void BeaconRecovered(this ILogger logger, string beacon);

    [LoggerMessage(
        EventId = EventIds.BeaconFetchFailed,
        Level = LogLevel.Debug,
        Message = "Randomness beacon {Beacon} is still unavailable ({Error})"
    )]
    public static partial void BeaconFetchFailed(
        this ILogger logger,
        string beacon,
        BeaconError error,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.BeaconInitialized,
        Level = LogLevel.Information,
        Message = "Randomness beacon {Beacon} gave its initial pulse; it is not contacted again"
    )]
    public static partial void BeaconInitialized(this ILogger logger, string beacon);

    [LoggerMessage(
        EventId = EventIds.InitialPulseMissing,
        Level = LogLevel.Critical,
        Message = "No randomness beacon gave a pulse within {Timeout}, and Randomness:RequireInitialPulse is set"
    )]
    public static partial void InitialPulseMissing(this ILogger logger, TimeSpan timeout);
}
