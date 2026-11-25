// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using NCode.Randomness.HealthChecks;

namespace NCode.Randomness.Tests.HealthChecks;

public class BeaconHealthCheckTests : UnitTestBase
{
    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WhenOffline_IsHealthy()
    {
        var status = CreateStatus(BeaconMode.Offline);

        var result = await new BeaconHealthCheck(status.Object).CheckHealthAsync(
            new HealthCheckContext()
        );

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenEveryBeaconIsHealthyOrPending_IsHealthy()
    {
        var status = CreateStatus(
            BeaconMode.Continuous,
            State("a", BeaconHealth.Healthy),
            State("b", BeaconHealth.Pending)
        );

        var result = await new BeaconHealthCheck(status.Object).CheckHealthAsync(
            new HealthCheckContext()
        );

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Pending", result.Data["b"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenABeaconIsUnavailable_IsDegradedNotUnhealthy()
    {
        var status = CreateStatus(
            BeaconMode.Continuous,
            State("a", BeaconHealth.Healthy),
            State("b", BeaconHealth.Unavailable)
        );

        var result = await new BeaconHealthCheck(status.Object).CheckHealthAsync(
            new HealthCheckContext()
        );

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("1 of 2 beacon(s) unavailable.", result.Description);
    }

    #endregion

    private static BeaconState State(string name, BeaconHealth health) =>
        new() { Name = name, Health = health };

    private Mock<IBeaconStatus> CreateStatus(BeaconMode mode, params BeaconState[] beacons)
    {
        var status = CreateStrictMock<IBeaconStatus>();
        status.Setup(x => x.Mode).Returns(mode).Verifiable();
        if (mode != BeaconMode.Offline)
        {
            status.Setup(x => x.Beacons).Returns(beacons).Verifiable();
        }

        return status;
    }
}
