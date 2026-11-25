// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// IPLD bytes.
/// </summary>
internal sealed class IpldBytes(byte[] value) : IpldNode
{
    public ReadOnlySpan<byte> Value => Bytes;

    private byte[] Bytes { get; } = value;
}
