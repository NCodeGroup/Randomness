// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// How a beacon is doing, as the collector last saw it.
/// </summary>
public enum BeaconHealth
{
    /// <summary>
    /// No fetch has finished yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The last fetch gave a verified, fresh pulse, or an unchanged one.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// The last fetch failed or its pulse was refused. Random values are still secure: the generator keeps reseeding
    /// from the operating system.
    /// </summary>
    Unavailable = 2,
}
