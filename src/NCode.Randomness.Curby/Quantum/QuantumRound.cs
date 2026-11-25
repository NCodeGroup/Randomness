// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The pulses that CURBy-Q published for one round of its DIRNG chain, each verified as a pulse of the chain, in the
/// order <c>request</c>, <c>precommit</c>, then <c>randomness</c> or <c>error</c>. A round that has all three of
/// request, precommit and randomness has a <see cref="Result"/>.
/// </summary>
internal sealed class QuantumRound
{
    private const string RequestStage = "request";
    private const string PrecommitStage = "precommit";
    private const string RandomnessStage = "randomness";
    private const string ErrorStage = "error";
    private const int HashLength = 128;
    private const int MinOutputBits = 256;
    private const int MaxSeedBits = 8 * 1024 * 1024;
    private const int MaxSeedCount = 8;

    /// <summary>
    /// Verifies the pulses of one round against the CURBy-Q chain.
    /// </summary>
    /// <param name="documents">The round's pulses, in the order the API serves them.</param>
    /// <param name="chain">The verified CURBy-Q chain.</param>
    public static QuantumRound Verify(IReadOnlyList<TwineDocument> documents, TwineChain chain)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(chain);
        if (documents.Count is < 1 or > 3)
        {
            throw new TwineFormatException(
                $"A CURBy-Q round has 1 to 3 pulses, not {documents.Count}."
            );
        }

        var pulses = new List<TwineSignedPulse>(documents.Count);
        foreach (var document in documents)
        {
            var pulse = TwineSignedPulse.Verify(document, chain);
            if (pulses.Count > 0)
            {
                pulse.VerifyPrecededBy(pulses[^1]);
            }

            pulses.Add(pulse);
        }

        var number = ReadRoundNumber(pulses);
        var stages = pulses
            .Select(pulse => pulse.Payload.Get<IpldString>(QuantumKeys.Stage).Value)
            .ToList();
        if (!AreKnownStages(stages))
        {
            var stageList = string.Join(", ", stages);
            throw new TwineFormatException(
                $"The round's stages are {stageList}, which CURBy-Q does not publish."
            );
        }

        if (stages[^1] == ErrorStage)
        {
            var reason = pulses[^1].Payload.Get<IpldString>("reason").Value;
            return new QuantumRound(number, reason, null);
        }

        if (pulses.Count < 3)
        {
            return new QuantumRound(number, null, null);
        }

