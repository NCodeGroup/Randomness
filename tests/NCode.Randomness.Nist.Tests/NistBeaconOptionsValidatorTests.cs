// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Tests;

public class NistBeaconOptionsValidatorTests
{
    private static string ValidId { get; } = new('a', 128);

    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        var result = new NistBeaconOptionsValidator().Validate(null, new NistBeaconOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithDefaultCertificateIds_Succeeds()
    {
        var options = new NistBeaconOptions { CertificateIds = [.. NistDefaults.CertificateIds] };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithSmallestResponseLimit_Succeeds()
    {
        var options = new NistBeaconOptions { MaxResponseBytes = 1024 };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithChainIndexOne_Succeeds()
    {
        var options = new NistBeaconOptions { ChainIndex = 1 };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("http://beacon.nist.gov/beacon/2.0/")]
    [InlineData("https://beacon.nist.gov/beacon/2.0")]
    [InlineData("https://beacon.nist.gov/beacon/2.0/?x=1")]
    [InlineData("https://beacon.nist.gov/beacon/2.0/#x")]
    [InlineData("https://user:secret@beacon.nist.gov/beacon/2.0/")]
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

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
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
    public void Validate_WithNullCertificateIds_Fails()
    {
        var options = new NistBeaconOptions { CertificateIds = null! };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Nist:CertificateIds must not be null; leave it empty to trust the default certificates.",
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
    public void Validate_WithShortestCertificateRetryDelay_Succeeds()
    {
        var options = new NistBeaconOptions { CertificateRetryDelay = TimeSpan.FromSeconds(1) };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(999, "00:00:00.9990000")]
    [InlineData(86_400_001, "1.00:00:00.0010000")]
    public void Validate_WithCertificateRetryDelayOutOfRange_Fails(long milliseconds, string shown)
    {
        var options = new NistBeaconOptions
        {
            CertificateRetryDelay = TimeSpan.FromMilliseconds(milliseconds),
        };

        var result = new NistBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            $"RandomnessBeacons:Nist:CertificateRetryDelay must be between 00:00:01 and 1.00:00:00; it is {shown}.",
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

        var failureCount = result.Failures?.Count();

        Assert.Equal(4, failureCount);
    }

    #endregion
}
