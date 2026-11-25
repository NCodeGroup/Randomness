// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// The IPLD null.
/// </summary>
internal sealed class IpldNull : IpldNode
{
    public static IpldNull Instance { get; } = new();
}
