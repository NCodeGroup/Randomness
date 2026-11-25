// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NCode.Randomness.Drand.Bls;

namespace NCode.Randomness.Drand.Tests.Bls;

public class FieldElementsTests
{
    #region Reduce Tests

    [Fact]
    public void Reduce_With64MaxBytes_ReturnsValueModuloP()
    {
        var expected = FieldElements.FromInteger(
            ((BigInteger.One << 512) - 1) % FieldElements.Modulus
        );

        var actual = FieldElements.Reduce(Enumerable.Repeat((byte)0xFF, 64).ToArray());

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
        var actual = FieldElements.IsSquare(FieldElements.FromInteger(value));

        Assert.Equal(expected, actual);
    }

    #endregion

    #region Sign Tests

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Sign_WithValue_ReturnsExpected(int value, bool expected)
    {
        var actual = FieldElements.Sign(FieldElements.FromInteger(value));

        Assert.Equal(expected, actual);
    }

    #endregion
}
