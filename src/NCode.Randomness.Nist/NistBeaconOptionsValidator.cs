// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Randomness.Nist;

/// <summary>
/// Validates <see cref="NistBeaconOptions"/> when the host starts, naming each bad setting by its configuration key.
/// </summary>
internal sealed class NistBeaconOptionsValidator : IValidateOptions<NistBeaconOptions>
{
    private const string Prefix = NistBeaconOptions.SectionName + ":";

    private const int MinResponseBytes = 1024;

    private const long MinChainIndex = 1;

    private const int CertificateIdLength = 128;

    /// <summary>
    /// Gets whether a value is a certificate identifier: 128 hexadecimal digits, the SHA-512 hash of a DER encoding.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a certificate identifier.</returns>
    private static bool IsCertificateId(string? value)
    {
        if (value is null || value.Length != CertificateIdLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, NistBeaconOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var address = options.BaseAddress;
        // The BaseAddress rule is copied on purpose from CurbyBeaconOptionsValidator in NCode.Randomness.Curby, as
        // DrandBeaconOptionsValidator in NCode.Randomness.Drand copies it: a fix to one copy belongs in the others.
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

        if (options.ChainIndex < MinChainIndex)
        {
            failures.Add($"{Prefix}{nameof(options.ChainIndex)} must be at least {MinChainIndex}.");
        }

        var ids = options.CertificateIds;
        if (ids is null)
        {
            failures.Add(
                $"{Prefix}{nameof(options.CertificateIds)} must not be null; leave it empty to trust the default certificates."
            );
        }
        else
        {
            var seen = new HashSet<string>(ids.Count, StringComparer.OrdinalIgnoreCase);
            var repeated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < ids.Count; ++i)
            {
                var id = ids[i];
                if (!IsCertificateId(id))
                {
                    failures.Add(
                        $"{Prefix}{nameof(options.CertificateIds)}:{i} is not a certificate identifier: {CertificateIdLength} hexadecimal digits."
                    );
                }
                else if (!seen.Add(id) && repeated.Add(id))
                {
                    failures.Add(
                        $"{Prefix}{nameof(options.CertificateIds)} lists {id} more than once."
                    );
                }
            }
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
