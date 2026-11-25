// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace NCode.Randomness;

/// <summary>
/// Adds beacons and other parts to the services <c>AddRandomness()</c> registered.
/// </summary>
public interface IRandomnessBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }
}
