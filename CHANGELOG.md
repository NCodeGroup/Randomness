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
  exponential back-off with jitter, an optional `RequireInitialPulse` start check, and a pulse age limit per beacon,
  `MaxPulseAgeByBeacon` ([ADR-1008](docs/decisions/1008-pulse-staleness-is-judged-per-beacon.md)).
- `IBeaconStatus`, the health check `AddRandomnessBeacons()`, and the metrics `ncode.randomness.beacon.pulses` and
  `ncode.randomness.generator.reseeds`.
- Reproducible draws: `IVerifiableRandomFactory` and `VerifiableRandom`, algorithm `ncode-randomness-draw/1`, specified
  in `docs/draws.md` with a reference implementation.
- `NCode.Randomness.Curby`: the CURBy beacon, `AddCurbyBeacon()`, over the CURBy-RNG Twine chain, and the CURBy-Q
  beacon, `AddCurbyQuantumBeacon()`, named `curby-q`, over the rounds of the CURBy-Q (DIRNG) chain, with settings in
  `RandomnessBeacons:Curby`
  ([#1](https://github.com/NCodeGroup/Randomness/issues/1),
  [ADR-1007](docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)).
- `NCode.Randomness.Nist`: the NIST Randomness Beacon 2.0 beacon, `AddNistBeacon()`, named `nist`, over chain 2 of
  `https://beacon.nist.gov/beacon/2.0/`, with settings in `RandomnessBeacons:Nist`
  ([#2](https://github.com/NCodeGroup/Randomness/issues/2),
  [ADR-1009](docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)).
- `NCode.Randomness.Drand`: the drand beacon, `AddDrandBeacon()`, named `drand`, over the League of Entropy's quicknet
  chain, with settings in `RandomnessBeacons:Drand`
  ([#2](https://github.com/NCodeGroup/Randomness/issues/2),
  [ADR-1010](docs/decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)).
- The CURBy and NIST adapters fetch each chain or certificate once for every caller, under its own 30-second timeout,
  and remember a failed fetch for `RandomnessBeacons:Curby:ChainRetryDelay` or
  `RandomnessBeacons:Nist:CertificateRetryDelay` (default 1 minute) before they try it again.

### Security

- Beacon pulses are only additional input to the secure generator, so its output is at least as unpredictable as the
  operating system's generator, whatever a beacon publishes.
- CURBy pulses are verified before use: the chain hashes to a pinned content identifier, every document hashes to its
  content identifier and has only the members Twine defines, every signature is canonical RS256 by the chain's key,
  and every salt matches its precommitment, so no server can change a pulse's randomness without breaking a check.
- CURBy-Q rounds are verified before use: the chain hashes to a pinned content identifier, every pulse of the round is
  signed by its key and follows the one before, a pulse of the pinned Bell chain answers the request with the
  precommitted data hash before the precommit, and the seed expands by SHAKE256 from the seed-chain pulse after the
  precommit to the parameters the round commits to. The Bell-test certificate and the Trevisan extraction are verified
  only out of process, by CURBy's `curby_verify`
  ([#1](https://github.com/NCodeGroup/Randomness/issues/1),
  [ADR-1007](docs/decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)).
- NIST pulses are verified before use: the certificate hashes to a pinned identifier and covers the pulse's timestamp,
  the RSA signature verifies over the pulse's signed fields, the output value is the hash of those fields and the
  signature, and the pulse chains to its predecessor's output and matches its precommitment
  ([#2](https://github.com/NCodeGroup/Randomness/issues/2),
  [ADR-1009](docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)).
- drand rounds are verified before use: the BLS signature of the round number, hashed to the curve as RFC 9380
  specifies, verifies against the pinned quicknet group key, and the randomness is the SHA-256 of that signature
  ([#2](https://github.com/NCodeGroup/Randomness/issues/2),
  [ADR-1010](docs/decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)).
- Each beacon adapter's `BaseAddress` must be an absolute `https` URL ending in `/`, with no user info, query or
  fragment, so no credential written into a configured address reaches a logged request.
- Pulses that roll back, switch chains, reuse an accepted round with other content, or are out of date by their
  timestamp are refused.
- Dependency injection registers the secure generator only as `ISecureRandom`, never as the plain `IRandomSource`, and
  a disposed generator or draw throws instead of returning bytes.
- Generator state lives in pinned memory, served bytes are erased from the buffers, and state is zeroed on disposal.
