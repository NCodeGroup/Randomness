// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Polly;

namespace NCode.Randomness.Curby.Http;

/// <summary>
/// Classifies the exceptions a Twine API fetch can throw, so every beacon over the Twine API reports them alike.
/// </summary>
internal static class TwineErrors
{
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
