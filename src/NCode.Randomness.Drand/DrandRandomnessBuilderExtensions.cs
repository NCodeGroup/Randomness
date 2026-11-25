// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NCode.Randomness.Drand.Http;
using NCode.Randomness.Drand.Rounds;

namespace NCode.Randomness.Drand;

/// <summary>
/// Registers the drand beacon.
/// </summary>
public static class DrandRandomnessBuilderExtensions
{
    extension(IRandomnessBuilder builder)
    {
        /// <summary>
        /// Registers the drand beacon, named <c>drand</c>, with its default options: the quicknet chain served by
        /// <see cref="DrandDefaults.BaseAddress"/>, verified against the chain's pinned group public key.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddDrandBeacon()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddOptions<DrandBeaconOptions>().ValidateOnStart();
            return builder.AddDrandBeaconCore();
        }

        /// <summary>
        /// Registers the drand beacon with options set by <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the options.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddDrandBeacon(Action<DrandBeaconOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            builder
                .Services.AddOptions<DrandBeaconOptions>()
                .Configure(configure)
                .ValidateOnStart();
            return builder.AddDrandBeaconCore();
        }

        /// <summary>
        /// Registers the drand beacon with options bound from <paramref name="configuration"/>, such as
        /// <c>configuration.GetSection("RandomnessBeacons:Drand")</c>. An unknown key fails the host at start.
        /// </summary>
        /// <param name="configuration">The configuration section to bind.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddDrandBeacon(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);
            builder
                .Services.AddOptions<DrandBeaconOptions>()
                .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
                .ValidateOnStart();
            return builder.AddDrandBeaconCore();
        }

        /// <summary>
        /// Registers the options validator, the drand API client with its HTTP client and resilience policy, the
        /// pinned quicknet chain and the drand beacon. Registering it again changes nothing.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        private IRandomnessBuilder AddDrandBeaconCore()
        {
            var services = builder.Services;
            var validator = ServiceDescriptor.Singleton<
                IValidateOptions<DrandBeaconOptions>,
                DrandBeaconOptionsValidator
            >();
            services.TryAddEnumerable(validator);
            var registered = services.Any(descriptor => descriptor.ServiceType == typeof(DrandApi));
            services.TryAddSingleton<DrandApi>();
            services.TryAddSingleton(DrandChain.Quicknet);
            services.TryAddSingleton<DrandBeacon>();
            var beacon = ServiceDescriptor.Singleton<IRandomnessBeacon, DrandBeacon>(provider =>
                provider.GetRequiredService<DrandBeacon>()
            );
            services.TryAddEnumerable(beacon);
            if (registered)
            {
                return builder;
            }

            services
                .AddHttpClient(DrandDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(static () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        AutomaticDecompression = System.Net.DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    }
                )
                .AddStandardResilienceHandler(ConfigureResilience);
            return builder;
        }
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    }
}
