// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby;

/// <summary>
/// Validates <see cref="CurbyBeaconOptions"/> when the host starts, naming each bad setting by its configuration key.
/// </summary>
internal sealed class CurbyBeaconOptionsValidator : IValidateOptions<CurbyBeaconOptions>
{
    private const string Prefix = CurbyBeaconOptions.SectionName + ":";

    private const int MinResponseBytes = 1024;

    /// <summary>
    /// Adds a failure when a chain identifier setting is not a Twine content identifier.
    /// </summary>
    /// <param name="failures">The failures found so far.</param>
    /// <param name="key">The name of the setting, within the section.</param>
    /// <param name="value">The value of the setting.</param>
    private static void CheckChainId(List<string> failures, string key, string? value)
    {
        try
        {
            Cid.Parse(value ?? string.Empty);
        }
        catch (TwineFormatException exception)
        {
            failures.Add($"{Prefix}{key} is not a Twine content identifier: {exception.Message}");
        }
    }

    public ValidateOptionsResult Validate(string? name, CurbyBeaconOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var address = options.BaseAddress;
        // The BaseAddress rule is copied on purpose into NistBeaconOptionsValidator in NCode.Randomness.Nist and
        // DrandBeaconOptionsValidator in NCode.Randomness.Drand: a fix to one copy belongs in the others.
        if (
            address is null
            || !address.IsAbsoluteUri
            || address.Scheme != Uri.UriSchemeHttps
            || !address.AbsolutePath.EndsWith('/')
            || address.UserInfo.Length > 0
            || address.Query.Length > 0
            || address.Fragment.Length > 0
        )
        {
            failures.Add(
                $"{Prefix}{nameof(options.BaseAddress)} must be an absolute https URL ending in '/', with no user info, query or fragment."
            );
        }

        CheckChainId(failures, nameof(options.ChainId), options.ChainId);
        CheckChainId(failures, nameof(options.QuantumChainId), options.QuantumChainId);
        CheckChainId(failures, nameof(options.BellChainId), options.BellChainId);

        if (options.MaxResponseBytes < MinResponseBytes)
        {
            failures.Add(
                $"{Prefix}{nameof(options.MaxResponseBytes)} must be at least {MinResponseBytes}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
