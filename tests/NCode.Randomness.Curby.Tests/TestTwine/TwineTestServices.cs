// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Http;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Builds the <see cref="TwineApi"/> and <see cref="TwineChainCache"/> a beacon test needs over a
/// <see cref="StubTwineServer"/>, and disposes the caches.
/// </summary>
internal sealed class TwineTestServices : IDisposable
{
    private List<TwineChainCache> Caches { get; } = [];

    /// <summary>
    /// Creates the API and the chain cache for the given options, and disposes the cache with this instance.
    /// </summary>
    public (TwineApi Api, TwineChainCache Chains) Create(
        StubTwineServer server,
        IOptions<CurbyBeaconOptions> options
    )
    {
        var api = new TwineApi(server, options);
        var chains = new TwineChainCache(api);
        Caches.Add(chains);
        return (api, chains);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var chains in Caches)
        {
            chains.Dispose();
        }
    }
}
