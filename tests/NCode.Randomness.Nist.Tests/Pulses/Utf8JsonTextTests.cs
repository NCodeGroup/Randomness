// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist.Tests.Pulses;

public class Utf8JsonTextTests
{
    #region IsText Tests

    [Theory]
    [InlineData("""{"a":"b"}""")]
    [InlineData("""["\uD83D\uDE00"]""")]
    [InlineData("""["\\uD800"]""")]
    [InlineData("""["\u0041\n\"\/"]""")]
    [InlineData("[\"\u00e9\"]")]
    public void IsText_WithWellFormedText_ReturnsTrue(string json)
    {
        var utf8Json = Encoding.UTF8.GetBytes(json);

        var actual = Utf8JsonText.IsText(utf8Json);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("""["\uD800"]""")]
    [InlineData("""["\uDC00"]""")]
    [InlineData("""["\uD800\u0041"]""")]
    [InlineData("""["\uD800\n"]""")]
    [InlineData("""["\uD800\uD800"]""")]
    [InlineData("""["\uD800""")]
    public void IsText_WithUnpairedSurrogateEscape_ReturnsFalse(string json)
    {
        var utf8Json = Encoding.UTF8.GetBytes(json);

        var actual = Utf8JsonText.IsText(utf8Json);

        Assert.False(actual);
    }

    [Fact]
    public void IsText_WithInvalidUtf8Byte_ReturnsFalse()
    {
        byte[] utf8Json = [.. "[\""u8, 0x80, .. "\"]"u8];

        var actual = Utf8JsonText.IsText(utf8Json);

        Assert.False(actual);
    }

    [Fact]
    public void IsText_WithEncodedSurrogate_ReturnsFalse()
    {
        byte[] utf8Json = [.. "[\""u8, 0xED, 0xA0, 0x80, .. "\"]"u8];

        var actual = Utf8JsonText.IsText(utf8Json);

        Assert.False(actual);
    }

    #endregion
}
