# Changelog

All notable changes to this project are documented in this file. The format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/). Until its first stable release, `## [Unreleased]` describes what the
product does.

## [Unreleased]

### Added

- `ISecureRandom`, registered by `AddRandomness()`: an NIST SP 800-90A HMAC_DRBG with HMAC-SHA-512 per processor,
  buffered, seeded and reseeded from the operating system's CSPRNG, and reseeded with every beacon pulse mixed in, after
  `ReseedInterval` and after `ReseedAfterBytes`.
- `SystemRandomSource.Instance`: the operating system's CSPRNG as an `ISecureRandom`.
- `IRandomSource` extension members: unbiased `NextInteger<T>` for every integer type from `byte` to `UInt128`, with
  or without bounds, `NextDouble`, `NextSingle`, `NextBoolean`, `NextGuid` (version 4), `GetBytes`, `Shuffle`,
  `GetItem`, `GetItems`, `GetString` and `AsRandom`.
- `IRandomnessBeacon` and `BeaconPulse`, the port a beacon adapter implements, registered with `AddBeacon<TBeacon>()`.
- A background beacon collector with `Continuous`, `InitializeOnly` and `Offline` modes, per-fetch timeouts,
  exponential back-off with jitter, and an optional `RequireInitialPulse` start check.
- `IBeaconStatus`, the health check `AddRandomnessBeacons()`, and the metrics `ncode.randomness.beacon.pulses` and
  `ncode.randomness.generator.reseeds`.
- Reproducible draws: `IVerifiableRandomFactory` and `VerifiableRandom`, algorithm `ncode-randomness-draw/1`, specified
  in `docs/draws.md` with a reference implementation.
- `NCode.Randomness.Curby`: the CURBy beacon, `AddCurbyBeacon()`, over the CURBy-RNG Twine chain, with settings in
  `RandomnessBeacons:Curby`.

### Security

- Beacon pulses are only additional input to the secure generator, so its output is at least as unpredictable as the
  operating system's generator, whatever a beacon publishes.
- CURBy pulses are verified before use: the chain hashes to a pinned content identifier, every document hashes to its
  content identifier and has only the members Twine defines, every signature is canonical RS256 by the chain's key,
  and every salt matches its precommitment, so no server can change a pulse's randomness without breaking a check.
- Pulses that roll back, switch chains, reuse an accepted round with other content, or are out of date by their
  timestamp are refused.
- Dependency injection registers the secure generator only as `ISecureRandom`, never as the plain `IRandomSource`, and
  a disposed generator or draw throws instead of returning bytes.
- Generator state lives in pinned memory, served bytes are erased from the buffers, and state is zeroed on disposal.
