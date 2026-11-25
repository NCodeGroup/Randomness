// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The three verified pulses of a complete CURBy-Q round and what they commit to: the Bell data hash, the seed that
/// the Trevisan extractor takes, the hash of the extractor's parameters and the extracted randomness.
/// </summary>
/// <param name="round">The DIRNG round number.</param>
/// <param name="request">The request pulse, which asks for the Bell experiment.</param>
/// <param name="precommit">The precommit pulse, which commits to the Bell data and names the seed.</param>
/// <param name="resultPulse">The randomness pulse.</param>
/// <param name="dataHash">The SHA3-512 hash of the Bell data, in lowercase hexadecimal.</param>
/// <param name="seedChain">The CID of the chain the seed pulse belongs to.</param>
/// <param name="seedLength">The length of the seed in bits.</param>
/// <param name="seedCount">The number of pulses after the first that mixes in the precommit that holds the seed.</param>
/// <param name="paramsHash">The SHA3-512 hash of the extractor's parameters, in lowercase hexadecimal.</param>
/// <param name="randomness">The extracted randomness.</param>
/// <param name="timestamp">The time of the randomness pulse.</param>
internal sealed class QuantumResult(
    long round,
    TwineSignedPulse request,
    TwineSignedPulse precommit,
    TwineSignedPulse resultPulse,
    string dataHash,
    Cid seedChain,
    int seedLength,
    int seedCount,
    string paramsHash,
    byte[] randomness,
    DateTimeOffset timestamp
)
{
    public long Round { get; } = round;

    public TwineSignedPulse Request { get; } = request;

    public TwineSignedPulse Precommit { get; } = precommit;

    public TwineSignedPulse ResultPulse { get; } = resultPulse;

    public string DataHash { get; } = dataHash;

    public Cid SeedChain { get; } = seedChain;

    /// <summary>
    /// Gets the length of the seed in bits.
    /// </summary>
    public int SeedLength { get; } = seedLength;

    public int SeedCount { get; } = seedCount;

    public string ParamsHash { get; } = paramsHash;

    public byte[] Randomness { get; } = randomness;

    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// Returns whether <paramref name="pulse"/>, a pulse of the Bell chain, is the response to this round's request:
    /// its payload links the request in <c>ref</c> and holds a <c>dataHash</c>, which must be the precommit's.
    /// </summary>
    /// <param name="pulse">The Bell pulse to examine.</param>
    public bool IsBellResponse(TwineSignedPulse pulse)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        if (
            !pulse.Payload.TryGet<IpldLink>("ref", out var reference)
            || !reference.Cid.Equals(Request.Cid)
            || !pulse.Payload.TryGet<IpldString>("dataHash", out var hash)
        )
        {
            return false;
        }

        if (!string.Equals(hash.Value, DataHash, StringComparison.Ordinal))
        {
            throw new TwineFormatException(
                $"The Bell response {pulse.Cid} carries another data hash than the precommit."
            );
        }

        return true;
    }
}
