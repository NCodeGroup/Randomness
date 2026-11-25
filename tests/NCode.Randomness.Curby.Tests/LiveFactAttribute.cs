// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Tests;

/// <summary>
/// A fact that calls the real CURBy API, so it runs only when NCODE_RANDOMNESS_LIVE_TESTS is <c>1</c>: tests are
/// otherwise hermetic.
/// </summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public const string Variable = "NCODE_RANDOMNESS_LIVE_TESTS";

    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
        {
            Skip = $"Set {Variable}=1 to call the real CURBy API.";
        }
    }
}
