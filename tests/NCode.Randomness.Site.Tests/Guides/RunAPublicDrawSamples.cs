// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Randomness;
using Recorded;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class RunAPublicDrawSamples
{
    [Fact]
    public async Task SiteRunAPublicDrawPublishSample_Runs()
    {
        var services = new ServiceCollection();
        services.AddRandomness().AddBeacon<RecordedCurbyBeacon>();
        await using var provider = services.BuildServiceProvider();
        var draws = provider.GetRequiredService<IVerifiableRandomFactory>();
        using var draw = await draws.CreateAsync(
            "curby",
            round: 1620423,
            context: "example-raffle-2026-11-25",
            CancellationToken.None
        );

        // begin-snippet: SiteRunAPublicDrawPublish
        // The record to publish with the result: the beacon, the round, the pulse's ID,
        // the context and the algorithm's name, which is all anyone needs to recompute it.
        var record = string.Join(
            ", ",
            $"{draw.Pulse.Beacon} round {draw.Pulse.Round}",
            $"pulse {draw.Pulse.Id}",
            $"context {draw.Context}",
            $"algorithm {VerifiableRandom.Algorithm}"
        );
        // end-snippet

        Assert.StartsWith("curby round 1620423, pulse bafyriqbtxzd22", record);
        Assert.EndsWith(
            $"context example-raffle-2026-11-25, algorithm {VerifiableRandom.Algorithm}",
            record
        );
    }

    [Fact]
    public void SiteRunAPublicDrawFromPulseSample_Runs()
    {
        var pulse = RecordedDocuments.CurbyPulse(1620423);

        // begin-snippet: SiteRunAPublicDrawFromPulse
        using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");
        var bytes = draw.GetBytes(32);
        var first = Convert.ToHexStringLower(bytes);
        // first is e43e360c…986e, the bytes docs/draws.md's reference implementation prints.
        // end-snippet

        Assert.Equal(
            "e43e360c9e2b9cd6a094e04a64e65922a3581ba6864da1cb1490244522f9986e",
            first
        );
    }
}
