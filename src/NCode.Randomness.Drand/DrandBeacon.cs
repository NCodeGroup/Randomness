// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NCode.Randomness.Drand.Http;
using NCode.Randomness.Drand.Rounds;

namespace NCode.Randomness.Drand;

/// <summary>
/// The drand beacon: rounds of the pinned quicknet chain, each checked before any byte is used. The round's BLS12-381
/// signature must verify against the chain's pinned group public key. The randomness is the SHA-256 hash of the
/// signature.
/// </summary>
/// <param name="api">The client for the drand API.</param>
/// <param name="chain">The pinned chain whose group public key verifies every round.</param>
internal sealed class DrandBeacon(DrandApi api, DrandChain chain) : IRandomnessBeacon
{
    public string Name => DrandDefaults.BeaconName;

    private DrandApi Api { get; } = api;

    private DrandChain Chain { get; } = chain;

    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync("latest", null, cancellationToken);

    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken)
    {
        if (round < 1)
        {
            var exception = new BeaconException(
                BeaconError.PulseNotFound,
                Name,
                $"Round {round} of drand does not exist; rounds start at 1."
            );
            return ValueTask.FromException<BeaconPulse>(exception);
        }

        var selector = round.ToString(CultureInfo.InvariantCulture);
        return FetchAsync(selector, round, cancellationToken);
    }

    private async ValueTask<BeaconPulse> FetchAsync(
        string selector,
        long? round,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var document = await Api.GetAsync($"{Chain.Hash}/public/{selector}", cancellationToken)
                .ConfigureAwait(false);
            if (document is null)
            {
                throw round is null
                    ? new BeaconException(
                        BeaconError.Unavailable,
                        Name,
                        "drand has no latest round."
                    )
                    : new BeaconException(
                        BeaconError.PulseNotFound,
                        Name,
                        $"drand has not published round {round}."
                    );
            }

            var verified = DrandRound.Verify(document.Value, Chain);
            if (round is { } expected && verified.Round != expected)
            {
                throw new DrandFormatException(
                    $"drand answered round {expected} with round {verified.Round}."
                );
            }

            var id = Convert.ToHexStringLower(verified.Signature);
            return new BeaconPulse(
                Name,
                Chain.Hash,
                verified.Round,
                id,
                verified.Timestamp,
                verified.Randomness
            );
        }
        catch (Exception exception)
            when (DrandErrors.ToBeaconException(exception, Name, cancellationToken)
                    is { } translated
            )
        {
            throw translated;
        }
    }
}
