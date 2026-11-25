// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Randomness.Drand.Bls;
using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Tests.Bls;

public class G1HashToCurveTests
{
    // RFC 9380 appendix J.9.1: BLS12381G1_XMD:SHA-256_SSWU_RO_ test vectors (u, Q0, Q1 and P for msg "" and "abc").
    private static ReadOnlySpan<byte> Domain =>
        "QUUX-V01-CS02-with-BLS12381G1_XMD:SHA-256_SSWU_RO_"u8;

    #region HashToField Tests

    [Fact]
    public void HashToField_WithEmptyMessage_ReturnsRfcVector()
    {
        var u = G1HashToCurve.HashToField([], Domain);
        var firstBytes = u[0].ToArray();
        var secondBytes = u[1].ToArray();
        var first = Convert.ToHexStringLower(firstBytes);
        var second = Convert.ToHexStringLower(secondBytes);

        Assert.Equal(2, u.Length);
        Assert.Equal(
            "0ba14bd907ad64a016293ee7c2d276b8eae71f25a4b941eece7b0d89f17f75cb3ae5438a614fb61d6835ad59f29c564f",
            first
        );
        Assert.Equal(
            "019b9bd7979f12657976de2884c7cce192b82c177c80e0ec604436a7f538d231552f0d96d9f7babe5fa3b19b3ff25ac9",
            second
        );
    }

    #endregion

    #region Hash Tests

    [Theory]
    [InlineData(
        "",
        "052926add2207b76ca4fa57a8734416c8dc95e24501772c814278700eed6d1e4e8cf62d9c09db0fac349612b759e79a1",
        "08ba738453bfed09cb546dbb0783dbb3a5f1f566ed67bb6be0e8c67e2e81a4cc68ee29813bb7994998f3eae0c9c6a265"
    )]
    [InlineData(
        "abc",
        "03567bc5ef9c690c2ab2ecdf6a96ef1c139cc0b2f284dca0a9a7943388a49a3aee664ba5379a7655d3c68900be2f6903",
        "0b9c15f3fe6e5cf4211f346271d7b01c8f3b28be689c8429c85b67af215533311f0b8dfaaa154fa6b88176c229f2885d"
    )]
    public void Hash_WithRfcVector_ReturnsExpectedPoint(string message, string x, string y)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var point = G1HashToCurve.Hash(bytes, Domain);
        var xBytes = point.X.ToArray();
        var yBytes = point.Y.ToArray();
        var actualX = Convert.ToHexStringLower(xBytes);
        var actualY = Convert.ToHexStringLower(yBytes);

        Assert.Equal(x, actualX);
        Assert.Equal(y, actualY);
    }

    #endregion

    #region MapToCurve Tests

    [Theory]
    [InlineData(
        "0ba14bd907ad64a016293ee7c2d276b8eae71f25a4b941eece7b0d89f17f75cb3ae5438a614fb61d6835ad59f29c564f",
        "11a3cce7e1d90975990066b2f2643b9540fa40d6137780df4e753a8054d07580db3b7f1f03396333d4a359d1fe3766fe",
        "0eeaf6d794e479e270da10fdaf768db4c96b650a74518fc67b04b03927754bac66f3ac720404f339ecdcc028afa091b7"
    )]
    [InlineData(
        "0d921c33f2bad966478a03ca35d05719bdf92d347557ea166e5bba579eea9b83e9afa5c088573c2281410369fbd32951",
        "125435adce8e1cbd1c803e7123f45392dc6e326d292499c2c45c5865985fd74fe8f042ecdeeec5ecac80680d04317d80",
        "0e8828948c989126595ee30e4f7c931cbd6f4570735624fd25aef2fa41d3f79cfb4b4ee7b7e55a8ce013af2a5ba20bf2"
    )]
    public void MapToCurve_WithRfcVector_ReturnsExpectedPoint(string u, string x, string y)
    {
        var element = FieldElements.FromHex(u);
        var point = G1HashToCurve.MapToCurve(element);
        var xBytes = point.X.ToArray();
        var yBytes = point.Y.ToArray();
        var actualX = Convert.ToHexStringLower(xBytes);
        var actualY = Convert.ToHexStringLower(yBytes);

        Assert.Equal(x, actualX);
        Assert.Equal(y, actualY);
    }

    // u = 0 makes tv1 zero, the exceptional case of RFC 9380 section 6.6.2. The expected point, map_to_curve(0) with
    // its cofactor cleared, comes from @noble/curves 2.4.0, whose hashToCurve gives RFC 9380 J.9.1's P for msg "": node
    // --input-type=module -e "import { bls12_381 } from '@noble/curves/bls12-381.js'; const p =
    // bls12_381.G1.mapToCurve(0n).toAffine(); console.log(p.x.toString(16).padStart(96, '0'),
    // p.y.toString(16).padStart(96, '0'));"
    [Fact]
    public void MapToCurve_WithZero_ClearsToTheReferencePoint()
    {
        var point = G1HashToCurve.MapToCurve(Fp.Zero);
        var projective = new G1Projective(point);
        var cleared = projective.ClearCofactor();
        var affine = new G1Affine(cleared);
        var xBytes = affine.X.ToArray();
        var yBytes = affine.Y.ToArray();
        var x = Convert.ToHexStringLower(xBytes);
        var y = Convert.ToHexStringLower(yBytes);

        Assert.Equal(
            "11a9a0372b8f332d5c30de9ad14e50372a73fa4c45d5f2fa5097f2d6fb93bcac592f2e1711ac43db0519870c7d0ea415",
            x
        );
        Assert.Equal(
            "092c0f994164a0719f51c24ba3788de240ff926b55f58c445116e8bc6a47cd63392fd4e8e22bdf9feaa96ee773222133",
            y
        );
    }

    #endregion
}
