// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;

namespace ReadmeSamples;

// begin-snippet: TokenService
public sealed class TokenService(ISecureRandom random)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string NewRecoveryCode() => random.GetString(Alphabet, 16);

    public int RollDie() => random.NextInteger(1, 7);
}
// end-snippet
