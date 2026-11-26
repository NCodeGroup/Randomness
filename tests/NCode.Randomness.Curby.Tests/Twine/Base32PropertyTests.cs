// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class Base32PropertyTests
{
    private const string Replay = "20261125,1";

    #region Encode Tests

    [Property(Replay = Replay)]
    public void Encode_WithAnyBytes_DecodesToTheSameBytes(NonNull<byte[]> bytes)
    {
        Assert.Equal(bytes.Get, Base32.Decode(Base32.Encode(bytes.Get)));
    }

    #endregion

    #region Decode Tests

    [Property(Replay = Replay)]
    public void Decode_WithAnyText_ReturnsCanonicalBytesOrThrowsTwineFormatException(
        NonNull<string> text
    )
    {
        byte[] bytes;
        try
        {
            bytes = Base32.Decode(text.Get);
        }
        catch (TwineFormatException)
        {
            return;
        }

        Assert.Equal(text.Get, Base32.Encode(bytes));
    }

    #endregion
}
