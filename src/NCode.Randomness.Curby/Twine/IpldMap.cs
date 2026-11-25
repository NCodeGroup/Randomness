// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD map, with string keys, each unique.
/// </summary>
internal sealed class IpldMap(ImmutableArray<KeyValuePair<string, IpldNode>> entries) : IpldNode
{
    public ImmutableArray<KeyValuePair<string, IpldNode>> Entries { get; } = entries;

    private FrozenDictionary<string, IpldNode> Lookup { get; } =
        entries.ToFrozenDictionary(StringComparer.Ordinal);

    public T Get<T>(string key)
        where T : IpldNode =>
        Lookup.TryGetValue(key, out var value)
            ? value.As<T>(key)
            : throw new TwineFormatException($"The field '{key}' is missing.");

    /// <summary>
    /// Reads the field <paramref name="key"/>: returns false when the map has no such field, and throws a
    /// <see cref="TwineFormatException"/> when the field holds another kind of node than <typeparamref name="T"/>.
    /// </summary>
    /// <param name="key">The name of the field.</param>
    /// <param name="value">The field's node, when the map has it.</param>
    /// <typeparam name="T">The kind of node the field must hold.</typeparam>
    /// <returns><see langword="true"/> when the map has the field; otherwise <see langword="false"/>.</returns>
    public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        where T : IpldNode
    {
        if (!Lookup.TryGetValue(key, out var node))
        {
            value = null;
            return false;
        }

        value = node.As<T>(key);
        return true;
    }
}
