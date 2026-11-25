// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Builds a CURBy-Q round, its Bell response and its seed pulses on three chains signed with keys of its own, so a
/// test can break one rule.
/// </summary>
internal sealed class QuantumRoundBuilder : IDisposable
{
    private static DateTimeOffset Start { get; } = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static byte[] DefaultDataDigest { get; } = Sha3.HashData512("bell data"u8);

    /// <summary>
    /// Formats the timestamp of the pulse at position <paramref name="index"/> of the round, one second after the
    /// pulse before it.
    /// </summary>
    private static string Timestamp(int index)
    {
        var timestamp = Start.AddSeconds(index);
        return TwineBuilder.FormatTimestamp(timestamp);
    }

    public TwineBuilder Quantum { get; } = new(specification: "twine/1.0.x/diqrng/1.0.x");

    public TwineBuilder Bell { get; } = new(specification: "twine/1.0.x/bell/1.0.x");

    public TwineBuilder Seed { get; } = new(specification: "twine/1.0.x/mirror/1.0.x");

    /// <summary>
    /// Gets the round number the pulses carry.
    /// </summary>
    public long Round { get; init; } = 7;

    /// <summary>
    /// Gets the seed length in bits.
    /// </summary>
    public int SeedLength { get; init; } = 2048;

    /// <summary>
    /// Gets the <c>dataHash</c> of the precommit and the randomness pulse.
    /// </summary>
    public string DataHash { get; init; } = Convert.ToHexStringLower(DefaultDataDigest);

    /// <summary>
    /// Gets the <c>dataHash</c> of the Bell response, or null for <see cref="DataHash"/>.
    /// </summary>
    public string? BellResponseDataHash { get; init; }

    /// <summary>
    /// Gets how many status pulses follow the Bell response.
    /// </summary>
    public int BellPulsesAfterResponse { get; init; } = 1;

    /// <summary>
    /// Gets the precommit's <c>rule.count</c>, the number of seed pulses between the first that mixes in the
    /// precommit and the one the seed expands from.
    /// </summary>
    public int SeedCount { get; init; } = 1;

    /// <summary>
    /// Gets whether the Bell chain is built from status pulses only, so no pulse answers the request.
    /// </summary>
    public bool OmitBellResponse { get; init; }

    /// <summary>
    /// Gets whether the seed chain starts with the first pulse that mixes in the precommit, and the precommit mixes
    /// in no seed pulse.
    /// </summary>
    public bool OmitSeedPulseZero { get; init; }

    /// <summary>
    /// Gets whether the precommit mixes in a pulse of the Bell chain; false leaves the mixin out.
    /// </summary>
    public bool PrecommitMixesBell { get; init; } = true;

    /// <summary>
    /// Gets whether the randomness pulse mixes in a pulse of the seed chain; false leaves the mixin out.
    /// </summary>
    public bool ResultMixesSeed { get; init; } = true;

    /// <summary>
    /// Gets how many seed pulses after pulse 0 mix in the precommit, or null for <see cref="SeedCount"/> + 1. The
    /// seed expands from the pulse <see cref="SeedCount"/> after the first of them either way.
    /// </summary>
    public int? SeedPulsesMixingPrecommit { get; init; }

    /// <summary>
    /// Gets the edit applied to the request pulse's payload.
    /// </summary>
    public Func<IpldMap, IpldMap> EditRequest { get; init; } = static payload => payload;

    /// <summary>
    /// Gets the edit applied to the precommit pulse's payload.
    /// </summary>
    public Func<IpldMap, IpldMap> EditPrecommit { get; init; } = static payload => payload;

    /// <summary>
    /// Gets the edit applied to the randomness pulse's payload.
    /// </summary>
    public Func<IpldMap, IpldMap> EditResult { get; init; } = static payload => payload;

    /// <summary>
    /// Gets the function that writes the parameters document from the expanded seed.
    /// </summary>
    public Func<byte[], string> Parameters { get; init; } =
        static seed => $$"""{"nBitsOut":512,"seed":"{{Convert.ToBase64String(seed)}}"}""";

