// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace NCode.Randomness.DependencyInjection;

internal sealed class RandomnessBuilder(IServiceCollection services) : IRandomnessBuilder
{
    public IServiceCollection Services { get; } = services;
}
