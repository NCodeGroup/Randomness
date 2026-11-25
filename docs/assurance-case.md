# Assurance Case

This page states NCode.Randomness's security requirements, the threats it considers, and why the design and the way it
is built meet them. [architecture.md](architecture.md) describes the components; [README.md](../README.md) is the
user-facing summary.

## Security Requirements

- **R1.** `ISecureRandom` output is unpredictable to anyone who does not hold the process's memory, including anyone
  who reads, forges, replays or withholds every beacon pulse.
- **R2.** No byte of a beacon pulse is used unless the pulse passed its vendor's verification against a pinned trust
  anchor.
- **R3.** A pulse that rolls back, equivocates or is out of date by its own timestamp is not mixed in.
- **R4.** A beacon outage never stops, blocks or weakens generation, and is visible in logs, metrics and health.
- **R5.** A draw is reproducible by anyone from its pulse, its context and the published algorithm.
- **R6.** Generator state is zeroed when it is disposed, and every byte a shard serves is erased from its buffer.

Users must not rely on: a draw's values being secret (they are public by design); beacon input making secret output
"quantum" (secret output is computationally secure); the generator surviving disclosure of the process's memory, which
exposes up to 511 buffered bytes per shard.

## Threat Model

The trust boundary is the process: the operating system's CSPRNG and the pinned trust anchors (CURBy's chain
identifiers, NIST's certificate identifiers and drand's group public key) are trusted; the network, the beacon API, any
mirror, and the beacon operator's future behavior are not.

| Threat | In scope | Countered by |
| --- | --- | --- |
| Attacker reads the beacon and predicts secret output | Yes | R1: beacon pulses are only additional input to an OS-seeded HMAC_DRBG ([ADR-1000](decisions/1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)) |
| A compromised API or mirror serves forged pulses or a forged key | Yes | R2: the chain CID is pinned and fixes the key; every document's CID and RS256 signature is checked ([ADR-1003](decisions/1003-curby-pulses-are-verified-against-a-pinned-chain-before-any-byte-is-used.md)); NIST's certificates are pinned by the SHA-512 of their DER, and a pulse's RSA signature, output and hash-chain link to its verified predecessor are checked ([ADR-1009](decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md)); drand's BLS signature is checked against the pinned quicknet key ([ADR-1010](decisions/1010-drand-rounds-are-verified-against-the-pinned-quicknet-key-with-a-managed-bls12-381-library.md)) |
| Replay of an old pulse, or a rolled-back chain | Yes | R3: rounds must not decrease, and pulses older than their beacon's limit (`MaxPulseAge`, or its `MaxPulseAgeByBeacon` entry) are refused |
| Equivocation: two different pulses for one round | Yes | R3: a second pulse for an accepted round with other content is refused, and a beacon cannot switch chains |
| A server grinds a pulse's CID, its randomness, without changing what is signed | Yes | R2: documents must have exactly the members Twine defines, and signatures must be canonical base64url of exactly the modulus length |
| The beacon operator grinds its own output | Partly | RS256 (deterministic) signatures and the salt precommitment, as CURBy's protocol defines; NIST's precommitment, which each pulse must match from its predecessor; drand's unique threshold signatures, which leave no choice among valid values for a round. The operator still knows a pulse before publishing it, so draws commit to a future round ([draws.md](draws.md)) |
| The CURBy-Q operator publishes randomness its Bell data and seed don't produce | Partly | Pinned CURBy-Q and Bell chains, signatures, the Bell response and seed in causal order, and the seed's expansion are checked; the hypothesis test and Trevisan extraction are checked only out of process by `curby_verify` ([ADR-1007](decisions/1007-curby-q-rounds-are-verified-as-curbys-client-verifies-them-and-their-extraction-is-left-to-curby-verify.md)) |
| A draw organizer picks a favorable pulse | Yes | The commit-first procedure in [draws.md](draws.md) |
| Beacon outage or slowness | Yes | R4: background collection, timeouts, back-off, circuit breaker, `Degraded` health ([ADR-1002](decisions/1002-beacons-are-adapters-collected-in-the-background-and-an-outage-degrades-never-fails.md)) |
| Oversized or malformed responses | Yes | Byte limit while streaming, strict DAG-JSON, no redirects, JSON content type only, except `application/octet-stream` for CURBy-Q's round parameters and `text/plain` for NIST's certificates, duplicate JSON members refused, every response read under the `MaxResponseBytes` streaming limit |
| Modulo bias in ranges | Yes | Rejection sampling in every bounded value |
| Disclosure of process memory | No | Out of scope; R6 limits what remains after disposal |
| A weak operating-system CSPRNG | Partly | Fresh beacon pulses diversify every reseed, for example after a virtual machine snapshot is restored |

## Secure Design Principles Applied

