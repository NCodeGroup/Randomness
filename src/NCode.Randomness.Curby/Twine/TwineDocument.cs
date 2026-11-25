// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A Twine chain or pulse as the HTTP API serves it, <c>{"cid": {"/": ...}, "data": {"content": ..., "signature":
/// ...}}</c>, whose declared CID has been checked against the SHA3-512 hash of its DAG-CBOR encoding. Nothing else is
/// trusted yet: the signature is checked by the chain or pulse built from it.
/// </summary>
internal sealed class TwineDocument
{
    // The depth limit that TwineApi applies to a response.
    private static JsonDocumentOptions JsonOptions { get; } = new() { MaxDepth = 32 };

    private TwineDocument(Cid cid, IpldMap content, string signature)
    {
        Cid = cid;
        Content = content;
        Signature = signature;
    }

    public Cid Cid { get; }

    public IpldMap Content { get; }

    public string Signature { get; }

    /// <summary>
    /// Parses a document from its UTF-8 JSON, as the HTTP API serves it, and checks its CID.
    /// </summary>
    /// <param name="utf8Json">The document, as UTF-8 JSON.</param>
    /// <returns>The document, whose declared CID matches its content.</returns>
    /// <exception cref="TwineFormatException">
    /// <paramref name="utf8Json"/> is not valid JSON, or is not a Twine document with the CID it declares.
    /// </exception>
    public static TwineDocument Parse(ReadOnlySpan<byte> utf8Json)
    {
        JsonElement json;
        try
        {
            json = JsonElement.Parse(utf8Json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The Twine document is not valid JSON: {exception.Message}",
                exception
            );
        }

        return Parse(json);
    }

    public static TwineDocument Parse(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new TwineFormatException("A Twine document is a JSON object.");
        }

        var root = DagJson.Read(json).As<IpldMap>("document");
        RequireExactly(root, "document", "cid", "data");
        var declared = root.Get<IpldLink>("cid").Cid;
        var data = root.Get<IpldMap>("data");
        // The CID hashes all of data, and the signature covers only content: any other member would let a server
        // change the CID, which is the randomness, without touching what is signed.
        RequireExactly(data, "data", "content", "signature");
        var computed = Cid.ForBlock(DagCbor.Encode(data));
        if (!computed.Equals(declared))
        {
            throw new TwineFormatException(
                $"The document declares CID {declared} but hashes to {computed}."
            );
        }

        var content = data.Get<IpldMap>("content");
        var signature = data.Get<IpldString>("signature").Value;
        return new TwineDocument(declared, content, signature);
    }

    private static void RequireExactly(IpldMap map, string name, string first, string second)
    {
        foreach (var (key, _) in map.Entries)
        {
            if (
                !string.Equals(key, first, StringComparison.Ordinal)
                && !string.Equals(key, second, StringComparison.Ordinal)
            )
            {
                throw new TwineFormatException(
                    $"The {name} has the member '{key}', which Twine does not define."
                );
            }
        }
    }

    /// <summary>
    /// Returns the payload a Twine signature signs: the multihash of the SHA3-512 hash of the content's DAG-CBOR
    /// encoding.
    /// </summary>
    public byte[] ContentMultihash()
    {
        var digest = Sha3.HashData512(DagCbor.Encode(Content));
        return [Cid.Sha3_512Code, Cid.DigestLength, .. digest];
    }
}
