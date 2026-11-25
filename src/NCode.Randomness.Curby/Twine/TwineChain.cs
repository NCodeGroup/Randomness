// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A verified Twine RNG chain: its document hashes to the pinned CID, which fixes its key, it follows the Twine 1 RNG
/// 1 specification, and it is signed by its own key.
/// </summary>
internal sealed class TwineChain : IDisposable
{
    private TwineChain(Cid cid, RSA key)
    {
        Cid = cid;
        Key = key;
    }

    public Cid Cid { get; }

    public RSA Key { get; }

    public static TwineChain Verify(TwineDocument document, Cid pinned)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pinned);
        if (!document.Cid.Equals(pinned))
        {
            throw new TwineFormatException(
                $"The chain is {document.Cid}, not the pinned chain {pinned}."
            );
        }

        var specification = document.Content.Get<IpldString>("specification").Value;
        if (
            !specification.StartsWith("twine/1.", StringComparison.Ordinal)
            || !specification.Contains("/rng/1.", StringComparison.Ordinal)
        )
        {
            throw new TwineFormatException(
                $"The chain follows '{specification}', not twine/1.x/rng/1.x."
            );
        }

        var key = JwsRs256.ImportKey(document.Content.Get<IpldMap>("key"));
        try
        {
            JwsRs256.Verify(document.Signature, key, document.ContentMultihash());
            return new TwineChain(document.Cid, key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public void Dispose() => Key.Dispose();
}
