// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Randomness;

/// <summary>
/// Validates <see cref="RandomnessOptions"/> when the host starts, naming each bad setting by its configuration key.
/// </summary>
internal sealed class RandomnessOptionsValidator : IValidateOptions<RandomnessOptions>
{
    private const string Prefix = RandomnessOptions.SectionName + ":";
    private static TimeSpan MaxTimerPeriod { get; } =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    public ValidateOptionsResult Validate(string? name, RandomnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!Enum.IsDefined(options.Mode))
        {
            failures.Add(
                $"{Prefix}{nameof(options.Mode)} must be Continuous, InitializeOnly or Offline."
            );
        }

        CheckPeriod(
            failures,
            nameof(options.RefreshInterval),
            options.RefreshInterval,
            TimeSpan.FromSeconds(1)
        );
        CheckPeriod(
            failures,
            nameof(options.MaxRetryDelay),
            options.MaxRetryDelay,
            options.RefreshInterval
        );
        CheckPeriod(
            failures,
            nameof(options.FetchTimeout),
            options.FetchTimeout,
            TimeSpan.FromMilliseconds(1)
        );
        CheckPeriod(
            failures,
            nameof(options.MaxPulseAge),
            options.MaxPulseAge,
            TimeSpan.FromSeconds(1)
        );
        CheckBeaconLimits(failures, options);
        CheckPeriod(failures, nameof(options.MaxClockSkew), options.MaxClockSkew, TimeSpan.Zero);
        CheckPeriod(
            failures,
            nameof(options.InitialPulseTimeout),
            options.InitialPulseTimeout,
            TimeSpan.FromMilliseconds(1)
        );
        CheckPeriod(
            failures,
            nameof(options.ReseedInterval),
            options.ReseedInterval,
            TimeSpan.FromMilliseconds(1)
        );
        if (options.ReseedAfterBytes < 1)
        {
            failures.Add($"{Prefix}{nameof(options.ReseedAfterBytes)} must be at least 1.");
        }

        if (options.RequireInitialPulse && options.Mode == BeaconMode.Offline)
        {
            failures.Add(
                $"{Prefix}{nameof(options.RequireInitialPulse)} cannot be set when {Prefix}{nameof(options.Mode)} is Offline."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Adds a failure for each per-beacon pulse age limit that is missing, has an empty beacon name, or lies outside the
    /// range a timer accepts, in beacon-name order.
    /// </summary>
    /// <param name="failures">The list that receives a message for each failure.</param>
    /// <param name="options">The options whose per-beacon limits are checked.</param>
    private static void CheckBeaconLimits(List<string> failures, RandomnessOptions options)
    {
        if (options.MaxPulseAgeByBeacon is null)
        {
            failures.Add($"{Prefix}{nameof(options.MaxPulseAgeByBeacon)} must not be null.");
            return;
        }

        var entries = options.MaxPulseAgeByBeacon.OrderBy(
            entry => entry.Key,
            StringComparer.Ordinal
        );
        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                failures.Add(
                    $"{Prefix}{nameof(options.MaxPulseAgeByBeacon)} has an empty beacon name."
                );
            }

            CheckPeriod(
                failures,
                $"{nameof(options.MaxPulseAgeByBeacon)}:{key}",
                value,
                TimeSpan.FromSeconds(1)
            );
        }
    }

    private static void CheckPeriod(
        List<string> failures,
        string key,
        TimeSpan value,
        TimeSpan minimum
    )
    {
        if (value < minimum || value > MaxTimerPeriod)
        {
            failures.Add(
                $"{Prefix}{key} must be between {minimum} and {MaxTimerPeriod}; it is {value}."
            );
        }
    }
}
