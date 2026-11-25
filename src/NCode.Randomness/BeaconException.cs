// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// A beacon gave no usable pulse. <see cref="Error"/> says why, for code to act on.
/// </summary>
public sealed class BeaconException : Exception
{
    /// <summary>
    /// Initializes an exception for <paramref name="beacon"/>.
    /// </summary>
    /// <param name="error">Why the beacon gave no usable pulse.</param>
    /// <param name="beacon">The name of the beacon.</param>
    /// <param name="message">A description for a person.</param>
    public BeaconException(BeaconError error, string beacon, string message)
        : base(message)
    {
        Error = error;
        Beacon = beacon;
    }

    /// <summary>
    /// Initializes an exception for <paramref name="beacon"/> caused by <paramref name="innerException"/>.
    /// </summary>
    /// <param name="error">Why the beacon gave no usable pulse.</param>
    /// <param name="beacon">The name of the beacon.</param>
    /// <param name="message">A description for a person.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public BeaconException(
        BeaconError error,
        string beacon,
        string message,
        Exception innerException
    )
        : base(message, innerException)
    {
        Error = error;
        Beacon = beacon;
    }

    /// <summary>
    /// Gets why the beacon gave no usable pulse.
    /// </summary>
    public BeaconError Error { get; }

    /// <summary>
    /// Gets the name of the beacon.
    /// </summary>
    public string Beacon { get; }
}
