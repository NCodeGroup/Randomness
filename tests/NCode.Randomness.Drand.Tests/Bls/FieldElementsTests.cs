// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NCode.Randomness.Drand.Bls;

namespace NCode.Randomness.Drand.Tests.Bls;

public class FieldElementsTests
{
    #region FromInteger Tests

    [Fact]
    public void FromInteger_WithModulus_Throws()
    {
        var value = FieldElements.Modulus;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FieldElements.FromInteger(value)
        );

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FromInteger_WithNegativeValue_Throws()
    {
        var value = BigInteger.MinusOne;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FieldElements.FromInteger(value)
        );

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FromInteger_WithLargestValue_RoundTrips()
    {
        var largest = FieldElements.Modulus - 1;

        var element = FieldElements.FromInteger(largest);
        var actual = FieldElements.ToInteger(element);

        Assert.Equal(largest, actual);
    }

    #endregion

    #region Reduce Tests

    [Fact]
    public void Reduce_With64MaxBytes_ReturnsValueModuloP()
    {
        var reduced = ((BigInteger.One << 512) - 1) % FieldElements.Modulus;
        var expected = FieldElements.FromInteger(reduced);
        var bytes = Enumerable.Repeat((byte)0xFF, 64).ToArray();

        var actual = FieldElements.Reduce(bytes);

        Assert.Equal(expected, actual);
    }

    #endregion

    #region IsSquare Tests

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(11, false)]
    public void IsSquare_WithValue_ReturnsExpected(int value, bool expected)
    {
        var element = FieldElements.FromInteger(value);

        var actual = FieldElements.IsSquare(element);

        Assert.Equal(expected, actual);
    }

    #endregion

    #region Sign Tests

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Sign_WithValue_ReturnsExpected(int value, bool expected)
    {
        var element = FieldElements.FromInteger(value);

        var actual = FieldElements.Sign(element);

        Assert.Equal(expected, actual);
    }

    #endregion
}
