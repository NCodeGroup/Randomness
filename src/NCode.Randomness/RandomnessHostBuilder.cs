// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NCode.Randomness;

/// <summary>
/// Composes a <see cref="RandomnessHost"/>: add beacons with the adapters' extension members, such as
/// <c>AddCurbyBeacon()</c>, then call <see cref="BuildAsync"/> once.
/// </summary>
public sealed class RandomnessHostBuilder : IRandomnessBuilder
{
    private int _built;

    internal RandomnessHostBuilder() { }

    /// <summary>
    /// Gets the services the host is built from. Add what a host would add, such as a logging provider; a
    /// registration made after <see cref="BuildAsync"/> has no effect.
    /// </summary>
    public IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Builds the host: checks every setting, as a host does at start, and starts collecting beacon pulses in the
    /// background; with <see cref="RandomnessOptions.RequireInitialPulse"/> set, it waits for the first pulse.
    /// </summary>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The started host; dispose it when done.</returns>
    /// <exception cref="InvalidOperationException">
    /// This builder has already built its host, the configuration has an unknown key, or
    /// <see cref="RandomnessOptions.RequireInitialPulse"/> is set and no beacon gave a pulse within
    /// <see cref="RandomnessOptions.InitialPulseTimeout"/>.
    /// </exception>
    /// <exception cref="OptionsValidationException">A setting is invalid.</exception>
    /// <exception cref="AggregateException">
    /// A hosted service failed to start and one it had started failed to stop; the start failure comes first.
    /// </exception>
    public async ValueTask<RandomnessHost> BuildAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _built, 1) != 0)
        {
            throw new InvalidOperationException(
                "This builder has already built its RandomnessHost; create another builder for another host."
            );
        }

        var provider = Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        return await RandomnessHost.StartAsync(provider, cancellationToken).ConfigureAwait(false);
    }
}
