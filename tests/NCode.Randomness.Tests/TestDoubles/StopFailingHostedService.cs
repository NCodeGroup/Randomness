// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// A hosted service that starts, fails to stop and records its disposal.
/// </summary>
internal sealed class StopFailingHostedService : IHostedService, IDisposable
{
    public const string Message = "The service failed to stop.";

    public bool Disposed { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(Message));

    public void Dispose() => Disposed = true;
}
