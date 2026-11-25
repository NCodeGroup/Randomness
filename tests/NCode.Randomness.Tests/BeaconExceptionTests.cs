// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests;

public class BeaconExceptionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithInnerException_KeepsEveryPart()
    {
        var inner = new HttpRequestException("down");

        var exception = new BeaconException(BeaconError.Unavailable, "curby", "unreachable", inner);

        Assert.Equal(BeaconError.Unavailable, exception.Error);
        Assert.Equal("curby", exception.Beacon);
        Assert.Equal("unreachable", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    #endregion
}
