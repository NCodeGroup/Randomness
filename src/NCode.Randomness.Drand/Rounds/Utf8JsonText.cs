// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Unicode;

namespace NCode.Randomness.Drand.Rounds;

/// <summary>
/// Checks that a UTF-8 JSON document is well-formed Unicode text before it is parsed: valid UTF-8, with every
/// <c>\u</c> escape of a surrogate in a high-low pair. System.Text.Json parses a document without decoding its
/// strings, and throws <see cref="InvalidOperationException"/> only when a string or property name with bad text is
/// read.
/// </summary>
// Copied on purpose into NCode.Randomness.Curby, NCode.Randomness.Nist and NCode.Randomness.Drand: independently
// versioned packages never share a type, so a fix to one copy belongs in the others.
internal static class Utf8JsonText
{
    /// <summary>
    /// The UTF-8 byte of the backslash that starts a JSON escape.
    /// </summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>
    /// The UTF-8 byte that follows the backslash of a <c>\u</c> escape.
    /// </summary>
    private const byte UnicodeEscape = (byte)'u';

    /// <summary>
    /// The number of hexadecimal digits in a <c>\u</c> escape.
    /// </summary>
    private const int HexLength = 4;

    /// <summary>
    /// Determines whether a UTF-8 JSON document is well-formed Unicode text.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 bytes of the JSON document.</param>
    /// <returns>
    /// <see langword="true"/> when the bytes are valid UTF-8 and every <c>\u</c> escape of a surrogate is half of a
    /// high-low pair; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsText(ReadOnlySpan<byte> utf8Json)
    {
        if (!Utf8.IsValid(utf8Json))
        {
            return false;
        }

        var rest = utf8Json;
        var backslash = rest.IndexOf(Backslash);
        while (backslash >= 0)
        {
            rest = rest[(backslash + 1)..];
            if (!TryReadUnit(rest, out var unit))
            {
                // Any other escape is skipped with the character it escapes, so \\uD800 is a backslash and text.
                rest = rest.IsEmpty ? rest : rest[1..];
            }
            else
            {
                rest = rest[(1 + HexLength)..];
                if (char.IsLowSurrogate(unit))
                {
                    return false;
                }

                if (char.IsHighSurrogate(unit))
                {
                    if (rest.IsEmpty || rest[0] != Backslash)
                    {
                        return false;
                    }

                    var next = rest[1..];
                    if (!TryReadUnit(next, out var low) || !char.IsLowSurrogate(low))
                    {
                        return false;
                    }

                    rest = rest[(2 + HexLength)..];
                }
            }

            backslash = rest.IndexOf(Backslash);
        }

        return true;
    }

    /// <summary>
    /// Reads the UTF-16 code unit of a <c>\u</c> escape.
    /// </summary>
    /// <param name="escape">The bytes after a backslash.</param>
    /// <param name="unit">The code unit when the bytes start with a <c>\u</c> escape; otherwise, the default.</param>
    /// <returns><see langword="true"/> when <paramref name="escape"/> starts with a <c>\u</c> escape.</returns>
    private static bool TryReadUnit(ReadOnlySpan<byte> escape, out char unit)
    {
        unit = default;
        if (escape.Length < 1 + HexLength || escape[0] != UnicodeEscape)
        {
            return false;
        }

        var hex = escape.Slice(1, HexLength);
        if (
            !ushort.TryParse(
                hex,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var value
            )
        )
        {
            return false;
        }

        unit = (char)value;
        return true;
    }
}
