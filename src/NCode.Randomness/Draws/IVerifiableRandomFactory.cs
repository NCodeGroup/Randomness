// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// Creates reproducible draws from the registered beacons' pulses.
/// </summary>
public interface IVerifiableRandomFactory
{
    /// <summary>
    /// Fetches and verifies the pulse of <paramref name="round"/> from <paramref name="beacon"/>, and derives its
    /// stream for <paramref name="context"/>.
    /// </summary>
    /// <param name="beacon">The name a beacon is registered under, such as <c>curby</c>.</param>
    /// <param name="round">The round, committed to before the beacon published it.</param>
    /// <param name="context">What the draw is for, committed to with the round.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The stream; dispose it when the draw is done.</returns>
    /// <exception cref="ArgumentException">No beacon is registered under <paramref name="beacon"/>.</exception>
    /// <exception cref="BeaconException">The beacon gave no verifiable pulse for the round.</exception>
    ValueTask<VerifiableRandom> CreateAsync(
        string beacon,
        long round,
        string context,
        CancellationToken cancellationToken
    );
}
