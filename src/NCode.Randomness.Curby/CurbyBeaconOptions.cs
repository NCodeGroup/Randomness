// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby;

/// <summary>
/// Settings for the CURBy beacon, bound from the <c>RandomnessBeacons:Curby</c> configuration section, beside the <c>Randomness</c> section of the core options, whose strict binding refuses any key it does not know.
/// </summary>
public sealed class CurbyBeaconOptions
{
    /// <summary>
    /// The configuration section these options bind from by convention.
    /// </summary>
    public const string SectionName = "RandomnessBeacons:Curby";

    /// <summary>
    /// Gets or sets the base address of the Twine HTTP API, an absolute <c>https</c> URL ending in <c>/</c>. The default
    /// is <see cref="CurbyDefaults.BaseAddress"/>. A mirror serves the same data: every document is verified against
    /// <see cref="ChainId"/>, so a mirror can withhold pulses but never forge one.
    /// </summary>
    public Uri BaseAddress { get; set; } = new(CurbyDefaults.BaseAddress);

    /// <summary>
    /// Gets or sets the content identifier of the chain to trust. The default is <see cref="CurbyDefaults.RngChainId"/>.
    /// </summary>
    public string ChainId { get; set; } = CurbyDefaults.RngChainId;

    /// <summary>
    /// Gets or sets the largest response body read, in bytes. The default is 256 KiB; a pulse is about 4 KiB.
    /// </summary>
    public int MaxResponseBytes { get; set; } = 256 * 1024;
}