    public BuiltQuantumRound Build()
    {
        var clock = 0;

        var request = BuildRequest(clock);
        clock++;

        var bellPulses = new List<(string Json, Cid Cid)>();
        if (!OmitBellResponse)
        {
            var responseDataHash = Ipld.Text(BellResponseDataHash ?? DataHash);
            var requestLink = Ipld.Link(request.Cid);
            var responseTime = Timestamp(clock++);
            var responseTimestamp = Ipld.Text(responseTime);
            var responseType = Ipld.Text("result");
            var responsePayload = Ipld.Map(
                ("dataHash", responseDataHash),
                ("ref", requestLink),
                ("timestamp", responseTimestamp),
                ("type", responseType)
            );
            var responseContent = Bell.Content(0, null, responsePayload);
            var bellResponse = Bell.Seal(responseContent);
            bellPulses.Add(bellResponse);
        }

        while (bellPulses.Count <= BellPulsesAfterResponse)
        {
            var statusCode = Ipld.Integer(0);
            var statusTime = Timestamp(clock++);
            var statusTimestamp = Ipld.Text(statusTime);
            var statusType = Ipld.Text("status");
            var statusPayload = Ipld.Map(
                ("status", statusCode),
                ("timestamp", statusTimestamp),
                ("type", statusType)
            );
            Cid? previousBell = bellPulses.Count == 0 ? null : bellPulses[^1].Cid;
            var statusContent = Bell.Content(bellPulses.Count, previousBell, statusPayload);
            var status = Bell.Seal(statusContent);
            bellPulses.Add(status);
        }

        var seedPulses = new List<(string Json, Cid Cid)>();
        if (!OmitSeedPulseZero)
        {
            var firstSeedRound = Ipld.Integer(100);
            var firstSeedTime = Timestamp(clock++);
            var firstSeedTimestamp = Ipld.Text(firstSeedTime);
            var firstSeedPayload = Ipld.Map(
                ("round", firstSeedRound),
                ("timestamp", firstSeedTimestamp)
            );
            var firstSeedContent = Seed.Content(0, null, firstSeedPayload);
            var firstSeed = Seed.Seal(firstSeedContent);
            seedPulses.Add(firstSeed);
        }

        var precommitDataHash = Ipld.Text(DataHash);
        var precommitRound = Ipld.Integer(Round);
        var ruleAfter = Ipld.Text("self");
        var ruleCount = Ipld.Integer(SeedCount);
        var rule = Ipld.Map(("after", ruleAfter), ("count", ruleCount));
        var seedAlgorithm = Ipld.Text("shake256");
        var seedChain = Ipld.Link(Seed.ChainCid);
        var seedLength = Ipld.Integer(SeedLength);
        var precommitStage = Ipld.Text("precommit");
        var precommitTime = Timestamp(clock++);
        var precommitTimestamp = Ipld.Text(precommitTime);
        var precommitMap = Ipld.Map(
            ("dataHash", precommitDataHash),
            ("round", precommitRound),
            ("rule", rule),
            ("seedAlgorithm", seedAlgorithm),
            ("seedChain", seedChain),
            ("seedLength", seedLength),
            ("stage", precommitStage),
            ("timestamp", precommitTimestamp)
        );
        var precommitPayload = EditPrecommit(precommitMap);
        var precommitMixins = new List<(Cid Chain, Cid Value)>();
        if (PrecommitMixesBell)
        {
            precommitMixins.Add((Bell.ChainCid, bellPulses[^1].Cid));
        }

        if (!OmitSeedPulseZero)
        {
            precommitMixins.Add((Seed.ChainCid, seedPulses[0].Cid));
        }
        var precommitContent = Quantum.Content(
            1,
            request.Cid,
            precommitPayload,
            [.. precommitMixins]
        );
        var precommit = Quantum.Seal(precommitContent);

        var mixingCount = SeedPulsesMixingPrecommit ?? SeedCount + 1;
        var firstMixing = seedPulses.Count;
        for (var i = firstMixing; i < firstMixing + mixingCount; i++)
        {
            var seedPulseRound = Ipld.Integer(100 + i);
            var seedPulseTime = Timestamp(clock++);
            var seedPulseTimestamp = Ipld.Text(seedPulseTime);
            var seedPulsePayload = Ipld.Map(
                ("round", seedPulseRound),
                ("timestamp", seedPulseTimestamp)
            );
            Cid? previousSeed = seedPulses.Count == 0 ? null : seedPulses[^1].Cid;
            var seedPulseContent = Seed.Content(
                i,
                previousSeed,
                seedPulsePayload,
                (Quantum.ChainCid, precommit.Cid)
            );
            var seedPulse = Seed.Seal(seedPulseContent);
            seedPulses.Add(seedPulse);
        }

        var seed = new byte[SeedLength / 8];
        Sha3.Shake256(seedPulses[firstMixing + SeedCount].Cid.Digest, seed);
        var parameters = Parameters(seed);
        var randomness = RandomNumberGenerator.GetBytes(64);
        var resultMixins = new List<(Cid Chain, Cid Value)>();
        if (ResultMixesSeed)
        {
            resultMixins.Add((Seed.ChainCid, seedPulses[^1].Cid));
        }

        var parametersBytes = Encoding.UTF8.GetBytes(parameters);
        var parametersDigest = Sha3.HashData512(parametersBytes);
        var paramsHash = Convert.ToHexStringLower(parametersDigest);
        var resultDataHash = Ipld.Text(DataHash);
        var paramsHashText = Ipld.Text(paramsHash);
        var randomnessBytes = Ipld.Bytes(randomness);
        var resultRound = Ipld.Integer(Round);
        var resultStage = Ipld.Text("randomness");
        var resultTime = Timestamp(clock++);
        var resultTimestamp = Ipld.Text(resultTime);
        var resultMap = Ipld.Map(
            ("dataHash", resultDataHash),
            ("paramsHash", paramsHashText),
            ("randomness", randomnessBytes),
            ("round", resultRound),
            ("stage", resultStage),
            ("timestamp", resultTimestamp)
        );
        var resultPayload = EditResult(resultMap);
        var resultContent = Quantum.Content(2, precommit.Cid, resultPayload, [.. resultMixins]);
        var result = Quantum.Seal(resultContent);
        var roundJson = $"[{request.Json},{precommit.Json},{result.Json}]";

        return new BuiltQuantumRound(
            request,
            precommit,
            result,
            bellPulses,
            seedPulses,
            parameters,
            randomness,
            roundJson
        );
    }

