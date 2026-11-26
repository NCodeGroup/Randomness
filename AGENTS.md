# NCode.Randomness

Cryptographically secure random numbers for .NET, strengthened by public randomness beacons such as CURBy.

Instructions for every coding agent in this repository. This file is an index: read each file it lists, in order, then the sections below. Where the project's instructions differ from the template's, the project's win. Claude Code reads this file through `CLAUDE.md` and imports each `@` file. GitHub Copilot reads it directly and loads every `.github/instructions/*.instructions.md` file on its own.

This file belongs to the project: template updates never change it. Keep it under 200 lines; put anything longer in a listed file.

## Instructions

- @.github/instructions/project-template.instructions.md — the plan-then-execute workflow and base conventions, from the project template. Template updates replace it; never edit it. To change one of its rules, override it below or in a listed file.
- `.github/instructions/csharp.instructions.md` — C# conventions, from the project template; read it before editing `.cs` files.
- `.github/instructions/csharp-tests.instructions.md` — C# test conventions, from the project template; read it too before editing a test project's `.cs` files.
- `docs/decisions/` — this project's architecture decision records, and in `project-template/` the template's; read the ones a change touches before deciding otherwise.
- Project instruction files: add one line for each, `@<path> — what it covers`.

## Commands

Run from the repository root in PowerShell 7.

