// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist;

/// <summary>
/// Settings for the NIST beacon, bound from the <c>RandomnessBeacons:Nist</c> configuration section, beside the <c>Randomness</c> section of the core options, whose strict binding refuses any key it does not know.
/// </summary>
public sealed class NistBeaconOptions
{
    /// <summary>
    /// The configuration section these options bind from by convention.
    /// </summary>
    public const string SectionName = "RandomnessBeacons:Nist";

    /// <summary>
    /// Gets or sets the base address of the NIST HTTP API, an absolute <c>https</c> URL ending in <c>/</c>, with no
    /// user info, query or fragment. The default is <see cref="NistDefaults.BaseAddress"/>. A mirror serves the same
    /// data: every pulse is verified against the pinned certificates, so a mirror can withhold pulses but never forge
    /// one.
    /// </summary>
    public Uri BaseAddress { get; set; } = new(NistDefaults.BaseAddress);

    /// <summary>
    /// Gets or sets the index of the chain to trust. The default is <see cref="NistDefaults.ChainIndex"/>.
    /// </summary>
    public long ChainIndex { get; set; } = NistDefaults.ChainIndex;

    /// <summary>
    /// Gets or sets the identifiers of the certificates to trust, each the SHA-512 hash of a certificate's DER encoding
    /// in hex. When empty, the default, <see cref="NistDefaults.CertificateIds"/> applies; a configured list replaces
    /// the defaults.
    /// </summary>
    public List<string> CertificateIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the largest response body read, in bytes. The default is 64 KiB; a pulse is about 2 KiB.
    /// </summary>
    public int MaxResponseBytes { get; set; } = 64 * 1024;
}
