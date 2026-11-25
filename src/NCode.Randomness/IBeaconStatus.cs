// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// What the beacon collector has done: for status pages, health checks, and code that waits for beacon entropy.
/// </summary>
public interface IBeaconStatus
{
    /// <summary>
    /// Gets the mode the collector runs in.
    /// </summary>
    BeaconMode Mode { get; }

    /// <summary>
    /// Gets a snapshot of each registered beacon, in registration order.
    /// </summary>
    IReadOnlyList<BeaconState> Beacons { get; }

    /// <summary>
    /// Waits until at least one beacon pulse has been mixed into the generator. Generation never needs this; use it
    /// where a policy demands beacon entropy before some operation.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes once a pulse is mixed in.</returns>
    ValueTask WaitForPulseAsync(CancellationToken cancellationToken);
}
