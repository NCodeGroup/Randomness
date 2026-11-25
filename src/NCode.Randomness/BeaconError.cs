// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// Why a beacon gave no usable pulse.
/// </summary>
public enum BeaconError
{
    /// <summary>
    /// The beacon could not be reached or did not answer in time.
    /// </summary>
    Unavailable = 0,

    /// <summary>
    /// The beacon has no pulse for the requested round.
    /// </summary>
    PulseNotFound = 1,

    /// <summary>
    /// The beacon's answer was malformed or failed verification, such as a bad signature or hash: it is never used.
    /// </summary>
    InvalidPulse = 2,

    /// <summary>
    /// The pulse is older than <see cref="RandomnessOptions.MaxPulseAge"/> or the beacon's entry in
    /// <see cref="RandomnessOptions.MaxPulseAgeByBeacon"/>, or dated further ahead than
    /// <see cref="RandomnessOptions.MaxClockSkew"/>.
    /// </summary>
    StalePulse = 3,

    /// <summary>
    /// The pulse's round is earlier than one already mixed in, as a replayed or rolled-back answer would be.
    /// </summary>
    RolledBack = 4,
}
