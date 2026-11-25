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
    public string Name => NistDefaults.BeaconName;

    private NistApi Api { get; } = api;

    private NistCertificateCache Certificates { get; } = certificates;

    private long ChainIndex { get; } = options.Value.ChainIndex;

    /// <summary>
    /// The last pulse verified, so the next index's predecessor, usually this pulse, needs no fetch.
    /// </summary>
    private NistPulse? _lastVerified;

    public ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken) =>
        FetchAsync("last", null, cancellationToken);

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
            pulse.VerifyFollows(predecessor);
            Volatile.Write(ref _lastVerified, pulse);
            var chainText = pulse.ChainIndex.ToString(CultureInfo.InvariantCulture);
            var id = Convert.ToHexString(pulse.Output);
            return new BeaconPulse(
                Name,
                chainText,
                pulse.PulseIndex,
                id,
                pulse.Timestamp,
                pulse.Output
            );
        }
        catch (Exception exception)
            when (NistErrors.ToBeaconException(exception, Name, cancellationToken) is { } translated
            )
        {
            throw translated;
        }
    }

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
        if (pulse.ChainIndex != ChainIndex)
        {
            throw new NistFormatException(
                $"NIST answered with chain {pulse.ChainIndex}, not chain {chain}."
            );
        }

        var certificate = await Certificates
            .GetAsync(pulse.CertificateId, cancellationToken)
            .ConfigureAwait(false);
        pulse.Verify(certificate);
        return pulse;
    }

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
