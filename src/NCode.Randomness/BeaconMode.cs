// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// How the library uses the registered randomness beacons. In every mode, random values are generated locally and
/// never wait on a beacon.
/// </summary>
public enum BeaconMode
{
    /// <summary>
    /// Fetches a pulse from every beacon at start and again every <see cref="RandomnessOptions.RefreshInterval"/>,
    /// mixing each new pulse into the generator.
    /// </summary>
    Continuous = 0,

    /// <summary>
    /// Fetches until each beacon has given one pulse, mixes it in, and then contacts no beacon again: start online,
    /// then run offline.
    /// </summary>
    InitializeOnly = 1,

    /// <summary>
    /// Contacts no beacon, even when beacons are registered: the generator is seeded by the operating system alone, as
    /// suits an air-gapped deployment.
    /// </summary>
    Offline = 2,
}
