// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Randomness.HealthChecks;

namespace NCode.Randomness;

/// <summary>
/// Registers the randomness beacons' health check.
/// </summary>
public static class RandomnessHealthChecksBuilderExtensions
{
    /// <summary>
    /// The name the health check is registered under.
    /// </summary>
    public const string BeaconsCheckName = "randomness-beacons";

    extension(IHealthChecksBuilder builder)
    {
        /// <summary>
        /// Adds a check that reports <see cref="HealthStatus.Degraded"/> while any beacon is unavailable. Tag it to
        /// keep it out of a liveness probe: a beacon is an external dependency.
        /// </summary>
        /// <param name="tags">Tags to filter the check by, such as <c>"ready"</c>.</param>
        /// <returns>The builder, for chaining.</returns>
        public IHealthChecksBuilder AddRandomnessBeacons(params IEnumerable<string> tags)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddCheck<BeaconHealthCheck>(
                BeaconsCheckName,
                HealthStatus.Degraded,
                tags
            );
        }
    }
}
