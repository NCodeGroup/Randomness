// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Drand.Bls;

namespace NCode.Randomness.Drand.Tests.Bls;

public class G1HashToCurveTests
{
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
        var point = G1HashToCurve.MapToCurve(FieldElements.FromHex(u));

        Assert.Equal(x, Convert.ToHexStringLower(point.X.ToArray()));
        Assert.Equal(y, Convert.ToHexStringLower(point.Y.ToArray()));
    }

    #endregion
}
