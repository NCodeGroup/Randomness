// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Http;
using NCode.Randomness.Curby.Twine;
using Polly;

namespace NCode.Randomness.Curby;

/// <summary>
/// The CURBy beacon: pulses of the pinned CURBy-RNG Twine chain, each checked as the CURBy client library checks
/// them. The chain must hash to the pinned CID and be signed by its own key; the pulse and its predecessor must each
/// hash to their CIDs and be signed by the chain's key with RS256; the predecessor must be the pulse's first link and
/// one round earlier; and the pulse's salt must match the predecessor's precommitment. The randomness is the
/// pulse's SHA3-512 digest.
/// </summary>
internal sealed class CurbyBeacon : IRandomnessBeacon, IDisposable
{
    public CurbyBeacon(TwineApi api, IOptions<CurbyBeaconOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Api = api;
        PinnedChain = Cid.Parse(options.Value.ChainId);
    }

    public string Name => CurbyDefaults.BeaconName;

    private TwineApi Api { get; }

    private Cid PinnedChain { get; }

    private SemaphoreSlim ChainGate { get; } = new(1, 1);

    // The verified chain, fetched once: its CID is pinned, so it never changes.
    private TwineChain? _chain;

    // The last pulse verified, so the next round's predecessor, usually this pulse, needs no fetch.
    private TwinePulse? _lastVerified;

    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync("latest", null, cancellationToken);

    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken)
    {
        if (round < 1)
        {
            // The first pulse of a chain has no predecessor, so it has no committed salt and no randomness.
            var exception = new BeaconException(
                BeaconError.PulseNotFound,
                Name,
                $"Round {round} of CURBy has no randomness."
            );
            return ValueTask.FromException<BeaconPulse>(exception);
        }

        return FetchAsync(round.ToString(CultureInfo.InvariantCulture), round, cancellationToken);
    }

    public void Dispose()
    {
        _chain?.Dispose();
        ChainGate.Dispose();
    }

    private async ValueTask<BeaconPulse> FetchAsync(
        string selector,
        long? round,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var chain = await GetChainAsync(cancellationToken).ConfigureAwait(false);
            var pulse = await GetPulseAsync(chain, selector, cancellationToken)
                .ConfigureAwait(false);
            if (pulse is null)
            {
                throw round is null
                    ? new BeaconException(
                        BeaconError.Unavailable,
                        Name,
                        "CURBy has no latest pulse."
                    )
                    : new BeaconException(
                        BeaconError.PulseNotFound,
                        Name,
                        $"CURBy has not published round {round}."
                    );
            }

            if (round is { } expected && pulse.Index != expected)
            {
                throw new TwineFormatException(
                    $"CURBy answered round {expected} with round {pulse.Index}."
                );
            }

            var previous = pulse.Previous
                ?? throw new TwineFormatException($"Pulse {pulse.Index} has no predecessor.");
            var predecessor = await GetPredecessorAsync(chain, previous, cancellationToken)
                .ConfigureAwait(false);
            pulse.VerifyFollows(predecessor);
            Volatile.Write(ref _lastVerified, pulse);
            return new BeaconPulse(
                Name,
                chain.Cid.ToString(),
                pulse.Index,
                pulse.Cid.ToString(),
                pulse.Timestamp,
                pulse.Cid.Digest
            );
        }
        catch (Exception exception) when (exception is TwineFormatException or JsonException)
        {
            throw new BeaconException(
                BeaconError.InvalidPulse,
                Name,
                $"CURBy answered with data that fails verification: {exception.Message}",
                exception
            );
        }
        catch (Exception exception) when (IsOutage(exception, cancellationToken))
        {
            throw new BeaconException(
                BeaconError.Unavailable,
                Name,
                $"CURBy is unavailable: {exception.Message}",
                exception
            );
        }
        catch (Exception exception) when (IsMalformed(exception, cancellationToken))
        {
            // Anything else the parser or verifier throws, such as an invalid UTF-16 escape, is bad data too.
            throw new BeaconException(
                BeaconError.InvalidPulse,
                Name,
                $"CURBy answered with data that cannot be read: {exception.Message}",
                exception
            );
        }
    }

    internal static bool IsMalformed(Exception exception, CancellationToken cancellationToken) =>
        exception is not (BeaconException or ObjectDisposedException or OperationCanceledException)
        && !IsOutage(exception, cancellationToken);

    internal static bool IsOutage(Exception exception, CancellationToken cancellationToken) =>
        exception is not BeaconException
        && (
            exception is HttpRequestException or TimeoutException or IOException
            || (
                exception is OperationCanceledException
                && !cancellationToken.IsCancellationRequested
            )
            || exception is ExecutionRejectedException
        );

    private async ValueTask<TwineChain> GetChainAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _chain) is { } cached)
        {
            return cached;
        }

        await ChainGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_chain is { } raced)
            {
                return raced;
            }

            var document = await Api.GetAsync($"chains/{PinnedChain}", cancellationToken)
                .ConfigureAwait(false)
                ?? throw new TwineFormatException(
                    $"The Twine API does not serve chain {PinnedChain}."
                );
            var chain = TwineChain.Verify(document, PinnedChain, "rng");
            Volatile.Write(ref _chain, chain);
            return chain;
        }
        finally
        {
            ChainGate.Release();
        }
    }

    private async ValueTask<TwinePulse?> GetPulseAsync(
        TwineChain chain,
        string selector,
        CancellationToken cancellationToken
    )
    {
        var document = await Api.GetAsync(
                $"chains/{chain.Cid}/pulses/{selector}",
                cancellationToken
            )
            .ConfigureAwait(false);
        return document is null ? null : TwinePulse.Verify(document, chain);
    }

    private async ValueTask<TwinePulse> GetPredecessorAsync(
        TwineChain chain,
        Cid previous,
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref _lastVerified) is { } last && last.Cid.Equals(previous))
        {
            return last;
        }

        return await GetPulseAsync(chain, previous.ToString(), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TwineFormatException($"The Twine API does not serve pulse {previous}.");
    }
}
