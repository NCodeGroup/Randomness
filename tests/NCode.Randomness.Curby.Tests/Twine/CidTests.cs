// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class CidTests
{
    #region Parse Tests

    [Fact]
    public void Parse_WithChainId_ReadsTheDigest()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        Assert.Equal(68, cid.Bytes.Length);
        Assert.Equal([0x14, 0x40], cid.Multihash[..2].ToArray());
        Assert.Equal(CurbyFixtures.ChainId, cid.ToString());
    }

    [Fact]
    public void Parse_WithUpperCase_Throws()
    {
        Assert.Throws<TwineFormatException>(() =>
            Cid.Parse("b" + CurbyFixtures.ChainId[1..].ToUpperInvariant())
        );
    }

    [Fact]
    public void Parse_WithAnotherMultibase_Throws()
    {
        Assert.Throws<TwineFormatException>(() => Cid.Parse("z" + CurbyFixtures.ChainId[1..]));
    }

    [Fact]
    public void Parse_WithNonzeroTrailingBits_Throws()
    {
        var text = CurbyFixtures.ChainId[..^1] + "t";

        var exception = Assert.Throws<TwineFormatException>(() => Cid.Parse(text));

        Assert.Contains("canonical", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithAnotherHash_Throws()
    {
        // A CIDv1 DAG-CBOR SHA2-256 identifier, a common IPFS form.
        var exception = Assert.Throws<TwineFormatException>(() =>
            Cid.Parse("bafyreigh2akiscaildcqabsyg3dfr6chu3fgpregiymsck7e7aqa4s52zy")
        );

        Assert.Contains("SHA3-512", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region FromBytes Tests

    [Fact]
    public void FromBytes_WithParsedBytes_WritesTheSameText()
    {
        var parsed = Cid.Parse(CurbyFixtures.LatestPulseId);

        var rebuilt = Cid.FromBytes(parsed.Bytes);

        Assert.Equal(CurbyFixtures.LatestPulseId, rebuilt.ToString());
        Assert.Equal(parsed, rebuilt);
        Assert.Equal(parsed.GetHashCode(), rebuilt.GetHashCode());
        Assert.True(parsed.Equals((object)rebuilt));
        Assert.False(parsed.Equals(null));
    }

    [Fact]
    public void FromBytes_WithWrongLength_Throws()
    {
        Assert.Throws<TwineFormatException>(() => Cid.FromBytes(new byte[10]));
    }

    #endregion
}
