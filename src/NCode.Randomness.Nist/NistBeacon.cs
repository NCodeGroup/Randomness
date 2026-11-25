// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Options;
using NCode.Randomness.Nist.Http;
using NCode.Randomness.Nist.Pulses;

namespace NCode.Randomness.Nist;

/// <summary>
/// The NIST beacon: pulses of the pinned NIST Randomness Beacon 2.0 chain, each checked before any byte is used. The
/// pulse must name a pinned certificate that covers its timestamp, its RSA signature and its output value must verify,
/// and its predecessor, which must verify too, must be the pulse one index earlier on the same chain, hash-linked by
/// the pulse's <c>previous</c> value and committed to by the predecessor's precommitment. The randomness is the
/// pulse's 64-byte output value.
/// </summary>
/// <param name="api">The client for the NIST API.</param>
/// <param name="certificates">The cache of pinned certificates.</param>
/// <param name="options">The options that pin the chain.</param>
internal sealed class NistBeacon(
    NistApi api,
    NistCertificateCache certificates,
    IOptions<NistBeaconOptions> options
) : IRandomnessBeacon
{
    /// <inheritdoc/>
    public string Name => NistDefaults.BeaconName;

    /// <summary>
    /// Gets the client that fetches pulses from the NIST API.
    /// </summary>
    private NistApi Api { get; } = api;

    /// <summary>
    /// Gets the cache of pinned certificates that verify each pulse's signature.
    /// </summary>
    private NistCertificateCache Certificates { get; } = certificates;

    /// <summary>
    /// Gets the index of the chain whose pulses this beacon accepts.
    /// </summary>
    private long ChainIndex { get; } = options.Value.ChainIndex;

    /// <summary>
    /// The last pulse verified, so the next index's predecessor, usually this pulse, needs no fetch.
    /// </summary>
    private NistPulse? _lastVerified;

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync("last", null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken)
    {
        if (round < 2)
        {
            // A pulse is used only with its verified predecessor, so the first pulse of a chain has no randomness.
            var exception = new BeaconException(
                BeaconError.PulseNotFound,
                Name,
                $"Pulse {round} of NIST has no predecessor to verify it against."
            );
            return ValueTask.FromException<BeaconPulse>(exception);
        }

        var selector = round.ToString(CultureInfo.InvariantCulture);
        return FetchAsync(selector, round, cancellationToken);
    }

    /// <summary>
    /// Fetches a pulse of the pinned chain and its predecessor, verifies both and maps the pulse to a beacon pulse.
    /// </summary>
    /// <param name="selector">The pulse selector of the path: <c>last</c> or a pulse index.</param>
    /// <param name="round">The pulse index the caller asked for, or <see langword="null"/> for the latest.</param>
    /// <param name="cancellationToken">The token that cancels the requests.</param>
    /// <returns>The verified pulse.</returns>
    /// <exception cref="BeaconException">
    /// The pulse does not exist yet, an answer is malformed or does not verify, or the API is unavailable.
    /// </exception>
    private async ValueTask<BeaconPulse> FetchAsync(
        string selector,
        long? round,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var pulse = await GetVerifiedAsync(selector, cancellationToken).ConfigureAwait(false);
            if (pulse is null)
            {
                throw round is null
                    ? new BeaconException(
                        BeaconError.Unavailable,
                        Name,
                        "NIST has no latest pulse."
                    )
                    : new BeaconException(
                        BeaconError.PulseNotFound,
                        Name,
                        $"NIST has not published pulse {round}."
                    );
            }

            if (round is { } expected && pulse.PulseIndex != expected)
            {
                throw new NistFormatException(
                    $"NIST answered pulse {expected} with pulse {pulse.PulseIndex}."
                );
            }

            if (pulse.PulseIndex < 2)
            {
                throw new NistFormatException(
                    $"Pulse {pulse.PulseIndex} starts chain {pulse.ChainIndex}; it has no predecessor."
                );
            }

            var predecessor = await GetPredecessorAsync(pulse, cancellationToken)
                .ConfigureAwait(false);
            var verified = NistPulseVerifier.ToBeaconPulse(pulse, predecessor);
            Volatile.Write(ref _lastVerified, pulse);
            return verified;
        }
        catch (Exception exception)
            when (NistErrors.ToBeaconException(exception, Name, cancellationToken) is { } translated
            )
        {
            throw translated;
        }
    }

    /// <summary>
    /// Fetches a pulse of the pinned chain, checks its chain and verifies it against its pinned certificate.
    /// </summary>
    /// <param name="selector">The pulse selector of the path: <c>last</c> or a pulse index.</param>
    /// <param name="cancellationToken">The token that cancels the requests.</param>
    /// <returns>The verified pulse, or <see langword="null"/> when the API answers 404.</returns>
    /// <exception cref="NistFormatException">The pulse is malformed, of another chain or does not verify.</exception>
    private async ValueTask<NistPulse?> GetVerifiedAsync(
        string selector,
        CancellationToken cancellationToken
    )
    {
        var chain = ChainIndex.ToString(CultureInfo.InvariantCulture);
        var document = await Api.GetPulseAsync($"chain/{chain}/pulse/{selector}", cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        var pulse = NistPulse.Parse(document.Value);
        NistPulseVerifier.VerifyChain(pulse, ChainIndex);

        var certificate = await Certificates
            .GetAsync(pulse.CertificateId, cancellationToken)
            .ConfigureAwait(false);
        pulse.Verify(certificate);
        return pulse;
    }

    /// <summary>
    /// Gets the verified pulse one index before <paramref name="pulse"/>: the last verified pulse when it is that
    /// pulse, otherwise a fetched one.
    /// </summary>
    /// <param name="pulse">The pulse whose predecessor is needed.</param>
    /// <param name="cancellationToken">The token that cancels the requests.</param>
    /// <returns>The verified predecessor.</returns>
    /// <exception cref="NistFormatException">The API does not serve the predecessor or it does not verify.</exception>
    private async ValueTask<NistPulse> GetPredecessorAsync(
        NistPulse pulse,
        CancellationToken cancellationToken
    )
    {
        var index = pulse.PulseIndex - 1;
        if (
            Volatile.Read(ref _lastVerified) is { } last
            && last.ChainIndex == pulse.ChainIndex
            && last.PulseIndex == index
        )
        {
            return last;
        }

        var selector = index.ToString(CultureInfo.InvariantCulture);
        return await GetVerifiedAsync(selector, cancellationToken).ConfigureAwait(false)
            ?? throw new NistFormatException($"NIST does not serve pulse {index}.");
    }
}
