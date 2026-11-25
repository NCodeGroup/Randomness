// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The three verified pulses of a complete CURBy-Q round and what they commit to: the Bell data hash, the seed that
/// the Trevisan extractor takes, the hash of the extractor's parameters and the extracted randomness.
/// </summary>
internal sealed class QuantumResult
{
    /// <summary>
    /// Gets the request pulse, which asks for the Bell experiment.
    /// </summary>
    public required TwineSignedPulse Request { get; init; }

    /// <summary>
    /// Gets the precommit pulse, which commits to the Bell data and names the seed.
    /// </summary>
    public required TwineSignedPulse Precommit { get; init; }

    /// <summary>
    /// Gets the randomness pulse.
    /// </summary>
    public required TwineSignedPulse ResultPulse { get; init; }

    /// <summary>
    /// Gets the SHA3-512 hash of the Bell data, in lowercase hexadecimal.
    /// </summary>
    public required string DataHash { get; init; }

    /// <summary>
    /// Gets the CID of the chain the seed pulse belongs to.
    /// </summary>
    public required Cid SeedChain { get; init; }

    /// <summary>
    /// Gets the length of the seed in bits.
    /// </summary>
    public required int SeedLength { get; init; }

    /// <summary>
    /// Gets the number of pulses after the first that mixes in the precommit that holds the seed.
    /// </summary>
    public required int SeedCount { get; init; }

    /// <summary>
    /// Gets the SHA3-512 hash of the extractor's parameters, in lowercase hexadecimal.
    /// </summary>
    public required string ParamsHash { get; init; }

    /// <summary>
    /// Gets the extracted randomness.
    /// </summary>
    public required byte[] Randomness { get; init; }

    /// <summary>
    /// Gets the time of the randomness pulse.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

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

    /// <summary>
    /// Returns whether <paramref name="pulse"/>, a pulse of the seed chain, mixes in the precommit.
    /// </summary>
    /// <param name="pulse">The seed pulse to examine.</param>
    public bool MixesPrecommit(TwineSignedPulse pulse)
    {
        ArgumentNullException.ThrowIfNull(pulse);
        var mixin = pulse.MixinOf(Precommit.Chain);
        return Precommit.Cid.Equals(mixin);
    }

    /// <summary>
    /// Selects the seed pulse: the pulse <see cref="SeedCount"/> after the first that mixes in the precommit.
    /// </summary>
    /// <param name="mixing">The seed pulses that mix in the precommit up to the result, newest first.</param>
    public TwineSignedPulse SelectSeed(IReadOnlyList<TwineSignedPulse> mixing)
    {
        ArgumentNullException.ThrowIfNull(mixing);
        if (mixing.Count <= SeedCount)
        {
            throw new TwineFormatException(
                $"Only {mixing.Count} seed pulse(s) mix in the precommit up to the result; the seed is the pulse {SeedCount} after the first."
            );
        }

        return mixing[mixing.Count - 1 - SeedCount];
    }

    /// <summary>
    /// Checks that the extractor's parameters are the ones the randomness pulse commits to and that their seed
    /// expands from <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">The seed pulse.</param>
    /// <param name="parameters">The extractor's parameters, a JSON document.</param>
    public void VerifySeed(TwineSignedPulse seed, ReadOnlySpan<byte> parameters)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var digest = Sha3.HashData512(parameters);
        var hash = Convert.ToHexStringLower(digest);
        if (!string.Equals(hash, ParamsHash, StringComparison.Ordinal))
        {
            throw new TwineFormatException("The round's parameters do not hash to its paramsHash.");
        }

        var encoded = ReadSeed(parameters);
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new TwineFormatException("The round's seed is not canonical base64.");
        }

        var canonical = Convert.ToBase64String(expected);
        if (!string.Equals(canonical, encoded, StringComparison.Ordinal))
        {
            throw new TwineFormatException("The round's seed is not canonical base64.");
        }

        var actual = new byte[SeedLength / 8];
        Sha3.Shake256(seed.Cid.Digest, actual);
        if (!actual.AsSpan().SequenceEqual(expected))
        {
            throw new TwineFormatException(
                $"The round's seed does not expand from seed pulse {seed.Index} ({seed.Cid})."
            );
        }
    }

    private static string ReadSeed(ReadOnlySpan<byte> parameters)
    {
        JsonElement document;
        try
        {
            document = JsonElement.Parse(parameters);
        }
        catch (JsonException exception)
        {
            throw new TwineFormatException(
                $"The round's parameters are not JSON: {exception.Message}"
            );
        }

        if (document.ValueKind != JsonValueKind.Object)
        {
            throw new TwineFormatException("The round's parameters are not a JSON object.");
        }

        JsonElement? seed = null;
        foreach (var property in document.EnumerateObject())
        {
            if (property.NameEquals("seed"u8))
            {
                if (seed is not null)
                {
                    throw new TwineFormatException("The round's parameters have 'seed' twice.");
                }

                seed = property.Value;
            }
        }

        if (seed is null)
        {
            throw new TwineFormatException("The round's parameters have no 'seed'.");
        }

        if (seed.Value.ValueKind != JsonValueKind.String)
        {
            throw new TwineFormatException("The round's 'seed' is not a string.");
        }

        return seed.Value.GetString() ?? string.Empty;
    }
}
