// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Tests.TestNist;

/// <summary>
/// Assertions the beacon tests share.
/// </summary>
// Copied on purpose from BeaconAssert in NCode.Randomness.Curby.Tests, as NCode.Randomness.Drand.Tests copies it:
// each test project stands alone, so a fix to one copy belongs in the others.
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
