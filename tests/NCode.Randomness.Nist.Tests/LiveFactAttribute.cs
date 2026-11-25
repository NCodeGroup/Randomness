// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Nist.Tests;

/// <summary>
/// A fact that calls the real NIST Randomness Beacon, so it runs only when NCODE_RANDOMNESS_LIVE_TESTS is <c>1</c>:
/// tests are otherwise hermetic.
/// </summary>
// Copied on purpose from LiveFactAttribute in NCode.Randomness.Curby.Tests, as NCode.Randomness.Drand.Tests copies
// it, each naming its own beacon: each test project stands alone, so a fix to one copy belongs in the others.
public sealed class LiveFactAttribute : FactAttribute
{
    public const string Variable = "NCODE_RANDOMNESS_LIVE_TESTS";

    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
        {
            Skip = $"Set {Variable}=1 to call the real NIST Randomness Beacon.";
        }
    }
}
