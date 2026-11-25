// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// A snapshot of one beacon's state.
/// </summary>
public sealed class BeaconState
{
    /// <summary>
    /// Gets the name the beacon is registered under.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets how the beacon is doing.
    /// </summary>
    public BeaconHealth Health { get; init; }

    /// <summary>
    /// Gets the last pulse mixed in from the beacon, or <see langword="null"/> when there is none.
    /// </summary>
    public BeaconPulse? LastPulse { get; init; }

    /// <summary>
    /// Gets when the last fetch finished, or <see langword="null"/> when none has.
    /// </summary>
    public DateTimeOffset? LastAttempt { get; init; }

    /// <summary>
    /// Gets why the last fetch gave no usable pulse, or <see langword="null"/> when it gave one.
    /// </summary>
    public BeaconError? LastError { get; init; }

    /// <summary>
    /// Gets how many fetches in a row have given no usable pulse.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
}
