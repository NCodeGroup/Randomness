// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Randomness;

namespace SiteSamples.Concepts;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class TheGeneratorSamples
{
    [Fact]
    public void SiteTheGeneratorReseedSample_Runs()
    {
        var services = new ServiceCollection();

        // begin-snippet: SiteTheGeneratorReseed
        services.AddRandomness(options =>
        {
            options.ReseedInterval = TimeSpan.FromSeconds(30);
            options.ReseedAfterBytes = 256 * 1024;
        });
        // end-snippet

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RandomnessOptions>>().Value;
        var expectedInterval = TimeSpan.FromSeconds(30);
        Assert.Equal(expectedInterval, options.ReseedInterval);
        Assert.Equal(256 * 1024, options.ReseedAfterBytes);
    }
}
