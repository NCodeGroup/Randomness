// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Tests.TestTwine;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class DagCborTests
{
    #region Encode Tests

    // Expected bytes follow RFC 8949's examples in Appendix A.
    [Theory]
    [InlineData(0L, "00")]
    [InlineData(23L, "17")]
    [InlineData(24L, "1818")]
    [InlineData(1000L, "1903e8")]
    [InlineData(1000000L, "1a000f4240")]
    [InlineData(1000000000000L, "1b000000e8d4a51000")]
    [InlineData(-1L, "20")]
    [InlineData(-1000L, "3903e7")]
    [InlineData(long.MinValue, "3b7fffffffffffffff")]
    public void Encode_WithInteger_UsesTheShortestHead(long value, string expected)
    {
        Assert.Equal(expected, Convert.ToHexStringLower(DagCbor.Encode(Ipld.Integer(value))));
    }

    [Theory]
    [InlineData(1.5, "fb3ff8000000000000")]
    [InlineData(31.6, "fb403f99999999999a")]
    [InlineData(-0.0, "fb8000000000000000")]
    public void Encode_WithFloat_UsesEightBytes(double value, string expected)
    {
        Assert.Equal(expected, Convert.ToHexStringLower(DagCbor.Encode(new IpldFloat(value))));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Encode_WithNonFiniteFloat_Throws(double value)
    {
        var exception = Assert.Throws<TwineFormatException>(() =>
            DagCbor.Encode(new IpldFloat(value))
        );

        Assert.Contains("has no encoding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Encode_WithMap_SortsKeysByLengthThenBytes()
    {
        var map = Ipld.Map(
            ("bb", Ipld.Integer(1)),
            ("a", Ipld.Integer(2)),
            ("ab", Ipld.Integer(3))
        );

        var bytes = DagCbor.Encode(map);

        // {"a": 2, "ab": 3, "bb": 1}
        Assert.Equal("a3616102626162036262620" + "1", Convert.ToHexStringLower(bytes));
    }

    [Fact]
    public void Encode_WithScalars_UsesTheirSimpleValues()
    {
        var list = Ipld.List(
            Ipld.Text("é"),
            Ipld.Bytes([1]),
            new IpldBoolean(true),
            new IpldBoolean(false),
            IpldNull.Instance
        );

        Assert.Equal("8562c3a94101f5f4f6", Convert.ToHexStringLower(DagCbor.Encode(list)));
    }

    [Fact]
    public void Encode_WithLink_WritesTagFortyTwoOverTheCidWithAZeroPrefix()
    {
        var cid = Cid.Parse(CurbyFixtures.ChainId);

        var bytes = DagCbor.Encode(Ipld.Link(cid));

        Assert.Equal("d82a5845000171", Convert.ToHexStringLower(bytes[..7]));
        Assert.Equal(cid.Bytes.ToArray(), bytes[5..]);
    }

    [Fact]
    public void Encode_WithLongText_UsesALongerHead()
    {
        var bytes = DagCbor.Encode(Ipld.Text(new string('a', 300)));

        Assert.Equal("79012c", Convert.ToHexStringLower(bytes[..3]));
    }

    #endregion
}
