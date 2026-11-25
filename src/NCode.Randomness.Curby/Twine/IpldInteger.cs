// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD integer, within the range of a 64-bit signed integer.
/// </summary>
internal sealed class IpldInteger(long value) : IpldNode
{
    public long Value { get; } = value;
}
