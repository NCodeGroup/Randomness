// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class IpldMapTests
{
    #region TryGet Tests

    [Fact]
    public void TryGet_WithMissingField_ReturnsFalse()
    {
        var read = Read("""{"a":1}""");
        var map = Assert.IsType<IpldMap>(read);

        var found = map.TryGet<IpldInteger>("b", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_WithMistypedField_Throws()
    {
        var read = Read("""{"a":1}""");
        var map = Assert.IsType<IpldMap>(read);

        var exception = Assert.Throws<TwineFormatException>(() =>
            map.TryGet<IpldString>("a", out _)
        );

        Assert.Contains("wrong type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGet_WithField_ReturnsIt()
    {
        var read = Read("""{"a":1}""");
        var map = Assert.IsType<IpldMap>(read);

        var found = map.TryGet<IpldInteger>("a", out var value);

        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal(1, value.Value);
    }

    #endregion

    private static IpldNode Read(string json)
    {
        var element = JsonElement.Parse(json);
        return DagJson.Read(element);
    }
}
