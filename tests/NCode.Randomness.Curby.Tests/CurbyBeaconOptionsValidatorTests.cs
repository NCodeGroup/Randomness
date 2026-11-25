// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Tests;

public class CurbyBeaconOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        Assert.True(
            new CurbyBeaconOptionsValidator().Validate(null, new CurbyBeaconOptions()).Succeeded
        );
    }

    [Theory]
    [InlineData("http://api.entwine.me/")]
    [InlineData("https://api.entwine.me/v1")]
    [InlineData("https://user:secret@api.entwine.me/")]
    [InlineData("https://api.entwine.me/?x=1")]
    [InlineData("https://api.entwine.me/#x")]
    public void Validate_WithUnsafeBaseAddress_Fails(string address)
    {
        var options = new CurbyBeaconOptions { BaseAddress = new Uri(address) };

        var result = new CurbyBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Curby:BaseAddress",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithRelativeBaseAddress_Fails()
    {
        var options = new CurbyBeaconOptions { BaseAddress = new Uri("twine/", UriKind.Relative) };

        Assert.True(new CurbyBeaconOptionsValidator().Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_WithMalformedChain_Fails()
    {
        var options = new CurbyBeaconOptions { ChainId = "not-a-cid" };

        var result = new CurbyBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Curby:ChainId",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithMalformedQuantumChain_Fails()
    {
        var options = new CurbyBeaconOptions { QuantumChainId = "not-a-cid" };

        var result = new CurbyBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Curby:QuantumChainId",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithMalformedBellChain_Fails()
    {
        var options = new CurbyBeaconOptions { BellChainId = "not-a-cid" };

        var result = new CurbyBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Curby:BellChainId",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithTinyResponseLimit_Fails()
    {
        var options = new CurbyBeaconOptions { MaxResponseBytes = 10 };

        var result = new CurbyBeaconOptionsValidator().Validate(null, options);

        Assert.Contains(
            "RandomnessBeacons:Curby:MaxResponseBytes",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
