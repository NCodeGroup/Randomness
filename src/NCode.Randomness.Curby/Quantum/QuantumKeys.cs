// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The payload keys of CURBy-Q pulses.
/// </summary>
internal static class QuantumKeys
{
    /// <summary>
    /// The SHA3-512 hash of the Bell data, in the precommit, the randomness pulse and the Bell response.
    /// </summary>
    public const string DataHash = "dataHash";

    /// <summary>
    /// The DIRNG round number, in every pulse of a round.
    /// </summary>
    public const string Round = "round";

    /// <summary>
    /// The stage of a round that the pulse publishes, in every pulse of a round.
    /// </summary>
    public const string Stage = "stage";
}
