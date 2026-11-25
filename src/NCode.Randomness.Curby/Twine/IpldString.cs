// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD string.
/// </summary>
internal sealed class IpldString(string value) : IpldNode
{
    public string Value { get; } = value;
}
