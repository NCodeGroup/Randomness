// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Http;

/// <summary>
/// Fetches and parses the pinned NIST certificates once, keyed by identifier. Only a certificate whose identifier is
/// pinned is ever requested, so the cache stays as small as the pinned list. The cache has one gate per identifier.
/// </summary>
// ADR-1009: a certificate NIST serves but the configuration does not pin never reaches a pulse.
internal sealed class NistCertificateCache(NistApi api, IOptions<NistBeaconOptions> options)
    : IDisposable
{
    /// <summary>
    /// Returns the pinned identifiers in lowercase: the configured ones, or <see cref="NistDefaults.CertificateIds"/> when none are configured.
    /// </summary>
    /// <param name="configured">The configured identifiers, which the options validator has checked.</param>
    /// <returns>The identifiers to trust.</returns>
    private static FrozenSet<string> CreatePinnedIds(List<string> configured)
    {
        IEnumerable<string> ids = configured.Count == 0 ? NistDefaults.CertificateIds : configured;
        return ids.Select(static id => id.ToLowerInvariant()).ToFrozenSet(StringComparer.Ordinal);
    }

    private NistApi Api { get; } = api;

    private FrozenSet<string> PinnedIds { get; } = CreatePinnedIds(options.Value.CertificateIds);

    private ConcurrentDictionary<string, NistCertificate> Certificates { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, SemaphoreSlim> Gates { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the certificate with the identifier <paramref name="id"/>, fetching it on first use.
    /// </summary>
    /// <param name="id">The identifier the certificate must hash to and the configuration must pin.</param>
    /// <param name="cancellationToken">The token that cancels the fetch.</param>
    /// <exception cref="NistFormatException">
    /// The identifier is not pinned, NIST does not serve the certificate, or the certificate is not acceptable.
    /// </exception>
    public async ValueTask<NistCertificate> GetAsync(byte[] id, CancellationToken cancellationToken)
    {
        var hex = Convert.ToHexStringLower(id);
        if (!PinnedIds.Contains(hex))
        {
            throw new NistFormatException(
                $"Certificate {hex} is not pinned: add it to {NistBeaconOptions.SectionName}:CertificateIds once it is NIST's."
            );
        }

        if (Certificates.TryGetValue(hex, out var cached))
        {
            return cached;
        }

        var gate = Gates.GetOrAdd(hex, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Certificates.TryGetValue(hex, out var raced))
            {
                return raced;
            }

            var body = await Api.GetCertificateAsync(hex, cancellationToken).ConfigureAwait(false)
                ?? throw new NistFormatException($"NIST does not serve certificate {hex}.");
            var certificate = NistCertificate.Parse(body, id);
            Certificates[hex] = certificate;
            return certificate;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var certificate in Certificates.Values)
        {
            certificate.Dispose();
        }

        foreach (var gate in Gates.Values)
        {
            gate.Dispose();
        }
    }
}
