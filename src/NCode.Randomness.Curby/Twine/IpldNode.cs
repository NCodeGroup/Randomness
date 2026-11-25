// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Curby.Twine;

/// <summary>
/// A value of the IPLD data model, as DAG-JSON reads it and DAG-CBOR writes it. Floats are not part of it here: no
/// Twine field uses one, so data that holds one is refused.
/// </summary>
internal abstract class IpldNode
{
    public T As<T>(string field)
        where T : IpldNode =>
        this as T ?? throw new TwineFormatException($"The field '{field}' has the wrong type.");
}
