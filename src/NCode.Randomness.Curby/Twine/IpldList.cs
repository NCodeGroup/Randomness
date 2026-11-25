// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD list.
/// </summary>
internal sealed class IpldList(ImmutableArray<IpldNode> items) : IpldNode
{
    public ImmutableArray<IpldNode> Items { get; } = items;
}
