// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// A hosted service that counts how many times it was started and stopped.
/// </summary>
internal sealed class RecordingHostedService : IHostedService
{
    public int Starts { get; private set; }

    public int Stops { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Starts++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Stops++;
        return Task.CompletedTask;
    }
}
