// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using NCode.Randomness.Drand.Bls;
using NCode.Randomness.Drand.Rounds;
using Neo.Cryptography.BLS12_381;

namespace NCode.Randomness.Drand.Tests.TestDrand;

/// <summary>
/// A drand chain whose secret key the tests know, so they can sign rounds the quicknet key would never sign.
/// </summary>
internal static class DrandTestChain
{
    public static Scalar SecretKey { get; } = new(0x1234_5678_9abc_def0UL);

    public static DrandChain Chain { get; } =
        new(
            new string('0', 64),
            new G2Affine(G2Affine.Generator * SecretKey),
            DrandDefaults.GenesisTime,
            DrandDefaults.Period
        );

    public static byte[] Sign(long round)
    {
        Span<byte> message = stackalloc byte[SHA256.HashSizeInBytes];
        DrandRound.MessageOf(round, message);
        var hashed = G1HashToCurve.Hash(message, DrandRound.Domain);
        var point = new G1Affine(hashed * SecretKey);
        return point.ToCompressed();
    }

    public static string Json(long round)
    {
        var signature = Sign(round);
        var randomness = SHA256.HashData(signature);
        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"round":{{round}},"randomness":"{{Convert.ToHexStringLower(randomness)}}","signature":"{{Convert.ToHexStringLower(signature)}}"}"""
        );
    }
}
