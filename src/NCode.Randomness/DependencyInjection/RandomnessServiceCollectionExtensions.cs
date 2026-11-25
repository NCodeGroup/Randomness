// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Randomness.DependencyInjection;
using NCode.Randomness.Diagnostics;
using NCode.Randomness.Draws;
using NCode.Randomness.Entropy;
using NCode.Randomness.Generation;
using NCode.Randomness.Hosting;

namespace NCode.Randomness;

/// <summary>
/// Registers the secure generator, the beacon collector and reproducible draws.
/// </summary>
public static class RandomnessServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ISecureRandom"/> (also as <see cref="IRandomSource"/>), <see cref="IBeaconStatus"/>,
        /// <see cref="IVerifiableRandomFactory"/> and the background beacon collector, with default options. Add beacons
        /// to the returned builder, such as with <c>AddCurbyBeacon()</c>; with none, the generator runs from the
        /// operating system's entropy alone.
        /// </summary>
        /// <returns>The builder, for adding beacons.</returns>
        public IRandomnessBuilder AddRandomness()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddOptions<RandomnessOptions>().ValidateOnStart();
            return services.AddRandomnessCore();
        }

        /// <summary>
        /// Registers the randomness services as <c>AddRandomness()</c> does, with options set by
        /// <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">Sets the options.</param>
        /// <returns>The builder, for adding beacons.</returns>
        public IRandomnessBuilder AddRandomness(Action<RandomnessOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            services.AddOptions<RandomnessOptions>().Configure(configure).ValidateOnStart();
            return services.AddRandomnessCore();
        }

        /// <summary>
        /// Registers the randomness services as <c>AddRandomness()</c> does, with options bound from
        /// <paramref name="configuration"/>, such as <c>configuration.GetSection("Randomness")</c>. An unknown key
        /// fails the host at start, so a misspelled setting is never silently ignored.
        /// </summary>
        /// <param name="configuration">The configuration section to bind.</param>
        /// <returns>The builder, for adding beacons.</returns>
        public IRandomnessBuilder AddRandomness(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            services
                .AddOptions<RandomnessOptions>()
                .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
                .ValidateOnStart();
            return services.AddRandomnessCore();
        }

        private RandomnessBuilder AddRandomnessCore()
        {
            services.AddLogging();
            services.AddMetrics();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<RandomnessOptions>,
                    RandomnessOptionsValidator
                >()
            );
            services.TryAddSingleton<IEntropyInput, SystemEntropyInput>();
            services.TryAddSingleton<RandomnessMetrics>();
            services.TryAddSingleton<BeaconEntropyPool>();
            services.TryAddSingleton<IBeaconEntropy>(provider =>
                provider.GetRequiredService<BeaconEntropyPool>()
            );
            services.TryAddSingleton<BeaconTracker>();
            services.TryAddSingleton<IBeaconStatus>(provider =>
                provider.GetRequiredService<BeaconTracker>()
            );
            services.TryAddSingleton<DefaultSecureRandom>();
            services.TryAddSingleton<ISecureRandom>(provider =>
                provider.GetRequiredService<DefaultSecureRandom>()
            );
            services.TryAddSingleton<IRandomSource>(provider =>
                provider.GetRequiredService<ISecureRandom>()
            );
            services.TryAddSingleton<IVerifiableRandomFactory, DefaultVerifiableRandomFactory>();
            services.AddHostedService<BeaconCollector>();
            return new RandomnessBuilder(services);
        }
    }
}
