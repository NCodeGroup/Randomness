// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Drand.Bls;

namespace NCode.Randomness.Drand.Tests.Bls;

public class ExpandMessageXmdTests
{
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
        "QUUX-V01-CS02-with-expander-SHA256-128"
    );

    #region Expand Tests

    [Theory]
    [InlineData("", 32, "68a985b87eb6b46952128911f2a4412bbc302a9d759667f87f7a21d803f07235")]
    [InlineData("abc", 32, "d8ccab23b5985ccea865c6c97b6e5b8350e794e603b4b97902f53a8a0d605615")]
    [InlineData(
        "",
        128,
        "af84c27ccfd45d41914fdff5df25293e221afc53d8ad2ac06d5e3e29485dadbee0d121587713a3e0dd4d5e69e93eb7cd4f5df4cd103e188cf60cb02edc3edf18eda8576c412b18ffb658e3dd6ec849469b979d444cf7b26911a08e63cf31f9dcc541708d3491184472c2c29bb749d4286b004ceb5ee6b9a7fa5b646c993f0ced"
    )]
    public void Expand_WithRfcVector_ReturnsExpectedBytes(
        string message,
        int length,
        string expected
    )
    {
        var actual = ExpandMessageXmd.Expand(Encoding.ASCII.GetBytes(message), Domain, length);

        Assert.Equal(expected, Convert.ToHexStringLower(actual));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void Expand_WithInvalidDomainLength_Throws(int domainLength)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExpandMessageXmd.Expand([1], new byte[domainLength], 32)
        );

        Assert.Contains("domain", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8161)]
    public void Expand_WithInvalidLength_Throws(int length)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExpandMessageXmd.Expand([1], Domain, length)
        );

        Assert.Contains("length", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