- **Economy of mechanism:** one standard DRBG, one sampler for every typed value, one port for every beacon.
- **Fail-safe defaults:** a pulse is refused on any failed check; secret output never depends on a beacon; the
  host, or `RandomnessHost`'s `BuildAsync`, fails at start on an invalid or misspelled setting.
- **Complete mediation:** every pulse passes the adapter's verification and the tracker's acceptance before the pool
  sees it.
- **Least privilege:** adapters only read public HTTPS endpoints; no credential is needed or stored.
- **Open design:** the algorithms are standard (SP 800-90A, FIPS 202, RFC 7515, RFC 8017, RFC 9380) and the draw
  algorithm is published with a reference implementation.

## Common Weaknesses Countered

- CWE-330, CWE-338 (insufficiently random values, weak PRNG): R1.
- CWE-347 (improper verification of a cryptographic signature): R2.
- CWE-294, CWE-1390 (replay, weak authentication): R2, R3.
- CWE-400, CWE-770 (uncontrolled resource consumption): response byte limits and timeouts.
- CWE-226 (sensitive information in a resource not removed before reuse): R6.

## Evidence

- **Known answers:** the HMAC_DRBG passes the 32 SHA-512 vectors of NIST CAVP; the managed SHA3-512 matches an
  independent implementation and the platform's, as does the managed SHAKE256; the Twine verifier accepts documents
  CURBy served, CURBy-Q's included, and recomputes their CIDs; NIST pulses across a certificate rotation verify;
  the hash to curve matches the RFC 9380 vectors and drand's rounds verify; draws match a reference implementation
  that shares no code with the library.
- **Negative tests:** each verification rule is broken in isolation with documents signed by a test key: wrong pin,
  wrong chain, bad or foreign signature, disallowed JWS headers, broken precommitment, wrong ordering, malformed
  payloads, unpinned NIST certificates, broken NIST links and precommitments, and foreign and identity BLS signatures;
  and each acceptance rule (rollback, equivocation, staleness).
- **Coverage:** `./build/dod.ps1` fails below 90% of lines or 80% of branches.

- **Gate:** `./build/dod.ps1` builds in Release with warnings as errors
  ([ADR-0100](decisions/project-template/0100-warnings-are-errors-in-every-project.md)), runs the analyzers and the
  banned-API list in `build/BannedSymbols.txt`
  ([ADR-0102](decisions/project-template/0102-disallowed-apis-are-banned-at-build-time.md)), and runs the tests; CI runs
  the commands on every pull request.
- **Static analysis:** with CodeQL's default setup configured and analyzing at least one language, the repository
  ruleset blocks a pull request that adds a CodeQL alert at warning level or above, or a security alert of any severity
  ([ADR-0005](decisions/project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md),
  [ADR-0030](decisions/project-template/0030-the-code-scanning-rule-is-added-only-where-codeql-analyzes-a-language.md)).
- **Review:** every change reaches the default branch through a pull request with passing CI and every conversation
  resolved, and the ruleset refuses force pushes and deletion
  ([ADR-0005](decisions/project-template/0005-the-default-branch-accepts-changes-only-through-pull-requests.md),
  [ADR-0014](decisions/project-template/0014-every-pull-request-discussion-is-remediated-and-replied-to-before-it-merges.md)).
  NCode Review reviews each pull request and approves a clean one.
- **Dependencies:** pinned, and moved by reviewed updates
  ([ADR-0008](decisions/project-template/0008-external-inputs-are-pinned-and-kept-current.md)) that wait three days for
  a new version ([ADR-0009](decisions/project-template/0009-new-dependency-versions-wait-a-cooldown.md)). Dependabot proposes the updates. Central package management keeps one version of each package
  ([ADR-0101](decisions/project-template/0101-central-package-management.md)).
- **Release integrity:** packages are deterministic and source-linked
  ([ADR-0130](decisions/project-template/0130-source-link-and-symbol-packages.md)). `publish.yml` builds them from a tagged commit the default branch holds and pushes them through
  nuget.org trusted publishing with no stored key, after a maintainer approves
  ([ADR-0135](decisions/project-template/0135-packages-are-published-from-a-github-release-through-trusted-publishing.md),
  [ADR-0137](decisions/project-template/0137-the-release-workflow-starts-package-publishing.md)), and each release
  carries signed provenance and an SBOM ([ADR-0139](decisions/project-template/0139-releases-carry-build-provenance.md),
  [ADR-0151](decisions/project-template/0151-release-follow-ups-are-jobs-publish-calls.md)). [SECURITY.md](../SECURITY.md#verify-a-package) says how to verify a package.
- **Response:** [SECURITY.md](../SECURITY.md) defines private reporting, coordinated disclosure and credit.

## Security Reviews

[//]: # "Maintainers: record each security review, newest first: its date, who did it, what it covered (the security requirements and the trust boundary above) and what it found. The OpenSSF Best Practices gold level asks for one in the last five years."

None recorded yet.
