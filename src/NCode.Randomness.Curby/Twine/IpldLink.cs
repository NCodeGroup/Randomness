// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD link: a content identifier.
/// </summary>
internal sealed class IpldLink(Cid cid) : IpldNode
{
    public Cid Cid { get; } = cid;
}
