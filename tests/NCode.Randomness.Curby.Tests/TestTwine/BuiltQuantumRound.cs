// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

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
