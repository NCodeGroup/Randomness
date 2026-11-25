// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Randomness.Drand;

/// <summary>
/// Validates <see cref="DrandBeaconOptions"/> when the host starts, naming each bad setting by its configuration key.
/// </summary>
internal sealed class DrandBeaconOptionsValidator : IValidateOptions<DrandBeaconOptions>
{
    private const string Prefix = DrandBeaconOptions.SectionName + ":";

    private const int MinResponseBytes = 1024;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DrandBeaconOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var address = options.BaseAddress;
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