- Build: `dotnet build`
- Test: `dotnet test`
- Gate: `./build/gate.ps1` (the full gate CI runs) or `./build/gate.ps1 -Fast` (Debug, no pack): `dotnet tool restore`, the sample keys, README samples and API map checks, each tested offline first, then the Definition of Done, `./build/dod.ps1`, whose Format stage `dotnet csharpier format .` fixes; it ends with `Definition of Done: PASSED`. Record a public API change with `./build/Update-PublicApi.ps1`
- Samples: `dotnet mdsnippets` copies the README samples from the tests
- Site: with [NCodeGroup/docs](https://github.com/NCodeGroup/docs) cloned into a folder named `docs` beside the main checkout and set up as its README says, and Node.js on `PATH` at the version its `.node-version` names, `node build/site.mjs build --out <folder>` builds the documentation site from `docs/site/` into a folder outside this repository, `node build/site.mjs check --out <folder>` loads each page of that build at 1440, 1280, 1024 and 390 px and names every element that leaves its content column, and `node build/site.mjs dev` serves it; the launcher finds the toolchain from the main checkout and from a plan's worktree alike, and none of them writes here

## Landing

How `/ship-plan` lands a reviewed plan. `integrate`: `pull-request` pushes the plan's branch and opens a pull request; `direct` pushes to the default branch. `release`: `none`; `tag` tags the plan's `Release:` version, or the next free one when another plan landed it first, and a plan has that line only when it changes something a release ships, such as the product's code, its package's readme or its build, so one that changes only site pages, CI, decision records, tests or repository docs lands with no tag and no release (ADR-0032 in `docs/decisions/project-template/`); when this project's releases ship something else, say what under `## Conventions`; `tag+release` also publishes a release from that version's section of `changelog`. On GitHub, `.github/workflows/tag-release.yml` tags and publishes it as the pull request merges, from the `Release:` line `/ship-plan` writes in its description or the changelog's newest section, and for a .NET library starts `.github/workflows/publish.yml`; `/ship-plan` run after the merge does only what that workflow didn't. `changelog`: the file every plan's last step writes, in the Keep a Changelog format, `CHANGELOG.md` by default. `pre-release`: `untracked` keeps the project pre-release until a version tag of 1.0.0 or later: nothing it released or stored has a reader that must keep working, so every text describes the product as it is, the changelog's `## [Unreleased]` is a catalog of what the product does that each plan edits in place, and a breaking change needs no deprecation or migration path, as ADR-0015 in `docs/decisions/project-template/` says; `tracked`, the default when the line is missing, tracks every change from the first release, for a project whose releases or stored data already have readers. `draft`: `no`, the default when the line is missing, which a template update never changes, so a project lands as it did; `yes` opens every pull request as a draft, as `/ship-plan draft` does, and a new project that runs NCode Review starts with it, since it reviews and approves a draft too, so marking it ready is the person's review; where NCode Review's approver is set up, which is where `.github/workflows/code-review.yml` passes `approve: true` and `approver-private-key` and the repository's `code-review` environment holds `NCODE_REVIEW_APPROVER_PRIVATE_KEY`, the NCode Review Approver submits that approval, and it meets the ruleset's required approval, of the last push, with one writer too; elsewhere NCode Review's own app approves, which counts for Scorecard but meets no required approval, which only someone with write access other than the author can give (ADR-0022, ADR-0034 and ADR-0036 in `docs/decisions/project-template/`); and `/run-issues` opens its pull requests as drafts either way. `follow-ups`: `issues` files a plan's follow-ups as issues in this repository; `<owner/repo>` files them in that repository; `none` files none, and `/ship-plan` lists them for you to record. `follow-up-label`: the label those issues get, `follow-up` by default; write a name with spaces as it is, such as `tech debt`. `gate`: `none`; a command, such as `./build/dod.ps1 -Fast`, that `/ship-plan`, `/run-issues`, `/address-feedback` and `/run-feedback` run from the repository root before each push, stopping when it exits non-zero or changes a file. `protect`: `none`; `hook` makes `.githooks/pre-push` refuse any push to the default branch, for a host that can't require pull requests, such as a private repository on GitHub Free. `pr-format`: `none`; `<file>#<heading>`, such as `AGENTS.md#Pull requests`, names the section that sets the title and description of the pull requests `/ship-plan` opens, which still end with their `Closes` lines. `review-bots`: the review bots, by login and separated by commas, whose findings `/run-issues`, `/address-feedback` and `/run-feedback` may fix, `ncode-review` by default, or `none`; `/run-issues` and `/run-feedback` judge every one of NCode Review's findings, whatever its severity, and fix each that holds, and a bot's finding alone never changes CI, the template, agent configuration, the build or the dependencies: `/address-feedback` and `/run-feedback` change them only when you approve it in their start question. `max-review-rounds`: `3`; after that many rounds of review feedback on a pull request, `/run-issues` and `/run-feedback` comment no further `/review`, so NCode Review isn't asked to review again, and still fix and answer every finding; they recommend the merge once every finding is fixed, declined or answered and the review of the last push they asked for is in. `prelude`: `none`; one PowerShell command that sets up the shell the workflow's commands run in, such as `./build/env.ps1`, a script in the repository, or `nvs use 26`, to put a tool installed through a version manager on `PATH`. `/ship-plan`, `/run-issues`, `/address-feedback` and `/run-feedback` run it before the gate, and executors and coordinators before each Done-when command, dot-sourced in the same PowerShell; a plan names it under Context for the executor, so no step repeats it. When it fails, they stop and show its error. Write it on one line, without a leading `. ` or `& ` operator, a `#` comment, or `;`, `|`, `&`, `&&` or `||`; `.template/Get-PlanState.ps1` prints any other value as `prelude=invalid`, and `/ship-plan` stops on it. `/run-issues` runs only when the host requires pull requests into the default branch, or `protect` is `hook` and the hooks are enabled.

- integrate: pull-request
- release: tag+release
- changelog: CHANGELOG.md
- pre-release: untracked
- draft: yes
- follow-ups: issues
- follow-up-label: follow-up
- gate: ./build/gate.ps1 -Fast
- protect: none
- pr-format: none
- review-bots: ncode-review
- max-review-rounds: 3
- prelude: none

## Conventions

- Secrets come only from `ISecureRandom`. Never derive a key, token, salt or nonce from `BeaconPulse.Randomness` or a
  `VerifiableRandom` ([ADR-1000](docs/decisions/1000-beacon-randomness-is-mixed-into-a-local-generator-and-never-used-alone-for-secrets.md)).
- Secret material, meaning a key, a seed, the generator's state or bytes kept from `ISecureRandom`, lives only in a
  `stackalloc` span or a pinned array (`GC.AllocateArray` or `GC.AllocateUninitializedArray` with `pinned: true`) and
  is cleared with `CryptographicOperations.ZeroMemory` on every path out, never in `ArrayPool<T>.Shared` or an unpinned
  array, which the garbage collector can copy. This overrides the template's rule to rent transient buffers from
  `ArrayPool<T>.Shared` for secret material; public data, such as a pulse or a response body, still rents from it. The
  packages take no buffer library dependency, such as NCode.Buffers: their members take and fill `Span<byte>`, so
  callers bring their own buffers
  ([ADR-1012](docs/decisions/1012-secrets-live-only-in-stack-or-pinned-memory-and-the-packages-take-no-buffer-library-dependency.md)).
- The sampling algorithms in `src/NCode.Randomness/Sampling/` and the draw derivation in `src/NCode.Randomness/Draws/`
  are the published `ncode-randomness-draw/1` contract in `docs/draws.md`: a change that alters the bytes they read or
  the values they return is a new algorithm name, with the document, its reference implementation and the pinned
  tests changed together ([ADR-1004](docs/decisions/1004-reproducible-draws-are-a-versioned-specified-algorithm-and-never-a-secure-random.md)).
- A beacon adapter is a package of its own that verifies every pulse against a pinned trust anchor before returning it,
  as `docs/beacon-adapters.md` says; the core never changes for a vendor.
- Fixtures in `tests/NCode.Randomness.Curby.Tests/Fixtures/`, `tests/NCode.Randomness.Nist.Tests/Fixtures/` and
  `tests/NCode.Randomness.Drand.Tests/Fixtures/` are documents CURBy, NIST and drand served, byte for byte: never edit
  them; fetch them again with the commands in their README.
- The C# samples in `README.md` and the four `src/*/README.md` are copied by MarkdownSnippets from
  `tests/NCode.Randomness.Readme.Tests/ReadmeSamplesTests.cs` and `TokenService.cs`
  ([ADR-1014](docs/decisions/1014-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md)):
  change a sample in its test, between `// begin-snippet: <name>` and `// end-snippet`, then run `dotnet mdsnippets`;
  never edit a README's `<!-- snippet: -->` block, which `./build/gate.ps1` checks against the tests. A `#region`
  name in a test needs a space.
- Releases of NCodeGroup/docs, the documentation site's toolchain, are adopted the day they are published while the
  NCode brand, the toolchain and the first sites built with them are built out
  ([ADR-1017](docs/decisions/1017-ncodegroup-docs-releases-are-taken-without-a-cooldown-during-the-build-out.md)),
  which overrides project-template ADR-0009's three-day cooldown for that one dependency: `.github/dependabot.yml`
  excludes it from the cooldown, until NCodeGroup/docs 1.0.0 or the maintainers end the build-out. Every other
  dependency, NCode Review included, waits three days.
- The documentation site's content is in `docs/site/`, and the shared toolchain in NCodeGroup/docs builds it
  ([ADR-1015](docs/decisions/1015-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md)):
  this repository gets no `package.json`, no lockfile and no Node.js code. A page is an `.mdx` file, never `.md`,
  listed in the `sidebar` of `docs/site/site.config.json`, with links to other pages written from the site's root with
  a trailing slash; the toolchain's `CONTRACT.md` is the reference. `.github/workflows/docs.yml` builds the site and
  checks its layout on every pull request, with the toolchain pinned by commit, and publishes it to GitHub Pages,
  https://randomness.ncode.io/, from every push to the default branch
  ([ADR-1018](docs/decisions/1018-the-documentation-site-is-published-to-github-pages-from-the-default-branch.md),
  [ADR-1019](docs/decisions/1019-the-documentation-site-is-served-at-randomness-ncode-io.md)). The READMEs link to it
  with that absolute URL, since nuget.org shows the package READMEs.
- A page never holds C# that must compile: it includes a sample with an empty `cs snippet=<Key>` code block, and the
  site's build fills it
  ([ADR-1016](docs/decisions/1016-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md)).
  Site samples are tests in `tests/NCode.Randomness.Site.Tests`: one class per page, named for the page's file in a
  folder named for the page's folder (`docs/site/guides/run-a-public-draw.mdx` has
  `Guides/RunAPublicDrawSamples.cs`), and one fact per sample, named `<Key>Sample_Runs`, with the sample between
  `// begin-snippet: <Key>` and `// end-snippet`, each alone on its line. A sample key is PascalCase letters and
  digits, defined once in the repository whatever its letter case; a site sample's key starts with `Site` and no
  README sample's does, and `./build/gate.ps1` checks the markers and all three rules. A page may also include a
  README sample by its key.
