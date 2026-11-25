// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand;

/// <summary>
/// Settings for the drand beacon, bound from the <c>RandomnessBeacons:Drand</c> configuration section, beside the <c>Randomness</c> section of the core options, whose strict binding refuses any key it does not know.
/// </summary>
public sealed class DrandBeaconOptions
{
    /// <summary>
    /// The configuration section these options bind from by convention.
    /// </summary>
    public const string SectionName = "RandomnessBeacons:Drand";

    /// <summary>
    /// Gets or sets the base address of the drand HTTP API, an absolute <c>https</c> URL ending in <c>/</c>. The default
    /// is <see cref="DrandDefaults.BaseAddress"/>. A mirror serves the same data: every round is verified against the
    /// pinned group key, so a mirror can withhold rounds but never forge one.
    /// </summary>
    public Uri BaseAddress { get; set; } = new(DrandDefaults.BaseAddress);

    /// <summary>
    /// Gets or sets the largest response body read, in bytes. The default is 16 KiB; a round is about 200 bytes.
    /// </summary>
    public int MaxResponseBytes { get; set; } = 16 * 1024;
}
