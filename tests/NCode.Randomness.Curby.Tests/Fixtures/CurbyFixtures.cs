// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace NCode.Randomness.Curby.Tests.Fixtures;

/// <summary>
/// The saved CURBy documents described in Fixtures/README.md.
/// </summary>
internal static class CurbyFixtures
{
    public const string ChainId = CurbyDefaults.RngChainId;
    public const long LatestRound = 1620423;
    public const string LatestPulseId =
        "bafyriqbtxzd22eybdovubearwfjyzlyowyhxqdkbzyboftamh6rhob2jafopfz4h2nf3eec4nawu2gil2vpgn57qilrlh23piwyozlugpmcvc";
    public const string LatestRandomness =
        "33be47ad13011bab409011b1538caf0eb60f780d41ce02e2cc0c3fa2770749015cf2e787d34bb2105c682d4d190bd55e66f7f042e2b3eb6f45b0ecae867b0551";

    public static DateTimeOffset LatestTimestamp { get; } =
        new(2026, 11, 25, 2, 44, 0, 150, TimeSpan.Zero);

    public static string Chain => Read("chain.json");

    public static string Pulse(long round) => Read($"pulse-{round}.json");

    public static JsonElement Parse(string json) => JsonElement.Parse(json);

    public static string CidOf(string json) =>
        JsonNode.Parse(json)?["cid"]?["/"]?.GetValue<string>()
        ?? throw new InvalidOperationException("No CID.");

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
