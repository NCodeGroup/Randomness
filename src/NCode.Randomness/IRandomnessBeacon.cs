// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// A public randomness beacon, such as CURBy: the port each vendor's adapter implements.
/// </summary>
/// <remarks>
/// <para>
/// An implementation verifies every pulse as its vendor's protocol specifies before returning it, and throws
/// <see cref="BeaconException"/> rather than return a pulse it could not verify. It bounds each call with its own
/// timeouts and retries, and is safe to call from many threads at once.
/// </para>
/// <para>
/// Register one with <c>AddBeacon&lt;TBeacon&gt;()</c> on the builder <c>AddRandomness()</c> returns.
/// </para>
/// </remarks>
public interface IRandomnessBeacon
{
    /// <summary>
    /// Gets the name the beacon is registered under, unique among the registered beacons, such as <c>curby</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Fetches and verifies the beacon's latest pulse.
    /// </summary>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">The beacon gave no verifiable pulse.</exception>
    ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Fetches and verifies the pulse of <paramref name="round"/>.
    /// </summary>
    /// <param name="round">The pulse's position in the beacon's chain.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">The beacon gave no verifiable pulse for the round, with
    /// <see cref="BeaconError.PulseNotFound"/> when the round has not been published.</exception>
    ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken);
}
