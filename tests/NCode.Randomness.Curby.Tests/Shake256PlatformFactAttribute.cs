// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Tests;

/// <summary>
/// A fact that runs only where the operating system provides SHAKE256.
/// </summary>
public sealed class Shake256PlatformFactAttribute : FactAttribute
{
    public Shake256PlatformFactAttribute()
    {
        if (!Shake256.IsSupported)
        {
            Skip = "The platform has no SHAKE256.";
        }
    }
}
