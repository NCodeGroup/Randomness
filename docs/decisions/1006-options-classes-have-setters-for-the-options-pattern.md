# 1006. Options classes have setters for the options pattern

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers
- **Refines:** project-template's C# convention that an options class has `init` or get-only members

## Context

`AddRandomness(options => options.Mode = BeaconMode.Offline)` and `AddCurbyBeacon(options => ...)` take an
`Action<TOptions>`, the shape the C# conventions require for a configuration callback. A callback runs after the
object is constructed, so it cannot assign an `init` property.

## Decision

`RandomnessOptions` and `CurbyBeaconOptions` expose `{ get; set; }` properties. They are still sealed, correct by
default, bound strictly (an unknown key fails the host) and validated when the host starts.

## Consequences

- Every other data type in the libraries keeps `init` or get-only members.

## References

- `src/NCode.Randomness/RandomnessOptions.cs`, `src/NCode.Randomness.Curby/CurbyBeaconOptions.cs`.
