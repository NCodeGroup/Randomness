// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Quantum;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby;

/// <summary>
/// The CURBy-Q beacon: rounds of the pinned CURBy-Q (DIRNG) Twine chain, each checked as the CURBy client library
/// checks them. Every pulse of the round must be signed by the chain's key; the response pulse on the pinned Bell
/// chain must answer the request in causal order with the CURBy-Q pulses; the seed pulse on the seed chain the
/// precommit names must be in causal order with them; and the seed must expand from the seed pulse. The Trevisan
/// extraction itself is not verified. The randomness is the extractor's output that the randomness pulse publishes.
/// </summary>
internal sealed class CurbyQuantumBeacon : IRandomnessBeacon
{
    private const string RoundPath = "curbyq/round/";

    /// <summary>
    /// Initializes the beacon with the Twine API, the chain cache and the pinned chains.
    /// </summary>
    /// <param name="api">The client for the Twine API.</param>
    /// <param name="chains">The cache of verified chains.</param>
    /// <param name="options">The options that pin the CURBy-Q and Bell chains.</param>
    public CurbyQuantumBeacon(
        TwineApi api,
        TwineChainCache chains,
        IOptions<CurbyBeaconOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        Api = api;
        Chains = chains;
        QuantumChain = Cid.Parse(options.Value.QuantumChainId);
        BellChain = Cid.Parse(options.Value.BellChainId);
    }

    /// <inheritdoc/>
    public string Name => CurbyDefaults.QuantumBeaconName;

    private TwineApi Api { get; }

    private TwineChainCache Chains { get; }

    private Cid QuantumChain { get; }

    private Cid BellChain { get; }

    /// <summary>
    /// The last pulse verified, so the same round fetched again needs no crawl.
    /// </summary>
    private BeaconPulse? _last;

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync($"{RoundPath}latest", null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken)
    {
        if (round < 0)
        {
            var exception = new BeaconException(
                BeaconError.PulseNotFound,
                Name,
                $"Round {round} of CURBy-Q does not exist."
            );
            return ValueTask.FromException<BeaconPulse>(exception);
        }

        var roundText = round.ToString(CultureInfo.InvariantCulture);
        var path = $"{RoundPath}{roundText}";
        return FetchAsync(path, round, cancellationToken);
    }

    /// <summary>
    /// Reads a round from the Twine API, verifies it and returns its pulse. The round's chains are crawled by
    /// <see cref="CrawlBellAsync"/> and <see cref="CrawlSeedAsync"/>. A round that does not verify throws a
    /// <see cref="BeaconException"/> with <see cref="BeaconError.InvalidPulse"/>, and an outage one with
    /// <see cref="BeaconError.Unavailable"/>. A round with no randomness pulse throws
    /// <see cref="BeaconError.PulseNotFound"/> for a numbered round and <see cref="BeaconError.Unavailable"/> for the
    /// latest.
    /// </summary>
    /// <param name="path">The path of the round, such as <c>curbyq/round/28297</c> or <c>curbyq/round/latest</c>.</param>
    /// <param name="round">The round number the answer must carry, or <see langword="null"/> for the latest round.</param>
    /// <param name="cancellationToken">The token that cancels the fetch.</param>
    /// <returns>The verified pulse of the round.</returns>
    private async ValueTask<BeaconPulse> FetchAsync(
        string path,
        long? round,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var chain = await Chains
                .GetAsync(QuantumChain, CurbyQuantumRoundVerifier.QuantumFamily, cancellationToken)
                .ConfigureAwait(false);
            var documents = await Api.GetDocumentsAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (documents.IsEmpty)
            {
                throw round is null
                    ? new BeaconException(
                        BeaconError.Unavailable,
                        Name,
                        "CURBy-Q has no latest round."
                    )
                    : new BeaconException(
                        BeaconError.PulseNotFound,
                        Name,
                        $"CURBy-Q has not published round {round}."
                    );
            }

            var verified = QuantumRound.Verify(documents, chain);
            if (round is { } expected && verified.Number != expected)
            {
                throw new TwineFormatException(
                    $"CURBy-Q answered round {expected} with round {verified.Number}."
                );
            }

            if (verified.Result is not { } result)
            {
                var message =
                    $"Round {verified.Number} of CURBy-Q produced no randomness: {verified.ErrorReason ?? "it has not completed"}.";
                throw new BeaconException(
                    round is null ? BeaconError.Unavailable : BeaconError.PulseNotFound,
                    Name,
                    message
                );
            }

            var resultId = result.ResultPulse.Cid.ToString();
            if (
                Volatile.Read(ref _last) is { } last
                && string.Equals(last.Id, resultId, StringComparison.Ordinal)
            )
            {
                return last;
            }

            await CrawlBellAsync(result, cancellationToken).ConfigureAwait(false);
            var seed = await CrawlSeedAsync(result, cancellationToken).ConfigureAwait(false);
            var numberText = verified.Number.ToString(CultureInfo.InvariantCulture);
            var paramsPath = $"{RoundPath}{numberText}/params";
            var parameters = await Api.GetBytesAsync(paramsPath, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new TwineFormatException(
                    $"The Twine API does not serve the parameters of round {verified.Number}."
                );
            result.VerifySeed(seed, parameters);

            var pulse = CurbyQuantumRoundVerifier.ToBeaconPulse(chain, verified.Number, result);
            Volatile.Write(ref _last, pulse);
            return pulse;
        }
        catch (Exception exception)
            when (TwineErrors.ToBeaconException(exception, Name, "CURBy-Q", cancellationToken)
                    is { } translated
            )
        {
            throw translated;
        }
    }

