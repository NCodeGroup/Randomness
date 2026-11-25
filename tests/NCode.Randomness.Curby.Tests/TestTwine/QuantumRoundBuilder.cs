// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Builds a CURBy-Q round, its Bell response and its seed pulses on three chains signed with keys of its own, so a
/// test can break one rule.
/// </summary>
internal sealed class QuantumRoundBuilder : IDisposable
{
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TwineBuilder Quantum { get; } = new(specification: "twine/1.0.x/diqrng/1.0.x");

    public TwineBuilder Bell { get; } = new(specification: "twine/1.0.x/bell/1.0.x");

    public TwineBuilder Seed { get; } = new(specification: "twine/1.0.x/mirror/1.0.x");

    public long Round { get; init; } = 7;

    /// <summary>
    /// The seed length in bits.
    /// </summary>
    public int SeedLength { get; init; } = 2048;

    public string DataHash { get; init; } =
        Convert.ToHexStringLower(Sha3.HashData512("bell data"u8));

    /// <summary>
    /// The <c>dataHash</c> of the Bell response, or null for <see cref="DataHash"/>.
    /// </summary>
    public string? BellResponseDataHash { get; init; }

    public int BellPulsesAfterResponse { get; init; } = 1;

    public int SeedCount { get; init; } = 1;

    public Func<IpldMap, IpldMap> EditRequest { get; init; } = static payload => payload;

    public Func<IpldMap, IpldMap> EditPrecommit { get; init; } = static payload => payload;

    public Func<IpldMap, IpldMap> EditResult { get; init; } = static payload => payload;

    /// <summary>
    /// Writes the parameters document from the expanded seed.
    /// </summary>
    public Func<byte[], string> Parameters { get; init; } =
        static seed => $$"""{"nBitsOut":512,"seed":"{{Convert.ToBase64String(seed)}}"}""";

    public BuiltQuantumRound Build()
    {
        var clock = 0;

        var request = BuildRequest(ref clock);

        var bellPulses = new List<(string Json, Cid Cid)>();
        var bellResponse = Bell.Seal(
            Bell.Content(
                0,
                null,
                Ipld.Map(
                    ("dataHash", Ipld.Text(BellResponseDataHash ?? DataHash)),
                    ("ref", Ipld.Link(request.Cid)),
                    ("timestamp", Ipld.Text(Timestamp(ref clock))),
                    ("type", Ipld.Text("result"))
                )
            )
        );
        bellPulses.Add(bellResponse);
        for (var i = 1; i <= BellPulsesAfterResponse; i++)
        {
            var status = Bell.Seal(
                Bell.Content(
                    i,
                    bellPulses[^1].Cid,
                    Ipld.Map(
                        ("status", Ipld.Integer(0)),
                        ("timestamp", Ipld.Text(Timestamp(ref clock))),
                        ("type", Ipld.Text("status"))
                    )
                )
            );
            bellPulses.Add(status);
        }

        var seedPulses = new List<(string Json, Cid Cid)>();
        seedPulses.Add(
            Seed.Seal(
                Seed.Content(
                    0,
                    null,
                    Ipld.Map(
                        ("round", Ipld.Integer(100)),
                        ("timestamp", Ipld.Text(Timestamp(ref clock)))
                    )
                )
            )
        );

        var precommitPayload = EditPrecommit(
            Ipld.Map(
                ("dataHash", Ipld.Text(DataHash)),
                ("round", Ipld.Integer(Round)),
                (
                    "rule",
                    Ipld.Map(("after", Ipld.Text("self")), ("count", Ipld.Integer(SeedCount)))
                ),
                ("seedAlgorithm", Ipld.Text("shake256")),
                ("seedChain", Ipld.Link(Seed.ChainCid)),
                ("seedLength", Ipld.Integer(SeedLength)),
                ("stage", Ipld.Text("precommit")),
                ("timestamp", Ipld.Text(Timestamp(ref clock)))
            )
        );
        var precommit = Quantum.Seal(
            Quantum.Content(
                1,
                request.Cid,
                precommitPayload,
                (Bell.ChainCid, bellPulses[^1].Cid),
                (Seed.ChainCid, seedPulses[0].Cid)
            )
        );

        for (var i = 1; i <= SeedCount + 1; i++)
        {
            var seedPulse = Seed.Seal(
                Seed.Content(
                    i,
                    seedPulses[^1].Cid,
                    Ipld.Map(
                        ("round", Ipld.Integer(100 + i)),
                        ("timestamp", Ipld.Text(Timestamp(ref clock)))
                    ),
                    (Quantum.ChainCid, precommit.Cid)
                )
            );
            seedPulses.Add(seedPulse);
        }

        var seed = new byte[SeedLength / 8];
        Sha3.Shake256(seedPulses[^1].Cid.Digest, seed);
        var parameters = Parameters(seed);
        var randomness = RandomNumberGenerator.GetBytes(64);

        var result = Quantum.Seal(
            Quantum.Content(
                2,
                precommit.Cid,
                EditResult(
                    Ipld.Map(
                        ("dataHash", Ipld.Text(DataHash)),
                        (
                            "paramsHash",
                            Ipld.Text(
                                Convert.ToHexStringLower(
                                    Sha3.HashData512(System.Text.Encoding.UTF8.GetBytes(parameters))
                                )
                            )
                        ),
                        ("randomness", Ipld.Bytes(randomness)),
                        ("round", Ipld.Integer(Round)),
                        ("stage", Ipld.Text("randomness")),
                        ("timestamp", Ipld.Text(Timestamp(ref clock)))
                    )
                ),
                (Seed.ChainCid, seedPulses[^1].Cid)
            )
        );

        return new BuiltQuantumRound(
            request,
            precommit,
            result,
            bellPulses,
            seedPulses,
            parameters,
            randomness,
            $"[{request.Json},{precommit.Json},{result.Json}]"
        );
    }

