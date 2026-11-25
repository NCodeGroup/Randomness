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
        var stages = pulses.Select(pulse => pulse.Payload.Get<IpldString>("stage").Value).ToList();
        if (!AreKnownStages(stages))
        {
            throw new TwineFormatException(
                $"The round's stages are {string.Join(", ", stages)}, which CURBy-Q does not publish."
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

    private static long ReadRoundNumber(List<TwineSignedPulse> pulses)
    {
        var number = pulses[0].Payload.Get<IpldInteger>("round").Value;
        foreach (var pulse in pulses.Skip(1))
        {
            var other = pulse.Payload.Get<IpldInteger>("round").Value;
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

    private static QuantumResult ReadResult(List<TwineSignedPulse> pulses)
    {
        var request = pulses[0];
        var precommit = pulses[1];
        var result = pulses[2];
        var dataHash = ReadHash(precommit.Payload, "dataHash", "precommit");
        var resultDataHash = ReadHash(result.Payload, "dataHash", "randomness pulse");
        if (!string.Equals(dataHash, resultDataHash, StringComparison.Ordinal))
        {
            throw new TwineFormatException(
                "The randomness pulse carries another dataHash than the precommit."
            );
        }

        var paramsHash = ReadHash(result.Payload, "paramsHash", "randomness pulse");

        var seedAlgorithm = precommit.Payload.Get<IpldString>("seedAlgorithm").Value;
        if (seedAlgorithm != "shake256")
        {
            throw new TwineFormatException(
                $"The seed algorithm '{seedAlgorithm}' is not shake256."
            );
        }

        var rule = precommit.Payload.Get<IpldMap>("rule");
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

        var seedLength = precommit.Payload.Get<IpldInteger>("seedLength").Value;
        if (seedLength is < 1 or > MaxSeedBits || seedLength % 8 != 0)
        {
            throw new TwineFormatException(
                $"The seed length {seedLength} is not a multiple of 8 bits from 8 to {MaxSeedBits}."
            );
        }

        var seedChain = precommit.Payload.Get<IpldLink>("seedChain").Cid;

        var outputBits = request
            .Payload.Get<IpldMap>("parameters")
            .Get<IpldInteger>("nBitsOut")
            .Value;
        if (outputBits < MinOutputBits || outputBits % 8 != 0)
        {
            throw new TwineFormatException(
                $"The output length {outputBits} is not a multiple of 8 bits of at least {MinOutputBits}."
            );
        }

        var randomness = result.Payload.Get<IpldBytes>("randomness").Value.ToArray();
        if (randomness.Length != outputBits / 8)
        {
            throw new TwineFormatException(
                $"The randomness has {randomness.Length} bytes, not {outputBits / 8}."
            );
        }

        var timestampText = result.Payload.Get<IpldString>("timestamp").Value;
        var timestamp = TwinePulse.ParseTimestamp(timestampText);
        return new QuantumResult
        {
            Request = request,
            Precommit = precommit,
            ResultPulse = result,
            DataHash = dataHash,
            SeedChain = seedChain,
            SeedLength = (int)seedLength,
            SeedCount = (int)count,
            ParamsHash = paramsHash,
            Randomness = randomness,
            Timestamp = timestamp,
        };
    }

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
}
