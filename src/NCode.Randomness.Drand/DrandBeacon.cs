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
    /// <inheritdoc/>
    public string Name => DrandDefaults.BeaconName;

    /// <summary>
    /// Gets the client that fetches rounds from the drand API.
    /// </summary>
    private DrandApi Api { get; } = api;

    /// <summary>
    /// Gets the pinned chain: its hash, period, genesis time and group public key.
    /// </summary>
    private DrandChain Chain { get; } = chain;

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync("latest", null, cancellationToken);

    /// <inheritdoc/>
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

    /// <summary>
    /// Fetches a round of the pinned chain, verifies it and maps it to a pulse.
    /// </summary>
    /// <param name="selector">The round selector of the path: <c>latest</c> or a round number.</param>
    /// <param name="round">The round the caller asked for, or <see langword="null"/> for the latest.</param>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">
    /// The round does not exist yet, the answer is malformed or does not verify, or the API is unavailable.
    /// </exception>
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

            return DrandRoundVerifier.ToBeaconPulse(Chain, verified);
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
