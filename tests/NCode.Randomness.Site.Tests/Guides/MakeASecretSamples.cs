// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Randomness;

namespace SiteSamples.Guides;

// `dotnet mdsnippets` and the site copy each sample between begin-snippet and end-snippet (ADR-1016).
public sealed class MakeASecretSamples
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    [Fact]
    public void SiteMakeASecretCodeSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteMakeASecretCode
        var code = random.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 16); // 16 characters, no 0, O, 1 or I
        // end-snippet

        Assert.Equal(16, code.Length);
        Assert.All(code, character => Assert.Contains(character, Alphabet));
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
    }

    [Fact]
    public void SiteMakeASecretGuidSample_Runs()
    {
        ISecureRandom random = SystemRandomSource.Instance;

        // begin-snippet: SiteMakeASecretGuid
        var id = random.NextGuid(); // a version 4 GUID
        // end-snippet

        Assert.Equal(4, id.Version);
    }
}
