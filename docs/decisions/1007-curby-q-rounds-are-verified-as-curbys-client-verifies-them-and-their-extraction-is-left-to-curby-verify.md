# 1007. CURBy-Q rounds are verified as CURBy's client verifies them, and their extraction is left to curby_verify

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers
- **Amends:** 1005

## Context

CURBy-Q is CURBy's quantum chain: a DIRNG round turns the data of a Bell experiment into 512 bits through a Trevisan
extractor. It publishes through Twine, like CURBy-RNG, whose pulse verification is in
[ADR-1003](1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md).
Facts read from `https://api.entwine.me` on 2026-11-25:

- Three chains take part, each signing with RS256 and a 4096-bit key. The CURBy-Q chain has the specification
  `twine/1.0.x/diqrng/1.0.x`; the Bell chain ("NIST-bell-experiment") `twine/1.0.x/bell/1.0.x`; the seed chain
  ("drand Mirror") `twine/1.0.x/mirror/1.0.x`.
- `GET curbyq/round/{n}` and `GET curbyq/round/latest` answer the round's pulses on the CURBy-Q chain, consecutive and
  in stage order `request`, `precommit`, then `randomness` or `error`; an unknown round answers `[]`. The request holds
  the extractor's `parameters` (`nBitsOut` 512 and floats); the precommit holds the Bell data's `dataHash`, a `rule`
  (`after: self`, `count`), `seedAlgorithm` `shake256`, a `seedChain` link and a `seedLength` in bits; the randomness
  pulse holds `dataHash`, `paramsHash` and the 64 `randomness` bytes.
- `GET curbyq/round/{n}/params` answers the JSON document whose SHA3-512 is `paramsHash` and whose `seed` is SHAKE256 of
  the seed pulse's digest, expanded to `seedLength` bits, in base64.
- The precommit mixes in a Bell pulse at or after the response: the Bell pulse that links the request in `ref` and
  holds the precommit's `dataHash`. The seed pulse is the pulse `rule.count` steps after the first seed-chain pulse
  that mixes in the precommit, and the randomness pulse mixes it in. Rounds 358, 10000, 20000, 27000, 28296 and 28297
  show this.
- Request and Bell pulses hold JSON floats such as `0.014989141965498868` and `1.0842021724855042e-20`; their CIDs
  recompute only when each float is a DAG-CBOR 64-bit float (`0xfb` and 8 bytes, big-endian).
- A round's raw Bell data is large (9,003,748 bytes for round 28297), and the extraction runs over about 30 million
  bits with a 249,224-bit seed.
- As of 2026-11-25, the newest complete round is 28297 (randomness at 2025-08-22T19:42:38.156Z); CURBy-Q has published
  nothing since the request of round 28298 that day. While it runs, it completes a round about every 13 minutes (as of
  2026-11-25).
- A round that is not cached takes about nine requests, an estimate as of 2026-11-25: the round, its parameters, the
  crawls of the Bell and seed chains, and the chain documents the first time.

CURBy's client library (`@buff-beacon-project/curby-client` 1.2.0, `src/dirng.ts`) documents that it does not validate
the DIRNG certificate or the extraction, and leaves them to `curby_verify`. That tool runs the extractor server of
`buff-beacon-project/trevisan_python_interface` in Docker, over ZeroMQ. The extractor is libtrevisan, licensed
GPL-2.0-or-later, as NIST modified it; the repositories that hold the modified code have no license (GitHub, read
2026-11-25).

## Decision

`NCode.Randomness.Curby` reads CURBy-Q rounds directly as a second beacon, `CurbyQuantumBeacon`, and verifies each round
as far as CURBy's client does and as far as the served data allows, without the extraction.

- **The beacon.** `AddCurbyQuantumBeacon()` registers it under the name `curby-q`. It shares the Twine verification
  code, the named `HttpClient` and the settings section `RandomnessBeacons:Curby`; `CurbyBeaconOptions` has
  `QuantumChainId` and `BellChainId` for its chains. The core does not change.
- **The pulse.** A round's `BeaconPulse` has the DIRNG round number as `Round`, the randomness pulse's CID as `Id`, the
  CURBy-Q chain's CID as `Chain`, the randomness pulse's timestamp as `Timestamp` and its `randomness` (the extractor's
  512 bits) as `Randomness`.
