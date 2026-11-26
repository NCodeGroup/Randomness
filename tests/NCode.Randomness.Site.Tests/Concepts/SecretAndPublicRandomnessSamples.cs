// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;
using Recorded;

namespace SiteSamples.Concepts;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class SecretAndPublicRandomnessSamples
{
    [Fact]
    public void SiteSecretAndPublicKindsSample_Runs()
    {
        var pulse = RecordedDocuments.CurbyPulse(1620423);

        // begin-snippet: SiteSecretAndPublicKinds
        ISecureRandom secrets = SystemRandomSource.Instance;
        using var draw = VerifiableRandom.Create(pulse, "example-raffle-2026-11-25");
        IRandomSource values = draw; // a draw is an IRandomSource, never an ISecureRandom

        var secretRoll = secrets.NextInteger(1, 7); // nobody can predict it
        var drawRoll = values.NextInteger(1, 7); // everyone with the pulse can recompute it
        // end-snippet

        Assert.IsNotAssignableFrom<ISecureRandom>(draw);
        Assert.InRange(secretRoll, 1, 6);
        Assert.InRange(drawRoll, 1, 6);
    }
}
