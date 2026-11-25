// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Randomness;

/// <summary>
/// Registers randomness beacons.
/// </summary>
public static class RandomnessBuilderExtensions
{
    extension(IRandomnessBuilder builder)
    {
        /// <summary>
        /// Registers <typeparamref name="TBeacon"/> as a singleton beacon. Each beacon class registers once; its
        /// <see cref="IRandomnessBeacon.Name"/> must be unique among the registered beacons.
        /// </summary>
        /// <typeparam name="TBeacon">The beacon adapter.</typeparam>
        /// <returns>The builder, for chaining.</returns>
        public IRandomnessBuilder AddBeacon<TBeacon>()
            where TBeacon : class, IRandomnessBeacon
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRandomnessBeacon, TBeacon>()
            );
            return builder;
        }
    }
}
