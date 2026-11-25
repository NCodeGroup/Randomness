// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace NCode.Randomness;

/// <summary>
/// Adds beacons and other parts to the randomness services that <c>AddRandomness()</c> registers or a
/// <see cref="RandomnessHost"/> builds.
/// </summary>
public interface IRandomnessBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }
}
