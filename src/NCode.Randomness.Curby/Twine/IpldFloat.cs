// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// An IPLD float, a 64-bit IEEE 754 value.
/// </summary>
internal sealed class IpldFloat(double value) : IpldNode
{
    public double Value { get; } = value;
}