    /// <summary>
    /// Builds the JSON of a round that ended with an <c>error</c> pulse.
    /// </summary>
    public string ErrorRoundJson(string reason)
    {
        var clock = 0;
        var request = BuildRequest(ref clock);
        var error = Quantum.Seal(
            Quantum.Content(
                1,
                request.Cid,
                Ipld.Map(
                    ("reason", Ipld.Text(reason)),
                    ("round", Ipld.Integer(Round)),
                    ("stage", Ipld.Text("error")),
                    ("timestamp", Ipld.Text(Timestamp(ref clock)))
                )
            )
        );
        return $"[{request.Json},{error.Json}]";
    }

    public void Dispose()
    {
        Quantum.Dispose();
        Bell.Dispose();
        Seed.Dispose();
    }

    private (string Json, Cid Cid) BuildRequest(ref int clock) =>
        Quantum.Seal(
            Quantum.Content(
                0,
                null,
                EditRequest(
                    Ipld.Map(
                        (
                            "parameters",
                            Ipld.Map(("beta", Ipld.Float(0.5)), ("nBitsOut", Ipld.Integer(512)))
                        ),
                        ("round", Ipld.Integer(Round)),
                        ("stage", Ipld.Text("request")),
                        ("timestamp", Ipld.Text(Timestamp(ref clock)))
                    )
                )
            )
        );

    private static string Timestamp(ref int clock) =>
        Start
            .AddSeconds(clock++)
            .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

/// <summary>
/// A CURBy-Q round that <see cref="QuantumRoundBuilder"/> built.
/// </summary>
internal sealed record BuiltQuantumRound(
    (string Json, Cid Cid) Request,
    (string Json, Cid Cid) Precommit,
    (string Json, Cid Cid) Result,
    IReadOnlyList<(string Json, Cid Cid)> BellPulses,
    IReadOnlyList<(string Json, Cid Cid)> SeedPulses,
    string Parameters,
    byte[] Randomness,
    string RoundJson
);
