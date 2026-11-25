// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NCode.Randomness.HealthChecks;

/// <summary>
/// Reports the beacons' health. A beacon outage is <see cref="HealthStatus.Degraded"/>, never unhealthy: random values
/// stay secure from the operating system's entropy, so no instance should be taken out of service for it.
/// </summary>
internal sealed class BeaconHealthCheck(IBeaconStatus status) : IHealthCheck
{
    private IBeaconStatus Status { get; } = status;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (Status.Mode == BeaconMode.Offline)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Beacons are offline by configuration.")
            );
        }

        var beacons = Status.Beacons;
        var data = new Dictionary<string, object>(beacons.Count, StringComparer.Ordinal);
        var unavailable = 0;
        foreach (var beacon in beacons)
        {
            data[beacon.Name] = beacon.Health.ToString();
            if (beacon.Health == BeaconHealth.Unavailable)
            {
                unavailable++;
            }
        }

        var result =
            unavailable == 0
                ? HealthCheckResult.Healthy(
                    $"{beacons.Count} beacon(s) registered, none unavailable.",
                    data
                )
                : HealthCheckResult.Degraded(
                    $"{unavailable} of {beacons.Count} beacon(s) unavailable.",
                    data: data
                );
        return Task.FromResult(result);
    }
}
