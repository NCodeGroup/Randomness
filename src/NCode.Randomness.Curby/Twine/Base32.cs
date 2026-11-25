// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// RFC 4648 base32 in lower case without padding: multibase prefix <c>b</c>, the form CIDs are written in. Decoding is
/// strict: only the 32 lower-case symbols, and the unused trailing bits must be zero, so each value has one spelling.
/// </summary>
internal static class Base32
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    public static byte[] Decode(ReadOnlySpan<char> text)
    {
        var bytes = new byte[text.Length * 5 / 8];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var symbol in text)
        {
            var value = Alphabet.IndexOf(symbol, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new TwineFormatException($"'{symbol}' is not a lower-case base32 symbol.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }

        if (buffer != 0 || bits >= 5)
        {
            throw new TwineFormatException("The base32 text is not in its canonical form.");
        }

        return bytes;
    }

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var text = new char[((bytes.Length * 8) + 4) / 5];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var value in bytes)
        {
            buffer = ((buffer << 8) | value) & 0xFFF;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                text[index++] = Alphabet[(buffer >> bits) & 31];
            }
        }

        if (bits > 0)
        {
            text[index] = Alphabet[(buffer << (5 - bits)) & 31];
        }

        return new string(text);
    }
}
