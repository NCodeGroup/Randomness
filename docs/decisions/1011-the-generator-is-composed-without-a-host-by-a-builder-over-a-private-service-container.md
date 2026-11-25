# 1011. The generator is composed without a host by a builder over a private service container

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

`AddRandomness()` composes the secure generator, the beacon tracker, the background collector and reproducible draws
in an `IServiceCollection`, and the generic host validates the options and runs the collector. Each beacon adapter
registers into the same collection through an extension member on `IRandomnessBuilder`, with its options, their
validator and a named `HttpClient` with a resilience policy. A console tool, or a library that doesn't use dependency
injection, has no host, and without one only `SystemRandomSource.Instance` is available, with no beacon input
([#4](https://github.com/NCodeGroup/Randomness/issues/4)).

## Decision

`RandomnessHost.CreateBuilder()` returns a `RandomnessHostBuilder`, an `IRandomnessBuilder` over a service collection
of its own on which `AddRandomness()` has run, so every adapter's extension member, such as `AddCurbyBeacon()`,
registers into it unchanged. `CreateBuilder` takes nothing, an `Action<RandomnessOptions>` or a configuration section,
as `AddRandomness` does.

- **Build and start in one call.** `BuildAsync` builds the service provider with `ValidateOnBuild` and
  `ValidateScopes`, runs the options' start validation (`IAsyncStartupValidator`) as the generic host does, and starts
  every registered `IHostedService` in registration order, so `RequireInitialPulse` holds as it does in a host. When any
  of that fails, it stops the services it started, in reverse order, disposes the provider and rethrows. A builder
  builds one host.
- **A small surface.** `RandomnessHost` exposes `SecureRandom`, `BeaconStatus` and `VerifiableRandomFactory`, typed by
  their interfaces, so the generator is reachable only as an `ISecureRandom`
  ([ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)). The
  builder's `Services` stays open for what a host would add, such as a logging provider or a `TimeProvider`.
- **Asynchronous disposal only.** `RandomnessHost` implements `IAsyncDisposable` and not `IDisposable`: stopping the
  collector is asynchronous, and blocking on a task is banned. `DisposeAsync` stops the hosted services in reverse
  order, waiting up to 30 seconds, the generic host's default `HostOptions.ShutdownTimeout` (read 2026-11-25), then
  disposes the provider, which disposes the generator and zeroes its state. A second call does nothing.
- **No host lifetime.** `RandomnessHost` calls only `StartAsync` and `StopAsync`: it has no console lifetime, no
  `IHostApplicationLifetime` and no `IHostedLifecycleService` hooks, none of which a part of this library uses.

## Options considered

- **Constructors wired by hand, without a container.** Every adapter would need a second composition path for its
  options, their validation and its resilient `HttpClient`, and the core would grow a factory per vendor, against the
  adapter contract in [beacon-adapters.md](../beacon-adapters.md).
- **A generic host in the caller** (`Host.CreateEmptyApplicationBuilder`). It adds Microsoft.Extensions.Hosting, and
  its configuration, logging and lifetime defaults, to a console tool or a library that wants none of them.
- **`IDisposable` as well.** A synchronous `Dispose` would have to block on the collector's stop.

## Consequences

- An application without dependency injection gets the same generator, the same beacons and the same validation as a
  host, and the adapters need no change
  ([ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)).
- The core package references Microsoft.Extensions.DependencyInjection, the container, as well as its abstractions,
  and its own awaits run without the caller's synchronization context.
- The service container shows through `RandomnessHostBuilder.Services`; a registration made there after `BuildAsync`
  has no effect.
- A caller that can't `await` can't dispose the host; `SystemRandomSource.Instance` needs no disposal.

## References

- `src/NCode.Randomness/RandomnessHost.cs` and `src/NCode.Randomness/RandomnessHostBuilder.cs`.
- [ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md) and
  [ADR-1002](1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md).
