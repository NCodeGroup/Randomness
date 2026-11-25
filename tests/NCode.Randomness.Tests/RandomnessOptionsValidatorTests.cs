// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests;

public class RandomnessOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        var result = new RandomnessOptionsValidator().Validate(null, new RandomnessOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(RandomnessOptions.RefreshInterval))]
    [InlineData(nameof(RandomnessOptions.MaxRetryDelay))]
    [InlineData(nameof(RandomnessOptions.FetchTimeout))]
    [InlineData(nameof(RandomnessOptions.MaxPulseAge))]
    [InlineData(nameof(RandomnessOptions.MaxClockSkew))]
    [InlineData(nameof(RandomnessOptions.InitialPulseTimeout))]
    [InlineData(nameof(RandomnessOptions.ReseedInterval))]
    public void Validate_WithNegativePeriod_NamesTheKey(string key)
    {
        var options = new RandomnessOptions();
        typeof(RandomnessOptions).GetProperty(key)?.SetValue(options, TimeSpan.FromSeconds(-1));

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"Randomness:{key} ", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithNegativeBeaconLimit_NamesTheKey()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon["a"] = TimeSpan.FromSeconds(-1);

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "Randomness:MaxPulseAgeByBeacon:a",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithEmptyBeaconName_Fails()
    {
        var options = new RandomnessOptions();
        options.MaxPulseAgeByBeacon[" "] = TimeSpan.FromMinutes(1);

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains("empty beacon name", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithPeriodBeyondTimerRange_Fails()
    {
        var options = new RandomnessOptions { MaxRetryDelay = TimeSpan.FromDays(60) };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains(
            "Randomness:MaxRetryDelay",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithRetryDelayBelowRefresh_Fails()
    {
        var options = new RandomnessOptions { MaxRetryDelay = TimeSpan.FromSeconds(30) };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains(
            "Randomness:MaxRetryDelay",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithUndefinedMode_Fails()
    {
        var options = new RandomnessOptions { Mode = (BeaconMode)42 };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains("Randomness:Mode", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithNoReseedVolume_Fails()
    {
        var options = new RandomnessOptions { ReseedAfterBytes = 0 };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains(
            "Randomness:ReseedAfterBytes",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenOfflineRequiresAPulse_Fails()
    {
        var options = new RandomnessOptions
        {
            Mode = BeaconMode.Offline,
            RequireInitialPulse = true,
        };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Contains(
            "Randomness:RequireInitialPulse",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WithSeveralFailures_ReportsEveryOne()
    {
        var options = new RandomnessOptions { ReseedAfterBytes = 0, FetchTimeout = TimeSpan.Zero };

        var result = new RandomnessOptionsValidator().Validate(null, options);

        Assert.Equal(2, result.Failures?.Count());
    }

    #endregion
}