    /// <summary>
    /// Builds the JSON of a round that ended with an <c>error</c> pulse.
    /// </summary>
    public string ErrorRoundJson(string reason)
    {
        var clock = 0;
        var request = BuildRequest(clock);
        clock++;
        var errorReason = Ipld.Text(reason);
        var errorRound = Ipld.Integer(Round);
        var errorStage = Ipld.Text("error");
        var errorTime = Timestamp(clock++);
        var errorTimestamp = Ipld.Text(errorTime);
        var errorPayload = Ipld.Map(
            ("reason", errorReason),
            ("round", errorRound),
            ("stage", errorStage),
            ("timestamp", errorTimestamp)
        );
        var errorContent = Quantum.Content(1, request.Cid, errorPayload);
        var error = Quantum.Seal(errorContent);
        return $"[{request.Json},{error.Json}]";
    }

    public void Dispose()
    {
        Quantum.Dispose();
        Bell.Dispose();
        Seed.Dispose();
    }

    /// <summary>
    /// Builds the request pulse at position <paramref name="index"/> of the round, which sets its timestamp.
    /// </summary>
    private (string Json, Cid Cid) BuildRequest(int index)
    {
        var beta = Ipld.Float(0.5);
        var nBitsOut = Ipld.Integer(512);
        var parameters = Ipld.Map(("beta", beta), ("nBitsOut", nBitsOut));
        var round = Ipld.Integer(Round);
        var stage = Ipld.Text("request");
        var time = Timestamp(index);
        var timestamp = Ipld.Text(time);
        var map = Ipld.Map(
            ("parameters", parameters),
            ("round", round),
            ("stage", stage),
            ("timestamp", timestamp)
        );
        var payload = EditRequest(map);
        var content = Quantum.Content(0, null, payload);
        return Quantum.Seal(content);
    }
}
