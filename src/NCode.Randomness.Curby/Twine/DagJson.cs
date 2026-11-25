// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// Reads DAG-JSON into the IPLD data model, strictly: a map whose only key is <c>/</c> is a link (a string) or bytes
/// (<c>{"bytes": "..."}</c>, unpadded standard base64), any other use of that key is refused, as are duplicate keys,
/// numbers that are not finite 64-bit floats or 64-bit integers, and bytes or links not in canonical form. Numbers with
/// a fraction or exponent are 64-bit floats and other numbers are 64-bit integers.
/// </summary>
internal static class DagJson
{
    private const string Reserved = "/";

    public static IpldNode Read(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ReadObject(element),
            JsonValueKind.Array => ReadArray(element),
            JsonValueKind.String => new IpldString(element.GetString() ?? string.Empty),
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.True => new IpldBoolean(true),
            JsonValueKind.False => new IpldBoolean(false),
            JsonValueKind.Null => IpldNull.Instance,
            _ => throw new TwineFormatException($"A JSON {element.ValueKind} is not DAG-JSON."),
        };

    private static IpldNode ReadObject(JsonElement element)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!keys.Add(property.Name))
            {
                throw new TwineFormatException($"The key '{property.Name}' appears twice.");
            }
        }

        if (keys.Contains(Reserved))
        {
            return ReadReserved(element, keys.Count);
        }

        var entries = ImmutableArray.CreateBuilder<KeyValuePair<string, IpldNode>>(keys.Count);
        foreach (var property in element.EnumerateObject())
        {
            var value = Read(property.Value);
            entries.Add(KeyValuePair.Create(property.Name, value));
        }

        return new IpldMap(entries.MoveToImmutable());
    }

    private static IpldNode ReadReserved(JsonElement element, int keyCount)
    {
        var value = element.GetProperty(Reserved);
        if (keyCount == 1 && value.ValueKind == JsonValueKind.String)
        {
            return new IpldLink(Cid.Parse(value.GetString() ?? string.Empty));
        }

        if (keyCount == 1 && IsBytes(value, out var bytes))
        {
            return new IpldBytes(DecodeBase64(bytes.GetString() ?? string.Empty));
        }

        throw new TwineFormatException("A map with the key '/' must be a link or bytes.");
    }

    private static bool IsBytes(JsonElement value, out JsonElement bytes)
    {
        bytes = default;
        return value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Count() == 1
            && value.TryGetProperty("bytes", out bytes)
            && bytes.ValueKind == JsonValueKind.String;
    }

    private static IpldList ReadArray(JsonElement element)
    {
        var items = ImmutableArray.CreateBuilder<IpldNode>(element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            items.Add(Read(item));
        }

        return new IpldList(items.MoveToImmutable());
    }

    /// <summary>
    /// Reads a JSON number as a DAG-JSON value: a number with a fraction or an exponent is a finite 64-bit float, and
    /// any other number is a 64-bit integer.
    /// </summary>
    /// <param name="element">The JSON number to read.</param>
    /// <returns>An <see cref="IpldFloat"/> or an <see cref="IpldInteger"/>.</returns>
    /// <exception cref="TwineFormatException">
    /// The number is not finite or does not fit a 64-bit float, or it is not a 64-bit integer.
    /// </exception>
    private static IpldNode ReadNumber(JsonElement element)
    {
        var raw = element.GetRawText();
        if (raw.AsSpan().IndexOfAny(".eE") >= 0)
        {
            if (!element.TryGetDouble(out var number) || !double.IsFinite(number))
            {
                throw new TwineFormatException($"The number {raw} is not a finite 64-bit float.");
            }

            return new IpldFloat(number);
        }

        if (!element.TryGetInt64(out var value))
        {
            throw new TwineFormatException($"The number {raw} is not a 64-bit integer.");
        }

        return new IpldInteger(value);
    }

    private static byte[] DecodeBase64(string text)
    {
        if (text.Contains('=', StringComparison.Ordinal))
        {
            throw new TwineFormatException("DAG-JSON bytes are unpadded base64.");
        }

        var padded = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        var buffer = new byte[padded.Length / 4 * 3];
        if (!Convert.TryFromBase64String(padded, buffer, out var written))
        {
            throw new TwineFormatException("DAG-JSON bytes are not valid base64.");
        }

        var bytes = buffer[..written];
        // Convert skips white space and accepts nonzero unused bits; re-encoding refuses both, so each value has one
        // spelling.
        if (
            !string.Equals(
                Convert.ToBase64String(bytes).TrimEnd('='),
                text,
                StringComparison.Ordinal
            )
        )
        {
            throw new TwineFormatException("DAG-JSON bytes are not in canonical base64.");
        }

        return bytes;
    }
}