    /// <summary>
    /// Walks the Bell chain back from the pulse the precommit mixes in to the response to the request, fetching each
    /// pulse with <see cref="GetDocumentAsync"/>. The walk's checks and limits are
    /// <see cref="QuantumBellWalk"/>'s, so it throws <see cref="TwineFormatException"/> when no pulse within ten
    /// answers the request or the chain starts first.
    /// </summary>
    /// <param name="result">The round's pulses.</param>
    /// <param name="cancellationToken">The token that cancels the fetches.</param>
    private async ValueTask CrawlBellAsync(
        QuantumResult result,
        CancellationToken cancellationToken
    )
    {
        var bell = await Chains
            .GetAsync(BellChain, CurbyQuantumRoundVerifier.BellFamily, cancellationToken)
            .ConfigureAwait(false);
        var walk = new QuantumBellWalk(result, bell);
        bool answered;
        do
        {
            var document = await GetDocumentAsync(bell, walk.Next, cancellationToken)
                .ConfigureAwait(false);
            answered = walk.Visit(document);
        } while (!answered);
    }

    /// <summary>
    /// Walks the seed chain back from the pulse the randomness pulse mixes in to the first pulse that does not mix in
    /// the precommit, fetching each pulse with <see cref="GetDocumentAsync"/>, and returns the seed pulse. The walk's
    /// checks and limits are <see cref="QuantumSeedWalk"/>'s, so it throws <see cref="TwineFormatException"/> when ten
    /// pulses in a row mix in the precommit or the chain starts first.
    /// </summary>
    /// <param name="result">The round's pulses.</param>
    /// <param name="cancellationToken">The token that cancels the fetches.</param>
    /// <returns>The pulse the seed expands from.</returns>
    private async ValueTask<TwineSignedPulse> CrawlSeedAsync(
        QuantumResult result,
        CancellationToken cancellationToken
    )
    {
        var seedChain = await Chains
            .GetAsync(result.SeedChain, null, cancellationToken)
            .ConfigureAwait(false);
        var walk = new QuantumSeedWalk(result, seedChain);
        TwineSignedPulse? seed = null;
        while (seed is null)
        {
            var document = await GetDocumentAsync(seedChain, walk.Next, cancellationToken)
                .ConfigureAwait(false);
            seed = walk.Visit(document);
        }

        return seed;
    }

    /// <summary>
    /// Fetches the document of the pulse with the CID <paramref name="cid"/> from <paramref name="chain"/>. The
    /// document must be the one requested.
    /// </summary>
    /// <param name="chain">The verified chain the pulse belongs to.</param>
    /// <param name="cid">The CID of the pulse.</param>
    /// <param name="cancellationToken">The token that cancels the fetch.</param>
    /// <returns>The document of the pulse, not yet verified.</returns>
    private async ValueTask<TwineDocument> GetDocumentAsync(
        TwineChain chain,
        Cid cid,
        CancellationToken cancellationToken
    )
    {
        var path = $"chains/{chain.Cid}/pulses/{cid}";
        var document = await Api.GetAsync(path, cancellationToken).ConfigureAwait(false)
            ?? throw new TwineFormatException($"The Twine API does not serve pulse {cid}.");
        if (!document.Cid.Equals(cid))
        {
            throw new TwineFormatException(
                $"The Twine API answered pulse {cid} with pulse {document.Cid}."
            );
        }

        return document;
    }
}
