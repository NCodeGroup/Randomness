// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class DagJsonTests
{
    #region Read Tests

    [Fact]
    public void Read_WithLink_ReadsTheCid()
    {
        var node = Read($$"""{"/":"{{CurbyFixtures.ChainId}}"}""");

        Assert.Equal(CurbyFixtures.ChainId, Assert.IsType<IpldLink>(node).Cid.ToString());
    }

    [Fact]
    public void Read_WithBytes_DecodesUnpaddedBase64()
    {
        var node = Read("""{"/":{"bytes":"AQID"}}""");

        Assert.Equal([1, 2, 3], Assert.IsType<IpldBytes>(node).Value.ToArray());
    }

    [Theory]
    [InlineData("""{"/":{"bytes":"AQ=="}}""")]
    [InlineData("""{"/":{"bytes":"AQ I"}}""")]
    [InlineData("""{"/":{"bytes":"AR"}}""")]
    [InlineData("""{"/":{"bytes":"!!"}}""")]
    public void Read_WithNonCanonicalBytes_Throws(string json)
    {
        Assert.Throws<TwineFormatException>(() => Read(json));
    }

    [Theory]
    [InlineData("""{"/":1}""")]
    [InlineData("""{"/":"x","a":1}""")]
    [InlineData("""{"/":{"bytes":"AQ","x":1}}""")]
    [InlineData("""{"/":{"bytes":1}}""")]
    public void Read_WithOtherUseOfTheReservedKey_Throws(string json)
    {
        Assert.Throws<TwineFormatException>(() => Read(json));
    }

    [Fact]
    public void Read_WithDuplicateKey_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() => Read("""{"a":1,"a":2}"""));

        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WithIntegerBeyondInt64_Throws()
    {
        Assert.Throws<TwineFormatException>(() => Read("18446744073709551615"));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("1.0")]
    [InlineData("-0.0")]
    [InlineData("1.0842021724855042e-20")]
    public void Read_WithFractionOrExponent_ReadsAFloat(string json)
    {
        var node = Assert.IsType<IpldFloat>(Read(json));

        Assert.Equal(
            BitConverter.DoubleToInt64Bits(double.Parse(json, CultureInfo.InvariantCulture)),
            BitConverter.DoubleToInt64Bits(node.Value)
        );
    }

    [Fact]
    public void Read_WithFloatBeyondDouble_Throws()
    {
        var exception = Assert.Throws<TwineFormatException>(() => Read("1e400"));

        Assert.Contains("not a finite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WithScalars_ReadsEachKind()
    {
        var node = Assert.IsType<IpldList>(Read("""["a",-5,true,false,null,{"k":[]}]"""));

        Assert.Equal("a", Assert.IsType<IpldString>(node.Items[0]).Value);
        Assert.Equal(-5, Assert.IsType<IpldInteger>(node.Items[1]).Value);
        Assert.True(Assert.IsType<IpldBoolean>(node.Items[2]).Value);
        Assert.False(Assert.IsType<IpldBoolean>(node.Items[3]).Value);
        Assert.IsType<IpldNull>(node.Items[4]);
        Assert.Empty(Assert.IsType<IpldMap>(node.Items[5]).Get<IpldList>("k").Items);
    }

    [Fact]
    public void Get_WithMissingOrMistypedField_Throws()
    {
        var map = Assert.IsType<IpldMap>(Read("""{"a":1}"""));

        Assert.Contains(
            "missing",
            Assert.Throws<TwineFormatException>(() => map.Get<IpldInteger>("b")).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "wrong type",
            Assert.Throws<TwineFormatException>(() => map.Get<IpldString>("a")).Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    private static IpldNode Read(string json) => DagJson.Read(JsonElement.Parse(json));
}
