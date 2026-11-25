// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Tests;

public class NistBeaconOptionsValidatorTests
{
    private static readonly string ValidId = new('a', 128);

    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        Assert.True(
            new NistBeaconOptionsValidator().Validate(null, new NistBeaconOptions()).Succeeded
        );
    }

    [Fact]
    public void Validate_WithDefaultCertificateIds_Succeeds()
    {
        var options = new NistBeaconOptions { CertificateIds = [.. NistDefaults.CertificateIds] };

        Assert.True(new NistBeaconOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("http://beacon.nist.gov/beacon/2.0/")]
    [InlineData("https://beacon.nist.gov/beacon/2.0")]
    [InlineData("https://beacon.nist.gov/beacon/2.0/?x=1")]
    [InlineData("https://beacon.nist.gov/beacon/2.0/#x")]
    public void Validate_WithUnsafeBaseAddress_Fails(string address)
    {
        var options = new NistBeaconOptions { BaseAddress = new Uri(address) };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:BaseAddress",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithRelativeBaseAddress_Fails()
    {
        var options = new NistBeaconOptions { BaseAddress = new Uri("beacon/", UriKind.Relative) };

        Assert.True(new NistBeaconOptionsValidator().Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithChainIndexBelowOne_Fails(long chainIndex)
    {
        var options = new NistBeaconOptions { ChainIndex = chainIndex };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:ChainIndex must be at least 1.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithShortCertificateId_Fails()
    {
        var options = new NistBeaconOptions { CertificateIds = [ValidId, ValidId[..127]] };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:CertificateIds:1 is not a certificate identifier",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithNonHexCertificateId_Fails()
    {
        var options = new NistBeaconOptions { CertificateIds = ['g' + ValidId[1..]] };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:CertificateIds:0 is not a certificate identifier",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithNullCertificateId_Fails()
    {
        var options = new NistBeaconOptions { CertificateIds = [null!] };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:CertificateIds:0 is not a certificate identifier",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithRepeatedCertificateIdInTwoCases_Fails()
    {
        var options = new NistBeaconOptions
        {
            CertificateIds = [ValidId, ValidId.ToUpperInvariant()],
        };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            $"RandomnessBeacons:Nist:CertificateIds lists {ValidId.ToUpperInvariant()} more than once.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithTinyResponseLimit_Fails()
    {
        var options = new NistBeaconOptions { MaxResponseBytes = 1023 };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:MaxResponseBytes must be at least 1024.",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithSeveralProblems_ReportsAllOfThem()
    {
        var options = new NistBeaconOptions
        {
            BaseAddress = new Uri("http://beacon.nist.gov/beacon/2.0/"),
            ChainIndex = 0,
            CertificateIds = ["short"],
            MaxResponseBytes = 1,
        };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Equal(4, result.Failures?.Count());
    }

    #endregion
}