- **The checks.** Each refuses the round with `InvalidPulse`:
  - the CURBy-Q chain hashes to the pinned CID and follows `diqrng/1.x`;
  - each pulse of the round is verified as ADR-1003 verifies a pulse (CID, strict shape, canonical RS256 by the chain's
    key), the pulses are consecutive and they carry one round number;
  - the Bell chain is the pinned `CurbyDefaults.BellChainId`, of the family `bell`; within 10 Bell pulses back from the
    one the precommit mixes in, one answers the request with the precommit's `dataHash`, and the randomness pulse
    carries that `dataHash` too;
  - the seed chain is the one the signed precommit names (any `twine/1.x` family); within 10 seed pulses back from the
    one the randomness pulse mixes in, the walk reaches a pulse that does not mix in the precommit, and the seed pulse
    is the pulse `rule.count` steps after the first that does; `rule.after` is `self` and `seedAlgorithm` is
    `shake256`;
  - the parameters hash to `paramsHash`, and SHAKE256 of the seed pulse's digest to `seedLength` bits equals their
    `seed`;
  - every crawled pulse is the one requested by its CID and precedes the one after it.
- **The seed selection** follows the served data, not the client library 1.2.0: its `strand.at(-rule.count)` picks the
  first pulse that mixes in the precommit, whose expansion matches none of the six rounds checked; the pulse after it
  matches all of them.
- **Rounds without randomness.** A round with an `error` pulse, or without a randomness pulse, gives no pulse:
  `GetPulseAsync` fails with `PulseNotFound` and `GetLatestPulseAsync` with `Unavailable`, naming the reason.
- **The extraction is not verified in process.** Neither the Trevisan extraction nor the Bell-test hypothesis test runs.
  The package README names `curby_verify` for auditing a round.
- **Floats amend ADR-1005.** The DAG-JSON subset accepts numbers with a fraction or an exponent as 64-bit floats, and
  the DAG-CBOR encoder writes them as 64-bit IEEE 754 (`0xfb`). A float that is not finite is refused. `Sha3.Shake256`
  uses the platform's SHAKE256 where it has one and a managed Keccak sponge elsewhere, as SHA3-512 does.

## Options considered

- **Port the extractor.** The code is GPL-2.0-or-later as NIST modified it, in repositories with no license, so it
  cannot be ported into this Apache-2.0 package. A clean-room version has to match its output bit for bit with no
  specification, and each round needs 9 MB of raw data and an extraction from 30 million bits, which costs CPU on
  every verification.
- **Run `curby_verify` out of process.** It needs Docker and the raw data; neither is a dependency a library can impose.
- **Read CURBy-Q only through CURBy-RNG's mixins.** This is what the default beacon does, but it never exposes a
  CURBy-Q round's own number, pulse or randomness, which is what an audit by `curby_verify` takes.

Not verifying the extraction costs little: a beacon's bytes are only additional input to the generator
([ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)), so certifying
them changes nothing for secrets.

## Consequences

- An auditor runs `curby_verify` on the round a draw used; the library vouches for the chain of signatures and links,
  not for the quantum certificate.
- The CURBy-Q registration allows `curby-q` pulses up to 30 minutes old, because a round takes longer than the default
  `MaxPulseAge`; the host's own `Randomness:MaxPulseAgeByBeacon:curby-q` wins
  ([ADR-1008](1008-pulse-staleness-is-judged-per-beacon.md)). Context has the cadence.
- A stalled chain reports stale pulses and degrades the health check, as it does for any beacon; draws over published
  rounds are unaffected. Context has the date of the newest round.
- A round that is not cached costs a crawl of the Bell and seed chains, and Context has the request count. A repeated
  fetch of the last verified round makes no crawl.
- If CURBy publishes a specification or a licensed extractor, verifying the extraction in process is a later decision.

## References

- [ADR-1000](1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md),
  [ADR-1003](1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md) and
  [ADR-1005](1005-twine-verification-is-implemented-in-the-adapter-without-an-ipld-dependency.md).
- https://github.com/buff-beacon-project/curby-js-client (`src/dirng.ts`).
- https://github.com/buff-beacon-project/curby_verify.
- https://github.com/buff-beacon-project/trevisan_python_interface.
- `src/NCode.Randomness.Curby/Quantum/`.
