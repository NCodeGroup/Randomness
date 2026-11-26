// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;

namespace SiteSamples.Reference;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class DrawAlgorithmSamples
{
    [Fact]
    public void SiteDrawAlgorithmNameSample_Runs()
    {
        // begin-snippet: SiteDrawAlgorithmName
        var name = VerifiableRandom.Algorithm; // "ncode-randomness-draw/1"
        var longest = VerifiableRandom.MaxContextLength; // 1024 characters
        // end-snippet

        Assert.Equal("ncode-randomness-draw/1", name);
        Assert.Equal(1024, longest);
    }
}
