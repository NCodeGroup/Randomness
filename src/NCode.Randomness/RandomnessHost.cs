// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NCode.Randomness;

/// <summary>
/// The secure generator, the background beacon collection and reproducible draws, composed without dependency
/// injection, for console tools and libraries that run without a host.
/// </summary>
/// <remarks>
/// Create one with <see cref="CreateBuilder()"/>, add beacons to the builder, then call
/// <see cref="RandomnessHostBuilder.BuildAsync"/>. Keep one instance for the life of the application, and dispose it
/// to stop the beacon collection and zero the generator's state.
/// </remarks>
public sealed class RandomnessHost : IAsyncDisposable
{
    private static TimeSpan ShutdownTimeout { get; } = TimeSpan.FromSeconds(30);

    private int _disposed;

    private RandomnessHost(ServiceProvider provider, ImmutableArray<IHostedService> hostedServices)
    {
        Provider = provider;
        HostedServices = hostedServices;
        SecureRandom = provider.GetRequiredService<ISecureRandom>();
        BeaconStatus = provider.GetRequiredService<IBeaconStatus>();
        VerifiableRandomFactory = provider.GetRequiredService<IVerifiableRandomFactory>();
    }

    private ServiceProvider Provider { get; }

    private ImmutableArray<IHostedService> HostedServices { get; }

    /// <summary>
    /// Gets the secure generator.
    /// </summary>
    public ISecureRandom SecureRandom { get; }

    /// <summary>
    /// Gets the health of the beacon collection.
    /// </summary>
    public IBeaconStatus BeaconStatus { get; }

    /// <summary>
    /// Gets the factory for reproducible draws.
    /// </summary>
    public IVerifiableRandomFactory VerifiableRandomFactory { get; }

    /// <summary>
    /// Creates a builder with default options. Add beacons to it, such as with <c>AddCurbyBeacon()</c>; with none,
    /// the generator runs from the operating system's entropy alone.
    /// </summary>
    /// <returns>The builder, for adding beacons.</returns>
    public static RandomnessHostBuilder CreateBuilder()
    {
        var builder = new RandomnessHostBuilder();
        builder.Services.AddRandomness();
        return builder;
    }

    /// <summary>
    /// Creates a builder as <see cref="CreateBuilder()"/> does, with options set by <paramref name="configure"/>.
    /// </summary>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The builder, for adding beacons.</returns>
    public static RandomnessHostBuilder CreateBuilder(Action<RandomnessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new RandomnessHostBuilder();
        builder.Services.AddRandomness(configure);
        return builder;
    }

    /// <summary>
    /// Creates a builder as <see cref="CreateBuilder()"/> does, with options bound from
    /// <paramref name="configuration"/>, such as <c>configuration.GetSection("Randomness")</c>. An unknown key fails
    /// <see cref="RandomnessHostBuilder.BuildAsync"/>, so a misspelled setting is never silently ignored.
    /// </summary>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <returns>The builder, for adding beacons.</returns>
    public static RandomnessHostBuilder CreateBuilder(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var builder = new RandomnessHostBuilder();
        builder.Services.AddRandomness(configuration);
        return builder;
    }

    /// <summary>
    /// Validates the options, starts every hosted service in registration order, and returns the started host. On a
    /// failure, stops the services it started and disposes the provider.
    /// </summary>
    internal static async ValueTask<RandomnessHost> StartAsync(
        ServiceProvider provider,
        CancellationToken cancellationToken
    )
    {
        var started = new List<IHostedService>();
        try
        {
            if (provider.GetService<IAsyncStartupValidator>() is { } validator)
            {
                await validator.ValidateAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var service in provider.GetServices<IHostedService>())
            {
                started.Add(service);
                await service.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            return new RandomnessHost(provider, [.. started]);
        }
        catch
        {
            await StopAsync(started).ConfigureAwait(false);
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Stops the beacon collection, waiting up to 30 seconds, and disposes the generator, zeroing its state. A second
    /// call does nothing.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await StopAsync(HostedServices).ConfigureAwait(false);
        }
        finally
        {
            await Provider.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask StopAsync(IReadOnlyList<IHostedService> services)
    {
        using var timeout = new CancellationTokenSource(ShutdownTimeout);
        for (var index = services.Count - 1; index >= 0; index--)
        {
            await services[index].StopAsync(timeout.Token).ConfigureAwait(false);
        }
    }
}
