// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Curby.Tests.Fixtures;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class TwineDocumentPropertyTests
{
    private const string Replay = "20261125,1";

    #region Parse Tests

    [Property(Replay = Replay)]
    public void Parse_WithAnyBytes_ThrowsTwineFormatException(NonNull<byte[]> bytes)
    {
        Assert.Throws<TwineFormatException>(() => TwineDocument.Parse(bytes.Get));
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Parse_WithOneByteChanged_ThrowsTwineFormatExceptionOrKeepsTheCid(
        NonNegativeInt document,
        NonNegativeInt position,
        byte value
    )
    {
        var latest = CurbyFixtures.Pulse(CurbyFixtures.LatestRound);
        var previous = CurbyFixtures.Pulse(CurbyFixtures.LatestRound - 1);
        string[] jsons = [CurbyFixtures.Chain, latest, previous];
        var json = jsons[document.Get % jsons.Length];
        var bytes = Encoding.UTF8.GetBytes(json);
        bytes[position.Get % bytes.Length] = value;

        TwineDocument parsed;
        try
        {
            parsed = TwineDocument.Parse(bytes);
        }
        catch (TwineFormatException)
        {
            return;
        }

        var cid = parsed.Cid.ToString();
        var expected = CurbyFixtures.CidOf(json);

        Assert.Equal(expected, cid);
    }

    #endregion

    #region ParseArray Tests

    [Property(Replay = Replay, MaxTest = 500)]
    public void ParseArray_WithOneByteChanged_ThrowsTwineFormatExceptionOrKeepsEachCid(
        NonNegativeInt position,
        byte value
    )
    {
        var bytes = Encoding.UTF8.GetBytes(CurbyQuantumFixtures.RoundJson);
        bytes[position.Get % bytes.Length] = value;

        ImmutableArray<TwineDocument> documents;
        try
        {
            documents = TwineDocument.ParseArray(bytes);
        }
        catch (TwineFormatException)
        {
            return;
        }

        Assert.Equal(3, documents.Length);
        Assert.Equal(CurbyQuantumFixtures.RequestId, documents[0].Cid.ToString());
        Assert.Equal(CurbyQuantumFixtures.PrecommitId, documents[1].Cid.ToString());
        Assert.Equal(CurbyQuantumFixtures.ResultId, documents[2].Cid.ToString());
    }

    #endregion
}
