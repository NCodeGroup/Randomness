# 1014. README samples are compiled tests that MarkdownSnippets copies into the READMEs

- **Status:** Accepted
- **Date:** 2026-11-25
- **Deciders:** NCode Group maintainers

## Context

A sample copied by hand into a README drifts from the API, and nothing notices a renamed member or a sample that never
compiled. Five READMEs show samples: `README.md`, the full guide on GitHub, and `src/NCode.Randomness/README.md`,
`src/NCode.Randomness.Curby/README.md`, `src/NCode.Randomness.Nist/README.md` and
`src/NCode.Randomness.Drand/README.md`, the package readmes nuget.org renders, where a relative link doesn't resolve.

## Decision

**Every README sample is a compiled xUnit test, MarkdownSnippets copies it into the READMEs, and `build/gate.ps1` fails
when a README differs from its test.**

1. The samples live in `tests/NCode.Randomness.Readme.Tests`, namespace `ReadmeSamples`. `ReadmeSamplesTests.cs` holds
   one fact per sample named `<Sample>Sample_Runs` that asserts what the sample's comments claim, between
   `// begin-snippet: <Sample>` and `// end-snippet`. A type a sample shows, such as `TokenService`, is a file of its
   own in the project, between the markers. The project departs from the test naming and region conventions because it
   tests the READMEs.
2. The project references the four packages, and no package grants it `InternalsVisibleTo`, so a sample compiles
   against the public API only, as a consumer's code does.
3. `tests/Shared/Recorded.props` gives the project the recorded beacon documents, `RecordedDocuments` and
   `RecordedCurbyBeacon`, so a sample that draws or verifies runs offline.
4. The project uses `tests/samples.runsettings`, which measures no module, so a sample never counts as coverage of a
   package. It formats to 90 columns, because site pages include README samples.
5. MarkdownSnippets.Tool, pinned in `.config/dotnet-tools.json` with `rollForward: false`, copies the samples with the
   `InPlaceOverwrite` convention and `OmitSnippetLinks` from `mdsnippets.json`.
6. `build/Assert-Markdown.ps1` runs `dotnet mdsnippets` and fails when it changes a Markdown file or fails.
   `build/Test-AssertMarkdown.ps1` proves both failures and a pass on a temporary Git repository with a fake
   `dotnet mdsnippets`. `build/gate.ps1` runs the self-test, then the check, before the template-owned `build/dod.ps1`,
   and CI and the Landing section's `gate` run `build/gate.ps1`.
7. A sample is edited in its test, never in a README.

## Options considered

- Hand-copied samples: rejected, because nothing checks them.
- A custom test that parses README code blocks: rejected, because it re-implements MarkdownSnippets.
- Snippet-source links: rejected, because repository-relative links break on nuget.org.
- Samples in each package's test project: rejected, because the root README registers all three adapters and those
  projects see internal members.
- MarkdownSnippets `InPlaceOverwrite`: chosen. The READMEs keep their names and each package README stays the package
  readme. NCode.PropertyBag's ADR-1003 decides the same.

## Consequences

- The samples show CSharpier's formatting, so a long line wraps in the README.
- An edit inside a `<!-- snippet: -->` block is overwritten by the next `dotnet mdsnippets`.
- Dependabot's `nuget` ecosystem updates the tool with the other packages.
- A `#region` name in the test needs a space, or MarkdownSnippets reads it as a snippet.

## References

- `tests/NCode.Randomness.Readme.Tests/ReadmeSamplesTests.cs`, `tests/Shared/Recorded.props`,
  `tests/samples.runsettings`, `mdsnippets.json`, `build/Assert-Markdown.ps1`, `build/Test-AssertMarkdown.ps1`,
  `build/gate.ps1`
- [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets)
- [NCode.PropertyBag ADR-1003](https://github.com/NCodeGroup/PropertyBag/blob/main/docs/decisions/1003-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md)
