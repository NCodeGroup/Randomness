// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NCode.Randomness.Curby.Http;

namespace NCode.Randomness.Curby;

/// <summary>
/// Registers the CURBy beacons.
/// </summary>
public static class CurbyRandomnessBuilderExtensions
{
    private static TimeSpan QuantumMaxPulseAge { get; } = TimeSpan.FromMinutes(30);

    extension(IRandomnessBuilder builder)
    {
        /// <summary>
        /// Registers the CURBy beacon, named <c>curby</c>, with its default options: the CURBy-RNG chain served by
        /// <see cref="CurbyDefaults.BaseAddress"/>.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyBeacon()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddOptions<CurbyBeaconOptions>().ValidateOnStart();
            return builder.AddCurbyBeaconCore();
        }

        /// <summary>
        /// Registers the CURBy beacon with options set by <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the options.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyBeacon(Action<CurbyBeaconOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            builder
                .Services.AddOptions<CurbyBeaconOptions>()
                .Configure(configure)
                .ValidateOnStart();
            return builder.AddCurbyBeaconCore();
        }

        /// <summary>
        /// Registers the CURBy beacon with options bound from <paramref name="configuration"/>, such as
        /// <c>configuration.GetSection("RandomnessBeacons:Curby")</c>. An unknown key fails the host at start.
        /// </summary>
        /// <param name="configuration">The configuration section to bind.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyBeacon(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);
            builder
                .Services.AddOptions<CurbyBeaconOptions>()
                .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
                .ValidateOnStart();
            return builder.AddCurbyBeaconCore();
        }

        /// <summary>
        /// Registers the CURBy-Q beacon, named <c>curby-q</c>, with its default options: the CURBy-Q chain and the
        /// Bell chain served by <see cref="CurbyDefaults.BaseAddress"/>. It allows <c>curby-q</c> pulses up to 30
        /// minutes old, since CURBy-Q completes a round about every 13 minutes, unless
        /// <c>Randomness:MaxPulseAgeByBeacon:curby-q</c> or a longer <c>Randomness:MaxPulseAge</c> is set.
        /// </summary>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyQuantumBeacon()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddOptions<CurbyBeaconOptions>().ValidateOnStart();
            return builder.AddCurbyQuantumBeaconCore();
        }

        /// <summary>
        /// Registers the CURBy-Q beacon with options set by <paramref name="configure"/>. It allows <c>curby-q</c>
        /// pulses up to 30 minutes old, since CURBy-Q completes a round about every 13 minutes, unless
        /// <c>Randomness:MaxPulseAgeByBeacon:curby-q</c> or a longer <c>Randomness:MaxPulseAge</c> is set.
        /// </summary>
        /// <param name="configure">Sets the options.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyQuantumBeacon(Action<CurbyBeaconOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            builder
                .Services.AddOptions<CurbyBeaconOptions>()
                .Configure(configure)
                .ValidateOnStart();
            return builder.AddCurbyQuantumBeaconCore();
        }

        /// <summary>
        /// Registers the CURBy-Q beacon with options bound from <paramref name="configuration"/>, such as
        /// <c>configuration.GetSection("RandomnessBeacons:Curby")</c>. An unknown key fails the host at start. It
        /// allows <c>curby-q</c> pulses up to 30 minutes old, since CURBy-Q completes a round about every 13
        /// minutes, unless <c>Randomness:MaxPulseAgeByBeacon:curby-q</c> or a longer <c>Randomness:MaxPulseAge</c> is
        /// set.
        /// </summary>
        /// <param name="configuration">The configuration section to bind.</param>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddCurbyQuantumBeacon(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);
            builder
                .Services.AddOptions<CurbyBeaconOptions>()
                .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
                .ValidateOnStart();
            return builder.AddCurbyQuantumBeaconCore();
        }

        private void AddCurbyServices()
        {
            var services = builder.Services;
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<CurbyBeaconOptions>,
                    CurbyBeaconOptionsValidator
                >()
            );
            if (services.Any(descriptor => descriptor.ServiceType == typeof(TwineApi)))
            {
                return;
            }

            services.AddSingleton<TwineApi>();
            services.TryAddSingleton<TwineChainCache>();
            services
                .AddHttpClient(CurbyDefaults.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(static () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        AutomaticDecompression = System.Net.DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    }
                )
                .AddStandardResilienceHandler(ConfigureResilience);
        }

        private IRandomnessBuilder AddCurbyQuantumBeaconCore()
        {
            builder.AddCurbyServices();
            var services = builder.Services;
            services.AddOptions<RandomnessOptions>().PostConfigure(UseQuantumMaxPulseAge);
            services.TryAddSingleton<CurbyQuantumBeacon>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRandomnessBeacon, CurbyQuantumBeacon>(provider =>
                    provider.GetRequiredService<CurbyQuantumBeacon>()
                )
            );
            return builder;
        }

        private IRandomnessBuilder AddCurbyBeaconCore()
        {
            builder.AddCurbyServices();
            var services = builder.Services;
            services.TryAddSingleton<CurbyBeacon>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRandomnessBeacon, CurbyBeacon>(provider =>
                    provider.GetRequiredService<CurbyBeacon>()
                )
            );
            return builder;
        }
    }

    private static void UseQuantumMaxPulseAge(RandomnessOptions options)
    {
        if (options.MaxPulseAgeByBeacon is null || options.MaxPulseAge >= QuantumMaxPulseAge)
        {
            return;
        }

        options.MaxPulseAgeByBeacon.TryAdd(CurbyDefaults.QuantumBeaconName, QuantumMaxPulseAge);
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    }
}
