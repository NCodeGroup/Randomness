// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Quantum;

/// <summary>
/// The limits the CURBy-Q Bell and seed walks share.
/// </summary>
internal static class QuantumWalk
{
    /// <summary>
    /// The most pulses, 10, a walk visits before it gives up.
    /// </summary>
    internal const int MaxPulses = 10;
}
