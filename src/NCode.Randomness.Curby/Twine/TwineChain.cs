// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A verified Twine chain: its document hashes to the pinned CID, which fixes its key, it follows the Twine 1
/// specification of the expected family (any family when none is expected), and it is signed by its own key.
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

    /// <summary>
    /// Verifies a chain document against its pinned CID.
    /// </summary>
    /// <param name="document">The chain document.</param>
    /// <param name="pinned">The CID the chain must hash to.</param>
    /// <param name="family">
    /// The specification family the chain must follow, such as <c>rng</c>, or <see langword="null"/> for any Twine 1
    /// chain.
    /// </param>
    public static TwineChain Verify(TwineDocument document, Cid pinned, string? family)
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
            || (
                family is not null
                && !specification.Contains($"/{family}/1.", StringComparison.Ordinal)
            )
        )
        {
            var suffix = family is null ? "" : $"/{family}/1.x";
            throw new TwineFormatException(
                $"The chain follows '{specification}', not twine/1.x{suffix}."
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
