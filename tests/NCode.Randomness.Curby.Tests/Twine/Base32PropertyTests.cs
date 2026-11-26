// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.Twine;

public class Base32PropertyTests
{
    private const string Replay = "20261125,1";

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    #region Encode Tests

    [Property(Replay = Replay)]
    public void Encode_WithAnyBytes_DecodesToTheSameBytes(NonNull<byte[]> bytes)
    {
        var text = Base32.Encode(bytes.Get);
        var decoded = Base32.Decode(text);

        Assert.Equal(bytes.Get, decoded);
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

        var encoded = Base32.Encode(bytes);

        Assert.Equal(text.Get, encoded);
    }

    [Property(Replay = Replay)]
    public void Decode_WithAnyBase32Symbols_ReturnsCanonicalBytesOrThrowsTwineFormatException(
        NonNull<byte[]> symbols
    )
    {
        var characters = symbols.Get.Select(symbol => Alphabet[symbol % Alphabet.Length]).ToArray();
        var text = new string(characters);

        byte[] bytes;
        try
        {
            bytes = Base32.Decode(text);
        }
        catch (TwineFormatException)
        {
            return;
        }

        var encoded = Base32.Encode(bytes);

        Assert.Equal(text, encoded);
    }

    #endregion
}
