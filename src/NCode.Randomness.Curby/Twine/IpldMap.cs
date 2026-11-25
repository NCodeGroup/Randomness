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

    public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        where T : IpldNode
    {
        if (Lookup.TryGetValue(key, out var node) && node is T typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }
}
