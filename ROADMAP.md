# Roadmap

What NCode.Randomness intends to do over the next year. Open issues hold the detail and the current state; this page
is the direction. It is reviewed at least once a year and when a release changes it.

[//]: # "Maintainers: replace the prompts with the project's own plans, and review this page at least once a year."

## Scope

NCode.Randomness covers what [README.md](README.md) describes. A feature outside that starts as an issue, where the
maintainers say whether it fits.

## Next Twelve Months

- **CURBy-Q.** Read the quantum DIRNG chain (CURBy-Q) as well, with the checks CURBy's client library makes, and
  verify its Trevisan extraction where it can be done in process.
- **More beacons.** Adapters for the NIST Randomness Beacon 2.0 and for drand (League of Entropy), so no single beacon
  is a point of trust.
- **Draws offline.** Verify a pulse document a user supplies, so a published draw can be checked without the network.
- **Use without a host.** A builder that composes the secure generator and beacons without dependency injection.
- **Supply chain.** Keep external inputs pinned and current
  ([ADR-0008](docs/decisions/project-template/0008-external-inputs-are-pinned-and-kept-current.md)),
  and raise the OpenSSF Scorecard score where the repository controls it
  ([ADR-0020](docs/decisions/project-template/0020-github-projects-are-scored-by-openssf-scorecard.md)).
- **OpenSSF Best Practices.** Reach the silver level, which needs a second owner for continuity
  ([GOVERNANCE.md](GOVERNANCE.md#continuity), [docs/best-practices-badge.md](docs/best-practices-badge.md)).

## Not Planned

- **Beacon bytes as secrets.** No API serves a pulse's randomness as an `ISecureRandom`: it is public
  ([ADR-1000](docs/decisions/1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)).
- **Fetching on demand.** No API makes a caller wait on a beacon for a secret value.
