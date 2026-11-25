// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Entropy;

/// <summary>
/// The beacon entropy the secure generator mixes in.
/// </summary>
internal interface IBeaconEntropy
{
    /// <summary>
    /// Gets the entropy mixed in so far. Reading it never blocks.
    /// </summary>
    BeaconEntropySnapshot Current { get; }
}
