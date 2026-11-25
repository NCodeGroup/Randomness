// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand;

/// <summary>
/// The drand quicknet chain's published constants.
/// </summary>
public static class DrandDefaults
{
    /// <summary>
    /// The name the drand beacon is registered under: <c>drand</c>.
    /// </summary>
    public const string BeaconName = "drand";

    /// <summary>
    /// The base address of the drand HTTP API.
    /// </summary>
    public const string BaseAddress = "https://api.drand.sh/";

    /// <summary>
    /// The hash of the quicknet chain, in lowercase hex: its identifier in the HTTP API.
    /// </summary>
    public const string ChainHash =
        "52db9ba70e0cc0f6eaf7803dd07447a1f5477735fd3f661792ba94600c84e971";

    /// <summary>
    /// The quicknet chain's pinned group public key: a compressed G2 point in lowercase hex. Every round is verified
    /// against it, so no server or mirror can substitute another.
    /// </summary>
    public const string PublicKey =
        "83cf0f2896adee7eb8b5f01fcad3912212c437e0073e911fb90022d3e760183c8c4b450b6a0a6c3ac6a5776a2d1064510d1fec758c921cc22b0e17e63aaf4bcb5ed66304de9cf809bd274ca73bab4af5a6e9c76a4bc09e76eae8991ef5ece45a";

    /// <summary>
    /// The time of round 1, in Unix seconds.
    /// </summary>
    public const long GenesisTime = 1692803367;

    /// <summary>
    /// The seconds between rounds.
    /// </summary>
    public const int Period = 3;

    /// <summary>
    /// The name of the <see cref="System.Net.Http.HttpClient"/> the beacon uses, for a host that adds its own handlers,
    /// such as a proxy, with <c>services.AddHttpClient(DrandDefaults.HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "NCode.Randomness.Drand";
}
