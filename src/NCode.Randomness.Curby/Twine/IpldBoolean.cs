// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD boolean.
/// </summary>
internal sealed class IpldBoolean(bool value) : IpldNode
{
    public bool Value { get; } = value;
}
