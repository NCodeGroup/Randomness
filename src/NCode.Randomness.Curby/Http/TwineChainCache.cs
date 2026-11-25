// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Fetches and verifies Twine chains once, keyed by CID and expected family. Both CURBy beacons share one instance.
/// Chains come only from pinned CIDs or CIDs in signed pulses, so the cache stays small.
/// </summary>
internal sealed class TwineChainCache(TwineApi api) : IDisposable
{
    private TwineApi Api { get; } = api;

    private ConcurrentDictionary<(Cid, string?), TwineChain> Chains { get; } = new();

    private SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>
    /// Returns the verified chain with the CID <paramref name="cid"/>, fetching it on first use.
    /// </summary>
    /// <param name="cid">The CID the chain must hash to.</param>
    /// <param name="family">
    /// The specification family the chain must follow, such as <c>rng</c>, or <see langword="null"/> for any Twine 1
    /// chain.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the fetch.</param>
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

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Chains.TryGetValue(key, out var raced))
            {
                return raced;
            }

            var document = await Api.GetAsync($"chains/{cid}", cancellationToken)
                .ConfigureAwait(false)
                ?? throw new TwineFormatException($"The Twine API does not serve chain {cid}.");
            var chain = TwineChain.Verify(document, cid, family);
            Chains[key] = chain;
            return chain;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var chain in Chains.Values)
        {
            chain.Dispose();
        }

        Gate.Dispose();
    }
}
