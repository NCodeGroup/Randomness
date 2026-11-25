// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Http;

/// <summary>
/// Fetches and parses the pinned NIST certificates, keyed by identifier. Only a certificate whose identifier is
/// pinned is ever requested, so the cache stays as small as the pinned list. Each certificate is fetched once for every
/// caller, under its own timeout, and a failed fetch is remembered for
/// <see cref="NistBeaconOptions.CertificateRetryDelay"/>.
/// </summary>
// ADR-1009: a certificate NIST serves but the configuration does not pin never reaches a pulse.
// The shared-fetch policy, one Lazy fetch per key under FetchTimeout with a failure remembered for the retry delay,
// is copied on purpose from TwineChainCache in NCode.Randomness.Curby: a fix to one copy belongs in the other.
internal sealed class NistCertificateCache(
    NistApi api,
    IOptions<NistBeaconOptions> options,
    TimeProvider time
) : IDisposable
{
    /// <summary>
    /// Gets how long one certificate fetch may take, whoever waits for it.
    /// </summary>
    internal static TimeSpan FetchTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns the pinned identifiers in lowercase: the configured ones, or
    /// <see cref="NistDefaults.CertificateIds"/> when none are configured.
    /// </summary>
    /// <param name="configured">The configured identifiers, which the options validator has checked.</param>
    /// <returns>The identifiers to trust.</returns>
    private static FrozenSet<string> CreatePinnedIds(List<string> configured)
    {
        IEnumerable<string> ids = configured.Count == 0 ? NistDefaults.CertificateIds : configured;
        return ids.Select(static id => id.ToLowerInvariant()).ToFrozenSet(StringComparer.Ordinal);
    }

    private NistApi Api { get; } = api;

    private TimeProvider Time { get; } = time;

    private TimeSpan RetryDelay { get; } = options.Value.CertificateRetryDelay;

    private FrozenSet<string> PinnedIds { get; } = CreatePinnedIds(options.Value.CertificateIds);

    private ConcurrentDictionary<string, NistCertificate> Certificates { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, Lazy<Task<NistCertificate>>> Fetches { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, DateTimeOffset> Failures { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the certificate with the identifier <paramref name="id"/>, fetching it on first use.
    /// </summary>
    /// <param name="id">The identifier the certificate must hash to and the configuration must pin.</param>
    /// <param name="cancellationToken">
    /// The token that stops this caller waiting; it never cancels the fetch other callers share.
    /// </param>
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

        var fetch = GetFetch(hex, id);
        if (HasExpired(hex, fetch))
        {
            var stale = KeyValuePair.Create(hex, fetch);
            Fetches.TryRemove(stale);
            fetch = GetFetch(hex, id);
        }

        var task = fetch.Value;
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the fetch every caller of a certificate shares, starting it when there is none. The
    /// <see cref="Lazy{T}"/> makes the fetch start once even when two callers race to add it.
    /// </summary>
    /// <param name="hex">The pinned identifier in lowercase hex.</param>
    /// <param name="id">The identifier the certificate must hash to.</param>
    /// <returns>The shared fetch of the certificate.</returns>
    private Lazy<Task<NistCertificate>> GetFetch(string hex, byte[] id) =>
        Fetches.GetOrAdd(hex, key => new(() => FetchAsync(key, id).AsTask()));

    /// <summary>
    /// Gets whether a fetch failed and its failure has been remembered for the whole retry delay, so the next caller
    /// replaces it with a new fetch.
    /// </summary>
    /// <param name="hex">The pinned identifier in lowercase hex.</param>
    /// <param name="fetch">The shared fetch of the certificate.</param>
    /// <returns><see langword="true"/> when the fetch failed and the retry delay has passed.</returns>
    private bool HasExpired(string hex, Lazy<Task<NistCertificate>> fetch)
    {
        if (!fetch.IsValueCreated)
        {
            return false;
        }

        var task = fetch.Value;
        if (!task.IsCompleted || task.IsCompletedSuccessfully)
        {
            return false;
        }

        if (!Failures.TryGetValue(hex, out var failedAt))
        {
            return false;
        }

        var elapsed = Time.GetUtcNow() - failedAt;
        return elapsed >= RetryDelay;
    }

    /// <summary>
    /// Fetches and parses one certificate under <see cref="FetchTimeout"/>, remembering when it fails.
    /// </summary>
    /// <param name="hex">The pinned identifier in lowercase hex.</param>
    /// <param name="id">The identifier the certificate must hash to.</param>
    /// <returns>The parsed certificate.</returns>
    /// <exception cref="NistFormatException">NIST does not serve the certificate or it is not acceptable.</exception>
    // Takes no CancellationToken: every caller shares the fetch, so FetchTimeout bounds it rather than one caller's
    // token.
    private async ValueTask<NistCertificate> FetchAsync(string hex, byte[] id)
    {
        try
        {
            using var timeout = new CancellationTokenSource(FetchTimeout, Time);
            var body = await Api.GetCertificateAsync(hex, timeout.Token).ConfigureAwait(false)
                ?? throw new NistFormatException($"NIST does not serve certificate {hex}.");
            var certificate = NistCertificate.Parse(body, id);
            Certificates[hex] = certificate;
            Failures.TryRemove(hex, out _);
            return certificate;
        }
        catch (Exception)
        {
            Failures[hex] = Time.GetUtcNow();
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var certificate in Certificates.Values)
        {
            certificate.Dispose();
        }
    }
}
