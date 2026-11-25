// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using NCode.Randomness.Diagnostics;

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// A meter factory of its own per test, so a collector sees only that test's measurements.
/// </summary>
internal sealed class Telemetry : IDisposable
{
    public Telemetry()
    {
        Provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        MeterFactory = Provider.GetRequiredService<IMeterFactory>();
        Metrics = new RandomnessMetrics(MeterFactory);
    }

    public IMeterFactory MeterFactory { get; }

    public RandomnessMetrics Metrics { get; }

    private ServiceProvider Provider { get; }

    public void Dispose() => Provider.Dispose();
}
