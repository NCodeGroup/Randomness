// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Recorded;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class CheckADrawSamples
{
    [Fact]
    public void SiteCheckADrawCompareSample_Runs()
    {
        var pulse = RecordedDocuments.CurbyPulse(1620423);
        const long committedRound = 1620423;
        const string committedId =
            "bafyriqbtxzd22eybdovubearwfjyzlyowyhxqdkbzyboftamh6rhob2jafopfz4h2nf3eec4nawu2gil2vpgn57qilrlh23piwyozlugpmcvc";

        // begin-snippet: SiteCheckADrawCompare
        // The documents are authentic, but are they the round
        // the draw committed to?
        if (
            pulse.Beacon != "curby"
            || pulse.Round != committedRound
            || pulse.Id != committedId
        )
        {
            throw new InvalidOperationException(
                "The documents are authentic, but not the committed round."
            );
        }
        // end-snippet

        Assert.Equal(committedRound, pulse.Round);
    }
}
