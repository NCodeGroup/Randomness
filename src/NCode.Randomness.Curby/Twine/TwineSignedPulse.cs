// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A verified pulse of any Twine chain: its document hashes to its CID, it names the chain, the chain's key signed it,
/// and its links and mixins are well formed.
/// </summary>
internal sealed class TwineSignedPulse
{
    /// <summary>
    /// Creates a pulse from values the caller has already verified.
    /// </summary>
    /// <param name="cid">The CID of the pulse's document.</param>
    /// <param name="chain">The CID of the chain the pulse belongs to.</param>
    /// <param name="index">The pulse's index on its chain.</param>
    /// <param name="links">The CIDs of the pulses the pulse links to, the previous pulse of its chain first.</param>
    /// <param name="mixins">The pulses the pulse mixes in, by the chain each one belongs to.</param>
    /// <param name="payload">The pulse's payload.</param>
    private TwineSignedPulse(
        Cid cid,
        Cid chain,
        long index,
        ImmutableArray<Cid> links,
        FrozenDictionary<Cid, Cid> mixins,
        IpldMap payload
    )
    {
        Cid = cid;
        Chain = chain;
        Index = index;
        Links = links;
        Mixins = mixins;
        Payload = payload;
    }

    /// <summary>
    /// Gets the CID of the pulse's document.
    /// </summary>
    public Cid Cid { get; }

    /// <summary>
    /// Gets the CID of the chain the pulse belongs to.
    /// </summary>
    public Cid Chain { get; }

    /// <summary>
    /// Gets the pulse's index on its chain, from 0.
    /// </summary>
    public long Index { get; }

    /// <summary>
    /// Gets the CIDs of the pulses the pulse links to, the previous pulse of its chain first.
    /// </summary>
    public ImmutableArray<Cid> Links { get; }

    /// <summary>
    /// Gets the pulses this pulse mixes in, by the chain each one belongs to.
    /// </summary>
    public FrozenDictionary<Cid, Cid> Mixins { get; }

    /// <summary>
    /// Gets the pulse's payload, the map the chain's specification defines.
    /// </summary>
    public IpldMap Payload { get; }

    /// <summary>
    /// Gets the CID of the pulse before this one, its first link, or <see langword="null"/> for the first pulse.
    /// </summary>
    public Cid? Previous => Links.IsEmpty ? null : Links[0];

    /// <summary>
    /// Returns the pulse of <paramref name="chain"/> that this pulse mixes in, or <see langword="null"/> when it mixes
    /// in none.
    /// </summary>
    public Cid? MixinOf(Cid chain) => Mixins.GetValueOrDefault(chain);

    /// <summary>
    /// Verifies a pulse document against <paramref name="chain"/>: it names the chain, the chain's key signed it with
    /// RS256, and its index, links, mixins and payload are well formed. It throws
    /// <see cref="TwineFormatException"/> when a check fails.
    /// </summary>
    /// <param name="document">The pulse document, already checked against its CID.</param>
    /// <param name="chain">The verified chain the pulse belongs to.</param>
    /// <returns>The verified pulse.</returns>
    public static TwineSignedPulse Verify(TwineDocument document, TwineChain chain)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chain);
        var content = document.Content;
        var chainCid = content.Get<IpldLink>("chain").Cid;
        if (!chainCid.Equals(chain.Cid))
        {
            throw new TwineFormatException(
                $"The pulse belongs to chain {chainCid}, not {chain.Cid}."
            );
        }

        var multihash = document.ContentMultihash();
        JwsRs256.Verify(document.Signature, chain.Key, multihash);

        var index = content.Get<IpldInteger>("index").Value;
        if (index < 0)
        {
            throw new TwineFormatException($"The pulse index {index} is negative.");
        }

        var links = content
            .Get<IpldList>("links")
            .Items.Select(item => item.As<IpldLink>("links").Cid)
            .ToImmutableArray();
        var mixins = new Dictionary<Cid, Cid>();
        foreach (var item in content.Get<IpldList>("mixins").Items)
        {
            var mixin = item.As<IpldMap>("mixins");
            var mixinChain = mixin.Get<IpldLink>("chain").Cid;
            var mixinValue = mixin.Get<IpldLink>("value").Cid;
            if (!mixins.TryAdd(mixinChain, mixinValue))
            {
                throw new TwineFormatException($"The pulse mixes in chain {mixinChain} twice.");
            }
        }

        var payload = content.Get<IpldMap>("payload");
        return new TwineSignedPulse(
            document.Cid,
            chainCid,
            index,
            links,
            mixins.ToFrozenDictionary(),
            payload
        );
    }

    /// <summary>
    /// Checks that this pulse directly follows <paramref name="previous"/> on the same chain.
    /// </summary>
    public void VerifyPrecededBy(TwineSignedPulse previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (
            Previous is null
            || !Previous.Equals(previous.Cid)
            || previous.Index != Index - 1
            || !previous.Chain.Equals(Chain)
        )
        {
            throw new TwineFormatException(
                $"Pulse {previous.Index} ({previous.Cid}) does not precede pulse {Index} ({Cid})."
            );
        }
    }
}
