// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NCode.Randomness.Nist.Http;

namespace NCode.Randomness.Nist;

/// <summary>
/// Registers the NIST beacon.
/// </summary>
public static class NistRandomnessBuilderExtensions
{
    extension(IRandomnessBuilder builder)
    {
        /// <summary>
        /// Registers the NIST beacon, named <c>nist</c>, with its default options: chain 2 of the NIST Randomness
        /// Beacon 2.0 served by <see cref="NistDefaults.BaseAddress"/>, verified against the certificates of
        /// <see cref="NistDefaults.CertificateIds"/>.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddNistBeacon()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddOptions<NistBeaconOptions>().ValidateOnStart();
            return builder.AddNistBeaconCore();
        }

        /// <summary>
        /// Registers the NIST beacon with options set by <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the options.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddNistBeacon(Action<NistBeaconOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            builder.Services.AddOptions<NistBeaconOptions>().Configure(configure).ValidateOnStart();
            return builder.AddNistBeaconCore();
        }

        /// <summary>
        /// Registers the NIST beacon with options bound from <paramref name="configuration"/>, such as
        /// <c>configuration.GetSection("RandomnessBeacons:Nist")</c>. An unknown key fails the host at start.
        /// </summary>
        /// <param name="configuration">The configuration section to bind.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddNistBeacon(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);
            builder
                .Services.AddOptions<NistBeaconOptions>()
                .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
                .ValidateOnStart();
            return builder.AddNistBeaconCore();
        }

        /// <summary>
        /// Registers the options validator, the NIST API client with its HTTP client and resilience policy, the
        /// certificate cache and the NIST beacon. Registering it again changes nothing.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        private IRandomnessBuilder AddNistBeaconCore()
        {
            var services = builder.Services;
            var validator = ServiceDescriptor.Singleton<
                IValidateOptions<NistBeaconOptions>,
                NistBeaconOptionsValidator
            >();
            services.TryAddEnumerable(validator);
            var registered = services.Any(descriptor => descriptor.ServiceType == typeof(NistApi));
            services.TryAddSingleton<NistApi>();
            services.TryAddSingleton<NistCertificateCache>();
            services.TryAddSingleton<NistBeacon>();
            var beacon = ServiceDescriptor.Singleton<IRandomnessBeacon, NistBeacon>(provider =>
                provider.GetRequiredService<NistBeacon>()
            );
            services.TryAddEnumerable(beacon);
            if (registered)
            {
                return builder;
            }

            services
                .AddHttpClient(NistDefaults.HttpClientName)
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

    /// <summary>
    /// Sets the named client's resilience: a 10 s attempt timeout, a 25 s total timeout, two retries and a 60 s
    /// circuit-breaker sampling window.
    /// </summary>
    /// <param name="options">The resilience options of the named client.</param>
    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    }
}
