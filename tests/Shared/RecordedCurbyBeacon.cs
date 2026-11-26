// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;
using NCode.Randomness.Curby;

namespace Recorded;

/// <summary>
/// A CURBy beacon over the recorded CURBy-RNG documents, so a draw sample runs the real pulse verification without the
/// network. It serves rounds 1620422 and 1620423, each verified by <see cref="CurbyPulseVerifier"/>.
/// </summary>
public sealed class RecordedCurbyBeacon : IRandomnessBeacon
{
    private const long LatestRound = 1620423;

    /// <inheritdoc/>
    public string Name => CurbyDefaults.BeaconName;

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        GetPulseAsync(LatestRound, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken) =>
        round is 1620422 or LatestRound
            ? ValueTask.FromResult(RecordedDocuments.CurbyPulse(round))
            : ValueTask.FromException<BeaconPulse>(
                new BeaconException(
                    BeaconError.PulseNotFound,
                    Name,
                    $"Round {round} is not recorded."
                )
            );
}
