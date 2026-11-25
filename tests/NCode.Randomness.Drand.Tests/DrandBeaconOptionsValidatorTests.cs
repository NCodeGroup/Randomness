// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Drand.Tests;

public class DrandBeaconOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        Assert.True(
            new DrandBeaconOptionsValidator().Validate(null, new DrandBeaconOptions()).Succeeded
        );
    }

    [Theory]
    [InlineData("http://api.drand.sh/")]
    [InlineData("https://api.drand.sh/v2")]
    [InlineData("https://api.drand.sh/?x=1")]
    [InlineData("https://api.drand.sh/#x")]
    public void Validate_WithUnsafeBaseAddress_Fails(string address)
    {
        var options = new DrandBeaconOptions { BaseAddress = new Uri(address) };

        var result = new DrandBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Drand:BaseAddress",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithRelativeBaseAddress_Fails()
    {
        var options = new DrandBeaconOptions { BaseAddress = new Uri("beacon/", UriKind.Relative) };

        var result = new DrandBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Drand:BaseAddress",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithTinyResponseLimit_Fails()
    {
        var options = new DrandBeaconOptions { MaxResponseBytes = 1023 };

        var result = new DrandBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Drand:MaxResponseBytes must be at least 1024.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithSeveralProblems_ReportsAllOfThem()
    {
        var options = new DrandBeaconOptions
        {
            BaseAddress = new Uri("http://api.drand.sh/"),
            MaxResponseBytes = 1,
        };

        var result = new DrandBeaconOptionsValidator().Validate(null, options);

        Assert.Equal(2, result.Failures?.Count());
    }

    #endregion
}
