// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Fetches and verifies Twine chains, keyed by CID and expected family. Both CURBy beacons share one instance. Chains
/// come only from pinned CIDs or CIDs in signed pulses, so the cache stays small. Each chain is fetched once for every
/// caller, under its own timeout, and a failed fetch is remembered for
/// <see cref="CurbyBeaconOptions.ChainRetryDelay"/>.
/// </summary>
// ADR-1002: one beacon's slow chain fetch never delays another's.
// The shared-fetch policy, one Lazy fetch per key under FetchTimeout with a failure remembered for the retry delay,
// is copied on purpose into NistCertificateCache in NCode.Randomness.Nist: a fix to one copy belongs in the other.
internal sealed class TwineChainCache(
    TwineApi api,
    IOptions<CurbyBeaconOptions> options,
    TimeProvider time
) : IDisposable
{
    /// <summary>
    /// Gets how long one chain fetch may take, whoever waits for it.
    /// </summary>
    internal static TimeSpan FetchTimeout { get; } = TimeSpan.FromSeconds(30);

    private TwineApi Api { get; } = api;

    private TimeProvider Time { get; } = time;

    private TimeSpan RetryDelay { get; } = options.Value.ChainRetryDelay;

    private ConcurrentDictionary<(Cid, string?), TwineChain> Chains { get; } = new();

    private ConcurrentDictionary<(Cid, string?), Lazy<Task<TwineChain>>> Fetches { get; } = new();

    private ConcurrentDictionary<(Cid, string?), DateTimeOffset> Failures { get; } = new();

    /// <summary>
    /// Returns the verified chain with the CID <paramref name="cid"/>, fetching it on first use.
    /// </summary>
    /// <param name="cid">The CID the chain must hash to.</param>
    /// <param name="family">
    /// The specification family the chain must follow, such as <c>rng</c>, or <see langword="null"/> for any Twine 1
    /// chain.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that stops this caller waiting; it never cancels the fetch other callers share.
    /// </param>
    public async ValueTask<TwineChain> GetAsync(
        Cid cid,
        string? family,
        CancellationToken cancellationToken
    )
    {
        var key = (cid, family);
        if (Chains.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var fetch = GetFetch(key);
        if (HasExpired(key, fetch))
        {
            var stale = KeyValuePair.Create(key, fetch);
            Fetches.TryRemove(stale);
            fetch = GetFetch(key);
        }

        var task = fetch.Value;
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the fetch every caller of a chain shares, starting it when there is none. The
    /// <see cref="Lazy{T}"/> makes the fetch start once even when two callers race to add it.
    /// </summary>
    /// <param name="key">The CID and expected family of the chain.</param>
    /// <returns>The shared fetch of the chain.</returns>
    private Lazy<Task<TwineChain>> GetFetch((Cid, string?) key) =>
        Fetches.GetOrAdd(key, chainKey => new(() => FetchAsync(chainKey).AsTask()));

    /// <summary>
    /// Gets whether a fetch failed and its failure has been remembered for the whole retry delay, so the next caller
    /// replaces it with a new fetch.
    /// </summary>
    /// <param name="key">The CID and expected family of the chain.</param>
    /// <param name="fetch">The shared fetch of the chain.</param>
    /// <returns><see langword="true"/> when the fetch failed and the retry delay has passed.</returns>
    private bool HasExpired((Cid, string?) key, Lazy<Task<TwineChain>> fetch)
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

        if (!Failures.TryGetValue(key, out var failedAt))
        {
            return false;
        }

        var elapsed = Time.GetUtcNow() - failedAt;
        return elapsed >= RetryDelay;
    }

    /// <summary>
    /// Fetches and verifies one chain under <see cref="FetchTimeout"/>, remembering when it fails.
    /// </summary>
    /// <param name="key">The CID and expected family of the chain.</param>
    /// <returns>The verified chain.</returns>
    /// <exception cref="TwineFormatException">The Twine API does not serve the chain or it does not verify.</exception>
    // Takes no CancellationToken: every caller shares the fetch, so FetchTimeout bounds it rather than one caller's
    // token.
    private async ValueTask<TwineChain> FetchAsync((Cid, string?) key)
    {
        try
        {
            var (cid, family) = key;
            using var timeout = new CancellationTokenSource(FetchTimeout, Time);
            var document = await Api.GetAsync($"chains/{cid}", timeout.Token).ConfigureAwait(false)
                ?? throw new TwineFormatException($"The Twine API does not serve chain {cid}.");
            var chain = TwineChain.Verify(document, cid, family);
            Chains[key] = chain;
            Failures.TryRemove(key, out _);
            return chain;
        }
        catch (Exception)
        {
            Failures[key] = Time.GetUtcNow();
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var chain in Chains.Values)
        {
            chain.Dispose();
        }
    }
}