        var result = ReadResult(pulses);
        return new QuantumRound(number, null, result);
    }

    /// <summary>
    /// Reads the round number the pulses share. It throws <see cref="TwineFormatException"/> when the pulses carry
    /// different round numbers or the number is negative.
    /// </summary>
    /// <param name="pulses">The round's pulses.</param>
    /// <returns>The round number.</returns>
    private static long ReadRoundNumber(List<TwineSignedPulse> pulses)
    {
        var number = pulses[0].Payload.Get<IpldInteger>(QuantumKeys.Round).Value;
        foreach (var pulse in pulses.Skip(1))
        {
            var other = pulse.Payload.Get<IpldInteger>(QuantumKeys.Round).Value;
            if (other != number)
            {
                throw new TwineFormatException(
                    $"The pulses belong to rounds {number} and {other}."
                );
            }
        }

        if (number < 0)
        {
            throw new TwineFormatException($"The round number {number} is negative.");
        }

        return number;
    }

    /// <summary>
    /// Returns whether <paramref name="stages"/> is a sequence CURBy-Q publishes: <c>request</c>, then optionally
    /// <c>precommit</c>, then optionally <c>randomness</c> or <c>error</c>; or <c>request</c> then <c>error</c>.
    /// </summary>
    /// <param name="stages">The stage of each pulse, in order.</param>
    private static bool AreKnownStages(List<string> stages) =>
        stages[0] == RequestStage
        && (
            stages.Count == 1
            || (
                stages[1] == PrecommitStage
                && (stages.Count == 2 || stages[2] is RandomnessStage or ErrorStage)
            )
            || (stages[1] == ErrorStage && stages.Count == 2)
        );

    /// <summary>
    /// Reads and checks the fields of a complete round's request, precommit and randomness pulses: the data hashes
    /// agree, the seed algorithm, rule, length and chain are acceptable, and the randomness has the requested length.
    /// It throws <see cref="TwineFormatException"/> when a field fails its check.
    /// </summary>
    /// <param name="pulses">The round's request, precommit and randomness pulses, in that order.</param>
    /// <returns>The round's result.</returns>
    private static QuantumResult ReadResult(List<TwineSignedPulse> pulses)
    {
        var request = pulses[0];
        var precommit = pulses[1];
        var result = pulses[2];
        var dataHash = ReadDataHash(precommit.Payload, result.Payload);
        var paramsHash = ReadHash(result.Payload, QuantumKeys.ParamsHash, "randomness pulse");
        CheckSeedAlgorithm(precommit.Payload);
        var seedCount = ReadSeedCount(precommit.Payload);
        var seedLength = ReadSeedLength(precommit.Payload);
        var seedChain = precommit.Payload.Get<IpldLink>("seedChain").Cid;
        var outputBits = ReadOutputBits(request.Payload);
        var randomness = ReadRandomness(result.Payload, outputBits);
        var timestamp = ReadTimestamp(result.Payload);
        return new QuantumResult
        {
            Request = request,
            Precommit = precommit,
            ResultPulse = result,
            DataHash = dataHash,
            SeedChain = seedChain,
            SeedLength = seedLength,
            SeedCount = seedCount,
            ParamsHash = paramsHash,
            Randomness = randomness,
            Timestamp = timestamp,
        };
    }

    /// <summary>
    /// Reads the data hash the precommit commits to and checks that the randomness pulse carries the same one. It
    /// throws <see cref="TwineFormatException"/> when either is malformed or they differ.
    /// </summary>
    /// <param name="precommit">The payload of the precommit pulse.</param>
    /// <param name="result">The payload of the randomness pulse.</param>
    /// <returns>The data hash, in lowercase hexadecimal.</returns>
    private static string ReadDataHash(IpldMap precommit, IpldMap result)
    {
        var dataHash = ReadHash(precommit, QuantumKeys.DataHash, "precommit");
        var resultDataHash = ReadHash(result, QuantumKeys.DataHash, "randomness pulse");
        if (!string.Equals(dataHash, resultDataHash, StringComparison.Ordinal))
        {
            throw new TwineFormatException(
                "The randomness pulse carries another dataHash than the precommit."
            );
        }

        return dataHash;
    }

    /// <summary>
    /// Checks that the precommit's seed algorithm is <c>shake256</c>. It throws <see cref="TwineFormatException"/>
    /// otherwise.
    /// </summary>
    /// <param name="precommit">The payload of the precommit pulse.</param>
    private static void CheckSeedAlgorithm(IpldMap precommit)
    {
        var seedAlgorithm = precommit.Get<IpldString>("seedAlgorithm").Value;
        if (seedAlgorithm != "shake256")
        {
            throw new TwineFormatException(
                $"The seed algorithm '{seedAlgorithm}' is not shake256."
            );
        }
    }

    /// <summary>
    /// Reads how many seed pulses the precommit's rule counts. It throws <see cref="TwineFormatException"/> unless the
    /// rule counts after <c>self</c> and the count is from 1 to the largest this package accepts.
    /// </summary>
    /// <param name="precommit">The payload of the precommit pulse.</param>
    /// <returns>The number of seed pulses.</returns>
    private static int ReadSeedCount(IpldMap precommit)
    {
        var rule = precommit.Get<IpldMap>(QuantumKeys.Rule);
        var after = rule.Get<IpldString>("after").Value;
        if (after != "self")
        {
            throw new TwineFormatException($"The seed rule counts after '{after}', not 'self'.");
        }

        var count = rule.Get<IpldInteger>("count").Value;
        if (count is < 1 or > MaxSeedCount)
        {
            throw new TwineFormatException(
                $"The seed rule count {count} is not between 1 and {MaxSeedCount}."
            );
        }

        return (int)count;
    }

    /// <summary>
    /// Reads the length in bits of the seed the precommit asks for. It throws <see cref="TwineFormatException"/>
    /// unless the length is a multiple of 8 bits from 8 to the largest this package accepts.
    /// </summary>
    /// <param name="precommit">The payload of the precommit pulse.</param>
    /// <returns>The seed length in bits.</returns>
    private static int ReadSeedLength(IpldMap precommit)
    {
        var seedLength = precommit.Get<IpldInteger>("seedLength").Value;
        if (seedLength is < 1 or > MaxSeedBits || seedLength % 8 != 0)
        {
            throw new TwineFormatException(
                $"The seed length {seedLength} is not a multiple of 8 bits from 8 to {MaxSeedBits}."
            );
        }

        return (int)seedLength;
    }

    /// <summary>
    /// Reads the number of bits the request asks the extractor to output. It throws
    /// <see cref="TwineFormatException"/> unless the number is a multiple of 8 bits of at least the smallest this
    /// package accepts.
    /// </summary>
    /// <param name="request">The payload of the request pulse.</param>
    /// <returns>The output length in bits.</returns>
    private static long ReadOutputBits(IpldMap request)
    {
        var parameters = request.Get<IpldMap>("parameters");
        var outputBits = parameters.Get<IpldInteger>("nBitsOut").Value;
        if (outputBits < MinOutputBits || outputBits % 8 != 0)
        {
            throw new TwineFormatException(
                $"The output length {outputBits} is not a multiple of 8 bits of at least {MinOutputBits}."
            );
        }

        return outputBits;
    }

    /// <summary>
    /// Reads the randomness the randomness pulse publishes. It throws <see cref="TwineFormatException"/> unless it
    /// has the number of bytes the request asked for.
    /// </summary>
    /// <param name="result">The payload of the randomness pulse.</param>
    /// <param name="outputBits">The output length in bits that the request asked for.</param>
    /// <returns>The randomness.</returns>
    private static byte[] ReadRandomness(IpldMap result, long outputBits)
    {
        var randomness = result.Get<IpldBytes>("randomness").Value.ToArray();
        if (randomness.Length != outputBits / 8)
        {
            throw new TwineFormatException(
                $"The randomness has {randomness.Length} bytes, not {outputBits / 8}."
            );
        }

        return randomness;
    }

    /// <summary>
    /// Reads the time the randomness pulse was published.
    /// </summary>
    /// <param name="result">The payload of the randomness pulse.</param>
    /// <returns>The timestamp of the pulse.</returns>
    private static DateTimeOffset ReadTimestamp(IpldMap result)
    {
        var timestampText = result.Get<IpldString>("timestamp").Value;
        return TwinePulse.ParseTimestamp(timestampText);
    }

    /// <summary>
    /// Reads a hash field of a pulse's payload. It throws <see cref="TwineFormatException"/> unless the field is 128
    /// lowercase hexadecimal characters.
    /// </summary>
    /// <param name="payload">The payload of the pulse.</param>
    /// <param name="field">The name of the field.</param>
    /// <param name="pulse">The name of the pulse, for the message.</param>
    /// <returns>The hash, in lowercase hexadecimal.</returns>
    private static string ReadHash(IpldMap payload, string field, string pulse)
    {
        var value = payload.Get<IpldString>(field).Value;
        if (value.Length != HashLength || !value.All(char.IsAsciiHexDigitLower))
        {
            throw new TwineFormatException(
                $"The {pulse} {field} is not {HashLength} lowercase hexadecimal characters."
            );
        }

        return value;
    }

    /// <summary>
    /// Initializes a round.
    /// </summary>
    /// <param name="number">The DIRNG round number.</param>
    /// <param name="errorReason">The reason in the round's <c>error</c> pulse, if it has one.</param>
    /// <param name="result">The round's result, if it has one.</param>
    private QuantumRound(long number, string? errorReason, QuantumResult? result)
    {
        Number = number;
        ErrorReason = errorReason;
        Result = result;
    }

    /// <summary>
    /// Gets the DIRNG round number.
    /// </summary>
    public long Number { get; }

    /// <summary>
    /// Gets the reason CURBy-Q gave in the round's <c>error</c> pulse, or <see langword="null"/> when it has none.
    /// </summary>
    public string? ErrorReason { get; }

    /// <summary>
    /// Gets the round's result, or <see langword="null"/> when it has no randomness pulse.
    /// </summary>
    public QuantumResult? Result { get; }
}
