// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Globalization;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist;

/// <summary>
/// Verifies NIST Randomness Beacon 2.0 pulse documents that a caller supplies, without the network, by every check the
/// NIST beacon makes: each pulse must be on the pinned chain and name a pinned certificate, which must hash to its
/// identifier, hold an RSA key of at least 2048 bits and be valid at the pulse's timestamp; each pulse must be version
/// 2.0 with cipher suite 0, and its signature and output value must verify; and the predecessor must be the pulse one
/// index earlier and earlier in time, hash-linked by the pulse's previous value and committed to by its precommitment.
/// An instance is immutable and thread-safe.
/// </summary>
public sealed class NistPulseVerifier
{
    /// <summary>
    /// Initializes the verifier to pin chain 2 and the certificates NIST signs it with.
    /// </summary>
    public NistPulseVerifier()
        : this(NistDefaults.ChainIndex, NistDefaults.CertificateIds) { }

    /// <summary>
    /// Initializes the verifier to pin a chain and the certificates that sign it.
    /// </summary>
    /// <param name="chainIndex">The index of the chain to pin, at least 1.</param>
    /// <param name="certificateIds">
    /// The identifiers of the certificates to pin: the SHA-512 hashes of the certificates' DER encodings, each in hex.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chainIndex"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="certificateIds"/> is empty or holds a value that is not a certificate identifier.
    /// </exception>
    public NistPulseVerifier(long chainIndex, IReadOnlyCollection<string> certificateIds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chainIndex, 1);
        ArgumentNullException.ThrowIfNull(certificateIds);
        if (certificateIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one certificate identifier must be pinned.",
                nameof(certificateIds)
            );
        }

        foreach (var id in certificateIds)
        {
            if (!NistBeaconOptionsValidator.IsCertificateId(id))
            {
                throw new ArgumentException(
                    $"'{id}' is not a certificate identifier: 128 hexadecimal digits.",
                    nameof(certificateIds)
                );
            }
        }

        ChainIndex = chainIndex;
        PinnedIds = certificateIds
            .Select(static id => id.ToLowerInvariant())
            .ToFrozenSet(StringComparer.Ordinal);
    }

    private long ChainIndex { get; }

    private FrozenSet<string> PinnedIds { get; }

    /// <summary>
    /// Verifies a pulse whose predecessor names the same certificate and returns it as the NIST beacon returns it.
    /// </summary>
    /// <param name="pulse">The pulse document, as the NIST API serves it, in UTF-8 JSON.</param>
    /// <param name="predecessor">
    /// The document of the pulse one index before <paramref name="pulse"/>, as the NIST API serves it, in UTF-8 JSON.
    /// </param>
    /// <param name="certificate">
    /// The PEM certificate document, as the NIST API serves it for the identifier both pulses name.
    /// </param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">A document is malformed or any check fails.</exception>
    public BeaconPulse Verify(
        ReadOnlySpan<byte> pulse,
        ReadOnlySpan<byte> predecessor,
        ReadOnlySpan<byte> certificate
    ) => Verify(pulse, predecessor, certificate, certificate);

    /// <summary>
    /// Verifies a pulse and its predecessor, each against the certificate it names, as across a certificate rotation,
    /// and returns the pulse as the NIST beacon returns it.
    /// </summary>
    /// <param name="pulse">The pulse document, as the NIST API serves it, in UTF-8 JSON.</param>
    /// <param name="predecessor">
    /// The document of the pulse one index before <paramref name="pulse"/>, as the NIST API serves it, in UTF-8 JSON.
    /// </param>
    /// <param name="certificate">
    /// The PEM certificate document, as the NIST API serves it for the identifier <paramref name="pulse"/> names.
    /// </param>
    /// <param name="predecessorCertificate">
    /// The PEM certificate document, as the NIST API serves it for the identifier <paramref name="predecessor"/> names.
    /// </param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">A document is malformed or any check fails.</exception>
    public BeaconPulse Verify(
        ReadOnlySpan<byte> pulse,
        ReadOnlySpan<byte> predecessor,
        ReadOnlySpan<byte> certificate,
        ReadOnlySpan<byte> predecessorCertificate
    )
    {
        try
        {
            var verifiedPulse = VerifyPulse(pulse, certificate);
            var verifiedPredecessor = VerifyPulse(predecessor, predecessorCertificate);
            return ToBeaconPulse(verifiedPulse, verifiedPredecessor);
        }
        catch (Exception exception) when (exception is not BeaconException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                NistDefaults.BeaconName,
                $"The NIST documents fail verification: {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Parses a pulse, checks its chain and pinned certificate, and verifies it against the certificate.
    /// </summary>
    /// <param name="document">The pulse document in UTF-8 JSON.</param>
    /// <param name="certificate">The PEM certificate document the pulse names.</param>
    /// <returns>The verified pulse.</returns>
    private NistPulse VerifyPulse(ReadOnlySpan<byte> document, ReadOnlySpan<byte> certificate)
    {
        var pulse = NistPulse.Parse(document);
        VerifyChain(pulse, ChainIndex);
        var id = Convert.ToHexStringLower(pulse.CertificateId);
        if (!PinnedIds.Contains(id))
        {
            throw new NistFormatException($"Certificate {id} is not pinned.");
        }

        using var parsed = NistCertificate.Parse(certificate, pulse.CertificateId);
        pulse.Verify(parsed);
        return pulse;
    }

    /// <summary>
    /// Checks that the pulse is on the pinned chain.
    /// </summary>
    /// <param name="pulse">The pulse to check.</param>
    /// <param name="chainIndex">The index of the pinned chain.</param>
    /// <exception cref="NistFormatException">The pulse is on another chain.</exception>
    internal static void VerifyChain(NistPulse pulse, long chainIndex)
    {
        if (pulse.ChainIndex != chainIndex)
        {
            throw new NistFormatException(
                $"Pulse {pulse.PulseIndex} is on chain {pulse.ChainIndex}, not the pinned chain {chainIndex}."
            );
        }
    }

    /// <summary>
    /// Checks that the pulse directly follows its predecessor and returns it as the NIST beacon's pulse.
    /// </summary>
    /// <param name="pulse">The verified pulse.</param>
    /// <param name="predecessor">The verified pulse one index before <paramref name="pulse"/>.</param>
    /// <returns>The beacon pulse.</returns>
    /// <exception cref="NistFormatException">The pulse does not follow its predecessor.</exception>
    internal static BeaconPulse ToBeaconPulse(NistPulse pulse, NistPulse predecessor)
    {
        pulse.VerifyFollows(predecessor);
        var chain = pulse.ChainIndex.ToString(CultureInfo.InvariantCulture);
        var id = Convert.ToHexString(pulse.Output);
        return new BeaconPulse(
            NistDefaults.BeaconName,
            chain,
            pulse.PulseIndex,
            id,
            pulse.Timestamp,
            pulse.Output
        );
    }
}
