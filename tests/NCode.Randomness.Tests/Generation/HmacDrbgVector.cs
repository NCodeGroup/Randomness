// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests.Generation;

/// <summary>
/// One NIST CAVP HMAC_DRBG vector, in hexadecimal. An empty reseed entropy means the vector does not reseed.
/// </summary>
public sealed record HmacDrbgVector(
    string Name,
    string Entropy,
    string Nonce,
    string Personalization,
    string ReseedEntropy,
    string ReseedAdditional,
    string Additional1,
    string Additional2,
    string Returned
)
{
    public override string ToString() => Name;
}
