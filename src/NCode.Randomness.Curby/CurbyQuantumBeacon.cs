// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
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
    private const int MaxCrawl = 10;

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

    public string Name => CurbyDefaults.QuantumBeaconName;

    private TwineApi Api { get; }

    private TwineChainCache Chains { get; }

    private Cid QuantumChain { get; }

    private Cid BellChain { get; }

    // The last pulse verified, so the same round fetched again needs no crawl.
    private volatile BeaconPulse? _last;

    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync($"{RoundPath}latest", null, cancellationToken);

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

    private async ValueTask<BeaconPulse> FetchAsync(
        string path,
        long? round,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var chain = await Chains
                .GetAsync(QuantumChain, "diqrng", cancellationToken)
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
            if (_last is { } last && string.Equals(last.Id, resultId, StringComparison.Ordinal))
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

            var chainId = chain.Cid.ToString();
            var pulse = new BeaconPulse(
                Name,
                chainId,
                verified.Number,
                resultId,
                result.Timestamp,
                result.Randomness
            );
            _last = pulse;
            return pulse;
        }
        catch (Exception exception) when (exception is TwineFormatException or JsonException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                Name,
                $"CURBy-Q answered with data that fails verification: {exception.Message}",
                exception
            );
        }
        catch (Exception exception) when (CurbyBeacon.IsOutage(exception, cancellationToken))
        {
            throw new BeaconException(
                BeaconError.Unavailable,
                Name,
                $"CURBy-Q is unavailable: {exception.Message}",
                exception
            );
        }
        catch (Exception exception) when (CurbyBeacon.IsMalformed(exception, cancellationToken))
        {
            // Anything else the parser or verifier throws, such as an invalid UTF-16 escape, is bad data too.
            throw new BeaconException(
                BeaconError.InvalidPulse,
                Name,
                $"CURBy-Q answered with data that cannot be read: {exception.Message}",
                exception
            );
        }
    }

    // Walks the Bell chain back from the pulse the precommit mixes in to the response to the request.
    private async ValueTask CrawlBellAsync(
        QuantumResult result,
        CancellationToken cancellationToken
    )
    {
        var bell = await Chains
            .GetAsync(BellChain, "bell", cancellationToken)
            .ConfigureAwait(false);
        var cid = result.Precommit.MixinOf(BellChain)
            ?? throw new TwineFormatException("The precommit does not mix in the Bell chain.");
        TwineSignedPulse? newer = null;
        for (var count = 0; count < MaxCrawl; ++count)
        {
            var pulse = await GetCrawledAsync(bell, cid, newer, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsBellResponse(pulse))
            {
                return;
            }

            cid = pulse.Previous
                ?? throw new TwineFormatException(
                    "The Bell chain starts before any response to the request."
                );
            newer = pulse;
        }

        throw new TwineFormatException(
            $"No response to the request is within {MaxCrawl} Bell pulses of the one the precommit mixes in."
        );
    }

    // Walks the seed chain back from the pulse the randomness pulse mixes in and returns the seed pulse.
    private async ValueTask<TwineSignedPulse> CrawlSeedAsync(
        QuantumResult result,
        CancellationToken cancellationToken
    )
    {
        var seedChain = await Chains
            .GetAsync(result.SeedChain, null, cancellationToken)
            .ConfigureAwait(false);
        var cid = result.ResultPulse.MixinOf(result.SeedChain)
            ?? throw new TwineFormatException("The result does not mix in the seed chain.");
        var mixing = new List<TwineSignedPulse>();
        TwineSignedPulse? newer = null;
        for (var count = 0; count < MaxCrawl; ++count)
        {
            var pulse = await GetCrawledAsync(seedChain, cid, newer, cancellationToken)
                .ConfigureAwait(false);
            if (!result.MixesPrecommit(pulse))
            {
                return result.SelectSeed(mixing);
            }

            mixing.Add(pulse);
            cid = pulse.Previous
                ?? throw new TwineFormatException(
                    "The seed chain starts before any pulse that does not mix in the precommit."
                );
            newer = pulse;
        }

        throw new TwineFormatException($"{MaxCrawl} seed pulses in a row mix in the precommit.");
    }

    private async ValueTask<TwineSignedPulse> GetCrawledAsync(
        TwineChain chain,
        Cid cid,
        TwineSignedPulse? newer,
        CancellationToken cancellationToken
    )
    {
        var path = $"chains/{chain.Cid}/pulses/{cid}";
        var document = await Api.GetAsync(path, cancellationToken).ConfigureAwait(false)
            ?? throw new TwineFormatException($"The Twine API does not serve pulse {cid}.");
        var pulse = TwineSignedPulse.Verify(document, chain);
        if (!pulse.Cid.Equals(cid))
        {
            throw new TwineFormatException(
                $"The Twine API answered pulse {cid} with pulse {pulse.Cid}."
            );
        }

        newer?.VerifyPrecededBy(pulse);
        return pulse;
    }
}
