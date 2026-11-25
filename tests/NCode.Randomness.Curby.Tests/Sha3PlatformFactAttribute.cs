// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Randomness.Curby.Tests;

/// <summary>
/// A fact that runs only where the operating system provides SHA3-512.
/// </summary>
public sealed class Sha3PlatformFactAttribute : FactAttribute
{
    public Sha3PlatformFactAttribute()
    {
        if (!SHA3_512.IsSupported)
        {
            Skip = "The platform has no SHA3-512.";
        }
    }
}
