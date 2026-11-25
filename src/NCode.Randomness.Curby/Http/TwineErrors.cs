// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Randomness.Curby.Twine;
using Polly;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Classifies the exceptions a Twine API fetch can throw, so every beacon over the Twine API reports them alike.
/// </summary>
internal static class TwineErrors
{
    /// <summary>
    /// Translates an exception a Twine API fetch threw into the <see cref="BeaconException"/> the beacon reports: data
    /// that fails verification or cannot be read is <see cref="BeaconError.InvalidPulse"/>, and an outage is
    /// <see cref="BeaconError.Unavailable"/>. The exception is the inner exception.
    /// </summary>
    /// <param name="exception">The exception to translate.</param>
    /// <param name="beacon">The name of the beacon that fetched.</param>
    /// <param name="source">The name of the service the messages blame, such as <c>CURBy</c>.</param>
    /// <param name="cancellationToken">The caller's token, whose cancellation is not an outage.</param>
    /// <returns>
    /// The exception to throw, or <see langword="null"/> when <paramref name="exception"/> is a beacon failure, a
    /// disposal or a cancellation that the caller should see as it is.
    /// </returns>
    public static BeaconException? ToBeaconException(
        Exception exception,
        string beacon,
        string source,
        CancellationToken cancellationToken
    )
    {
        if (exception is TwineFormatException or JsonException)
        {
            var message =
                $"{source} answered with data that fails verification: {exception.Message}";
            return new BeaconException(BeaconError.InvalidPulse, beacon, message, exception);
        }

        if (IsOutage(exception, cancellationToken))
        {
            var message = $"{source} is unavailable: {exception.Message}";
            return new BeaconException(BeaconError.Unavailable, beacon, message, exception);
        }

        if (IsMalformed(exception, cancellationToken))
        {
            // Anything else the parser or verifier throws, such as an invalid UTF-16 escape, is bad data too.
            var message = $"{source} answered with data that cannot be read: {exception.Message}";
            return new BeaconException(BeaconError.InvalidPulse, beacon, message, exception);
        }

        return null;
    }

    /// <summary>
    /// Returns whether <paramref name="exception"/> is neither a beacon failure, a disposal, a cancellation nor an
    /// outage, so the parser or verifier threw it on malformed data, such as an invalid UTF-16 escape.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="cancellationToken">The caller's token, whose cancellation is not an outage.</param>
    /// <returns><see langword="true"/> when the data was malformed; otherwise <see langword="false"/>.</returns>
    internal static bool IsMalformed(Exception exception, CancellationToken cancellationToken) =>
        exception is not (BeaconException or ObjectDisposedException or OperationCanceledException)
        && !IsOutage(exception, cancellationToken);

    /// <summary>
    /// Returns whether <paramref name="exception"/> shows the service could not be reached: a failed request, a
    /// timeout, an I/O failure, a corrupt compressed body, a cancellation the caller did not ask for or a request the
    /// resilience policy rejected.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="cancellationToken">The caller's token, whose cancellation is not an outage.</param>
    /// <returns><see langword="true"/> when the service is unavailable; otherwise <see langword="false"/>.</returns>
    internal static bool IsOutage(Exception exception, CancellationToken cancellationToken) =>
        exception is not BeaconException
        && (
            exception
                is HttpRequestException
                    or TimeoutException
                    or IOException
                    or InvalidDataException
            || (
                exception is OperationCanceledException
                && !cancellationToken.IsCancellationRequested
            )
            || exception is ExecutionRejectedException
        );
}
