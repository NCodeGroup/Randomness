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

    public ValidateOptionsResult Validate(string? name, CurbyBeaconOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var address = options.BaseAddress;
        if (
            address is null
            || !address.IsAbsoluteUri
            || address.Scheme != Uri.UriSchemeHttps
            || !address.AbsolutePath.EndsWith('/')
            || address.Query.Length > 0
            || address.Fragment.Length > 0
        )
        {
            failures.Add(
                $"{Prefix}{nameof(options.BaseAddress)} must be an absolute https URL ending in '/', with no query or fragment."
            );
        }

        try
        {
            Cid.Parse(options.ChainId ?? string.Empty);
        }
        catch (TwineFormatException exception)
        {
            failures.Add(
                $"{Prefix}{nameof(options.ChainId)} is not a Twine content identifier: {exception.Message}"
            );
        }

        try
        {
            Cid.Parse(options.QuantumChainId ?? string.Empty);
        }
        catch (TwineFormatException exception)
        {
            failures.Add(
                $"{Prefix}{nameof(options.QuantumChainId)} is not a Twine content identifier: {exception.Message}"
            );
        }

        try
        {
            Cid.Parse(options.BellChainId ?? string.Empty);
        }
        catch (TwineFormatException exception)
        {
            failures.Add(
                $"{Prefix}{nameof(options.BellChainId)} is not a Twine content identifier: {exception.Message}"
            );
        }

        if (options.MaxResponseBytes < 1024)
        {
            failures.Add($"{Prefix}{nameof(options.MaxResponseBytes)} must be at least 1024.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
