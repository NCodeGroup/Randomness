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
    /// <summary>
    /// Gets how long stopping the hosted services may take: the generic host's default shutdown timeout.
    /// </summary>
    private static TimeSpan ShutdownTimeout { get; } = TimeSpan.FromSeconds(30);

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
        catch (Exception startFailure)
        {
            using var timeout = new CancellationTokenSource(ShutdownTimeout);
            await CleanUpAfterFailedStartAsync(provider, started, startFailure, timeout.Token)
                .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Stops the services a failed start started and disposes the provider.
    /// </summary>
    /// <remarks>
    /// The start failure is the cause the caller needs, so a stop failure never hides it, and the provider is
    /// disposed whatever fails.
    /// </remarks>
    /// <param name="provider">The provider to dispose.</param>
    /// <param name="started">The services the start started, in start order.</param>
    /// <param name="startFailure">The failure that ended the start.</param>
    /// <param name="cancellationToken">Bounds how long the stops may take.</param>
    /// <exception cref="AggregateException">
    /// A service failed to stop; it holds the start failure first, then the stop failures.
    /// </exception>
    private static async ValueTask CleanUpAfterFailedStartAsync(
        ServiceProvider provider,
        IReadOnlyList<IHostedService> started,
        Exception startFailure,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await StopAsync(started, cancellationToken).ConfigureAwait(false);
        }
        catch (AggregateException stopFailure)
        {
            throw new AggregateException([startFailure, .. stopFailure.InnerExceptions]);
        }
        finally
        {
            await provider.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops the services in reverse order, calling every one even when an earlier stop throws.
    /// </summary>
    /// <param name="services">The services to stop, in start order.</param>
    /// <param name="cancellationToken">Bounds how long the stops may take.</param>
    /// <exception cref="AggregateException">One or more services failed to stop; it holds every failure.</exception>
    private static async ValueTask StopAsync(
        IReadOnlyList<IHostedService> services,
        CancellationToken cancellationToken
    )
    {
        List<Exception>? failures = null;
        for (var index = services.Count - 1; index >= 0; index--)
        {
            try
            {
                await services[index].StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }

    /// <summary>
    /// 1 once <see cref="DisposeAsync"/> has run; set with <see cref="Interlocked"/>.
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Creates the host over a started provider and the hosted services it started, in start order.
    /// </summary>
    /// <param name="provider">The provider the services were resolved from.</param>
    /// <param name="hostedServices">The started services, in start order.</param>
    private RandomnessHost(ServiceProvider provider, ImmutableArray<IHostedService> hostedServices)
    {
        Provider = provider;
        HostedServices = hostedServices;
        SecureRandom = provider.GetRequiredService<ISecureRandom>();
        BeaconStatus = provider.GetRequiredService<IBeaconStatus>();
        VerifiableRandomFactory = provider.GetRequiredService<IVerifiableRandomFactory>();
    }

    /// <summary>
    /// Gets the provider that owns the generator and the beacon services.
    /// </summary>
    private ServiceProvider Provider { get; }

    /// <summary>
    /// Gets the started hosted services, in start order.
    /// </summary>
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
    /// Stops the beacon collection, waiting up to 30 seconds, and disposes the generator, zeroing its state. A second
    /// call does nothing.
    /// </summary>
    /// <exception cref="AggregateException">
    /// A hosted service failed to stop; every other service was still stopped and the generator disposed.
    /// </exception>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(ShutdownTimeout);
        try
        {
            await StopAsync(HostedServices, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            await Provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
