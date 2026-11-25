// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NCode.Randomness.Curby.Twine;

namespace NCode.Randomness.Curby.Tests.TestTwine;

/// <summary>
/// Terse constructors for IPLD values.
/// </summary>
internal static class Ipld
{
    public static IpldMap Map(params (string Key, IpldNode Value)[] entries) =>
        new([.. entries.Select(entry => KeyValuePair.Create(entry.Key, entry.Value))]);

    public static IpldList List(params IpldNode[] items) => new([.. items]);

    public static IpldString Text(string value) => new(value);

    public static IpldInteger Integer(long value) => new(value);

    public static IpldFloat Float(double value) => new(value);

    public static IpldBytes Bytes(byte[] value) => new(value);

    public static IpldLink Link(Cid cid) => new(cid);

    public static IpldMap With(this IpldMap map, string key, IpldNode value)
    {
        var entries = map
            .Entries.Where(entry => entry.Key != key)
            .ToImmutableArray()
            .Add(KeyValuePair.Create(key, value));
        return new IpldMap(entries);
    }
}
