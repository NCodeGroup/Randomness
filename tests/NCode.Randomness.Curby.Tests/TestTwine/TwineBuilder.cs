// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Builds a Twine RNG chain and its pulses signed with a key of its own, so a test can break exactly one rule. A
/// pulse commits to the next salt as CURBy does: it holds H(x) for a secret x, and the next pulse's salt is x XOR the
/// committing pulse's digest. Its encoder is the library's, so the saved CURBy fixtures, not these documents, prove
/// the encoding.
/// </summary>
internal sealed class TwineBuilder : IDisposable
{
    /// <summary>
    /// Formats <paramref name="time"/> as a pulse's <c>timestamp</c>: UTC with milliseconds, as CURBy writes it.
    /// </summary>
    internal static string FormatTimestamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public TwineBuilder(
        int keySize = 2048,
        string specification = "twine/1.0.x/rng/1.0.x",
        string header = """{"alg":"RS256"}"""
    )
    {
        Key = RSA.Create(keySize);
        Header = header;
        var parameters = Key.ExportParameters(false);
        var jwk = Ipld.Map(
            ("alg", Ipld.Text("RS256")),
            ("e", Ipld.Text(Base64Url.EncodeToString(parameters.Exponent))),
            ("kty", Ipld.Text("RSA")),
            ("n", Ipld.Text(Base64Url.EncodeToString(parameters.Modulus)))
        );
        ChainContent = Ipld.Map(
            ("key", jwk),
            ("links_radix", Ipld.Integer(32)),
            ("meta", Ipld.Map(("name", Ipld.Text("test")))),
            ("mixins", Ipld.List()),
            ("source", Ipld.Text("test")),
            ("specification", Ipld.Text(specification))
        );
        (ChainJson, ChainCid) = Seal(ChainContent);
    }

    public string ChainJson { get; }

    public Cid ChainCid { get; }

    public RSA Key { get; }

    private string Header { get; }

    private IpldMap ChainContent { get; }

    /// <summary>
    /// Builds <paramref name="count"/> consecutive pulses from round 0, each one minute after the last.
    /// </summary>
    public IReadOnlyList<(string Json, Cid Cid)> Pulses(int count, DateTimeOffset start)
    {
        var pulses = new List<(string Json, Cid Cid)>();
        Cid? previous = null;
        byte[]? secret = null;
        for (var index = 0; index < count; index++)
        {
            var salt = new byte[64];
            if (secret is not null && previous is not null)
            {
                for (var i = 0; i < salt.Length; i++)
                {
                    salt[i] = (byte)(secret[i] ^ previous.Digest[i]);
                }
            }

            secret = RandomNumberGenerator.GetBytes(64);
            var content = PulseContent(index, previous, salt, secret, start.AddMinutes(index));
            var sealedPulse = Seal(content);
            pulses.Add(sealedPulse);
            previous = sealedPulse.Cid;
        }

        return pulses;
    }

    public IpldMap PulseContent(
        long index,
        Cid? previous,
        byte[] salt,
        byte[] nextSecret,
        DateTimeOffset timestamp
    )
    {
        var nextDigest = Sha3.HashData512(nextSecret);
        var pre = Ipld.Bytes([0x14, 0x40, .. nextDigest]);
        var saltBytes = Ipld.Bytes(salt);
        var time = FormatTimestamp(timestamp);
        var timestampText = Ipld.Text(time);
        var payload = Ipld.Map(("pre", pre), ("salt", saltBytes), ("timestamp", timestampText));
        return Content(index, previous, payload);
    }

    /// <summary>
    /// Builds the content of a pulse with the given payload, for a chain of any family.
    /// </summary>
    public IpldMap Content(
        long index,
        Cid? previous,
        IpldMap payload,
        params (Cid Chain, Cid Value)[] mixins
    )
    {
        IpldNode[] links = previous is null ? [] : [Ipld.Link(previous)];
        var chainLink = Ipld.Link(ChainCid);
        var indexNode = Ipld.Integer(index);
        var linksNode = Ipld.List(links);
        IpldNode[] mixinMaps =
        [
            .. mixins.Select(mixin =>
            {
                var mixinChain = Ipld.Link(mixin.Chain);
                var mixinValue = Ipld.Link(mixin.Value);
                return Ipld.Map(("chain", mixinChain), ("value", mixinValue));
            }),
        ];
        var mixinsNode = Ipld.List(mixinMaps);
        var sourceNode = Ipld.Text("test");
        return Ipld.Map(
            ("chain", chainLink),
            ("index", indexNode),
            ("links", linksNode),
            ("mixins", mixinsNode),
            ("payload", payload),
            ("source", sourceNode)
        );
    }

    /// <summary>
    /// Signs <paramref name="content"/> with the chain's key and wraps it in a document with its true CID.
    /// </summary>
    public (string Json, Cid Cid) Seal(IpldMap content, string? signature = null)
    {
        var data = Ipld.Map(
            ("content", content),
            ("signature", Ipld.Text(signature ?? Sign(content)))
        );
        var cid = Cid.ForBlock(DagCbor.Encode(data));
        var json = DagJsonWriter.Write(Ipld.Map(("cid", Ipld.Link(cid)), ("data", data)));
        return (json, cid);
    }

    public string Sign(IpldMap content)
    {
        byte[] payload = [0x14, 0x40, .. Sha3.HashData512(DagCbor.Encode(content))];
        var signingInput =
            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(Header))
            + "."
            + Base64Url.EncodeToString(payload);
        var signature = Key.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return signingInput + "." + Base64Url.EncodeToString(signature);
    }

    public void Dispose() => Key.Dispose();
}
