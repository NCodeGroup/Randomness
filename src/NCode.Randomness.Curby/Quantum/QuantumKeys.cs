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
    /// The seed of the extractor, in the extractor's parameters.
    /// </summary>
    public const string Seed = "seed";

    /// <summary>
    /// The SHA3-512 hash of the extractor's parameters, in the randomness pulse.
    /// </summary>
    public const string ParamsHash = "paramsHash";

    /// <summary>
    /// The rule that selects the seed pulse, in the precommit.
    /// </summary>
    public const string Rule = "rule";

    /// <summary>
    /// The link to the request pulse, in the Bell response.
    /// </summary>
    public const string Ref = "ref";

    /// <summary>
    /// The DIRNG round number, in every pulse of a round.
    /// </summary>
    public const string Round = "round";

    /// <summary>
    /// The stage of a round that the pulse publishes, in every pulse of a round.
    /// </summary>
    public const string Stage = "stage";
}
