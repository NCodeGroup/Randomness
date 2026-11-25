// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand.Tests.Fixtures;

/// <summary>
/// The saved drand documents described in Fixtures/README.md.
/// </summary>
internal static class DrandFixtures
{
    public const long LatestRound = 34271222;
    public const string LatestRandomness =
        "a971695a0eb21ba2e817d541aea68d469757f63ecc8aaf72be1c1a972f84db53";
    public const string FirstRandomness =
        "fe290beca10872ef2fb164d2aa4442de4566183ec51c56ff3cd603d930e54fdd";

    public static DateTimeOffset LatestTimestamp { get; } =
        new(2026, 11, 25, 14, 30, 30, TimeSpan.Zero);

    public static string Info => File.ReadAllText(Path("info.json"));

    public static string Round(long round) => File.ReadAllText(Path($"public-{round}.json"));

    private static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
