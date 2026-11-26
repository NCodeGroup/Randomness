// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;

namespace SiteSamples.Start;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class WhichApiSamples
{
    [Fact]
    public void SiteAnswerSystemSample_Runs()
    {
        // begin-snippet: SiteAnswerSystem
        Guid id = SystemRandomSource.Instance.NextGuid();
        // end-snippet

        Assert.Equal(4, id.Version);
    }
}
