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

    public Cid Cid { get; }

    public Cid Chain { get; }

    public long Index { get; }

    public ImmutableArray<Cid> Links { get; }

    /// <summary>
    /// Gets the pulses this pulse mixes in, by the chain each one belongs to.
    /// </summary>
    public FrozenDictionary<Cid, Cid> Mixins { get; }

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

        JwsRs256.Verify(document.Signature, chain.Key, document.ContentMultihash());

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
            if (!mixins.TryAdd(mixinChain, mixin.Get<IpldLink>("value").Cid))
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