- A page never holds a copy of a repository file either: it shows the file, whole or from the line that holds one text
  to the line that holds another, with an empty `file=<path>` code block, such as
  `xml file=Directory.Build.props from="<ItemGroup" to="</ItemGroup>"`, which the site's build fills and links to
  those lines at the commit it was built from (NCodeGroup/docs `CONTRACT.md` section 4, NCodeGroup/docs ADR-1010). The
  site's components style themselves with the brand's public tokens: the `--nc-` colour roles, spacing, radii,
  borders, elevation, motion, focus, controls and font families, the type roles' `-stretch` and `-tracking` parts,
  and Starlight's `--sl-text-*` sizes, never a step of a colour ramp or of the type scale; the site's build fails a
  component that reads one, or a `--nc-` token the brand does not define (NCodeGroup/docs ADR-1012).
- A guide has a `## Pitfalls` section, which the `requiredHeadings` rule of `docs/site/site.config.json` requires; the
  site's build fails a guide without it (NCodeGroup/docs ADR-1011). The API map's data,
  `docs/site/_components/data/api-map.json`, lists every line of `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
  of the four packages in exactly one entry, and `./build/gate.ps1` checks it with `build/Assert-ApiMap.ps1`: a change
  that adds a public member adds its entry. A change to a beacon's checks, cadence or settings updates its entry in
  `docs/site/_components/data/beacons.json`.
- Tests are hermetic. The live tests against CURBy, NIST and drand run only with `NCODE_RANDOMNESS_LIVE_TESTS=1`:
  `dotnet test tests/NCode.Randomness.Curby.Tests --filter "FullyQualifiedName~CurbyLiveTests"`,
  `dotnet test tests/NCode.Randomness.Nist.Tests --filter "FullyQualifiedName~NistLiveTests"` and
  `dotnet test tests/NCode.Randomness.Drand.Tests --filter "FullyQualifiedName~DrandLiveTests"`.
- NIST replaces its certificate about once a year. A release adds the new identifier to `NistDefaults.CertificateIds`
  ([ADR-1009](docs/decisions/1009-nist-pulses-are-verified-against-pinned-certificates-and-their-hash-chain-before-any-byte-is-used.md))
  after the checks `src/NCode.Randomness.Nist/README.md` lists.
- Options classes have setters for `Action<TOptions>` callbacks, which overrides the template's `init`-only rule for
  them ([ADR-1006](docs/decisions/1006-options-classes-have-setters-for-the-options-pattern.md)). The core binds
  `Randomness` strictly, so each adapter binds a section of its own under `RandomnessBeacons`.
- A type that only its own factory builds, such as `NistPulse.Parse`, `NistCertificate.Parse` or `DrandRound.Verify`,
  keeps a private classic constructor: a primary constructor can't be private, so it would let any code in the
  assembly build an unverified instance, and `csharp.instructions.md` asks for one only where it can express the type.
