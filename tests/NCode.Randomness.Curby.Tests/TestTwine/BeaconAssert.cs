// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Assertions the beacon tests share.
/// </summary>
internal static class BeaconAssert
{
    /// <summary>
    /// Asserts that fetching a pulse fails with a <see cref="BeaconException"/> of the given beacon and error.
    /// </summary>
    public static async ValueTask<BeaconException> AssertBeaconErrorAsync(
        Func<CancellationToken, ValueTask<BeaconPulse>> fetch,
        string beacon,
        BeaconError error,
        CancellationToken cancellationToken
    )
    {
        var exception = await Assert.ThrowsAsync<BeaconException>(async () =>
            await fetch(cancellationToken)
        );
        Assert.Equal(error, exception.Error);
        Assert.Equal(beacon, exception.Beacon);
        return exception;
    }
}
