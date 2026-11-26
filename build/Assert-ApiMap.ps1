# Copyright © NCode Group
# SPDX-License-Identifier: Apache-2.0

#requires -version 7
<#
.SYNOPSIS
    Checks the API map against the PublicAPI files of the four packages: every public line is in exactly one entry of
    the map, and every line the map lists is public.
.DESCRIPTION
    The API map on /reference/api-map/ is the data in docs/site/_components/data/api-map.json, which ApiMap.astro
    renders. Each of its entries names a `package` and lists in `api` the lines of that package's public API it
    covers. The public lines of a package are those of src/<package>/PublicAPI.Shipped.txt and
    src/<package>/PublicAPI.Unshipped.txt, without their trailing white space, leaving out empty lines, lines that
    start with # and lines that start with *REMOVED*. Lines are compared as ordinal strings.

    Only the entries of the packages selected with -Package are checked, and only their public lines are required to
    be listed, so the map can be filled package by package.

    Reports, in this order: the map file or a PublicAPI file is missing, which stops the check; no public line in the
    selected packages, which stops it too; each entry whose package is not one of the four packages, in entry order,
    whatever -Package is; each public line no entry lists, in package order, then file order, Shipped before
    Unshipped; each line an entry lists that is not in the public API of the entry's own package, in entry order; each
    line listed more than once, in the order it is first listed, with the id of each entry that lists it. Prints
    nothing when none holds; otherwise prints 'API map: FAILED.' and each problem, and exits 1. build/gate.ps1 runs it,
    after build/Test-AssertApiMap.ps1, which tests it.
.PARAMETER Root
    The repository root; build/Test-AssertApiMap.ps1 points it at a fixture folder.
.PARAMETER Package
    The packages to check, by name. Defaults to the four packages of the repository.
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string[]]$Package = @('NCode.Randomness', 'NCode.Randomness.Curby', 'NCode.Randomness.Nist', 'NCode.Randomness.Drand')
)
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $Root
try {
    $allPackages = @('NCode.Randomness', 'NCode.Randomness.Curby', 'NCode.Randomness.Nist', 'NCode.Randomness.Drand')
    $mapPath = 'docs/site/_components/data/api-map.json'
    if (-not (Test-Path -LiteralPath $mapPath -PathType Leaf)) {
        'API map: FAILED.'
        "API map: $mapPath does not exist"
        exit 1
    }
    # The public lines of each selected package, in file order, and the same lines as a set.
    $public = [Collections.Generic.List[string]]::new()
    $publicOf = [Collections.Generic.Dictionary[string, [Collections.Generic.HashSet[string]]]]::new([StringComparer]::Ordinal)
    foreach ($name in $Package) {
        $publicOf[$name] = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($file in @('PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt')) {
            $path = "src/$name/$file"
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                'API map: FAILED.'
                "API map: $path does not exist"
                exit 1
            }
            foreach ($line in [IO.File]::ReadLines([IO.Path]::Combine($PWD.Path, $path))) {
                $text = $line.TrimEnd()
                if ($text.Length -and -not $text.StartsWith('#', [StringComparison]::Ordinal) -and -not $text.StartsWith('*REMOVED*', [StringComparison]::Ordinal)) {
                    $public.Add($text)
                    $null = $publicOf[$name].Add($text)
                }
            }
        }
    }
    # A misread file yields no lines, and a map checked against nothing would pass.
    if ($public.Count -eq 0) {
        'API map: FAILED.'
        "API map: no public line in $($Package -join ', ')"
        exit 1
    }
    $map = Get-Content -LiteralPath $mapPath -Raw | ConvertFrom-Json -AsHashtable
    # The lines of the selected entries in entry order, and the ids of the entries that list each one, in the order it
    # is first listed.
    $unknown = [Collections.Generic.List[string]]::new()
    $listings = [Collections.Generic.List[object]]::new()
    $ids = [Collections.Generic.Dictionary[string, [Collections.Generic.List[string]]]]::new([StringComparer]::Ordinal)
    $first = [Collections.Generic.List[string]]::new()
    foreach ($entry in $map['entries']) {
        $entryPackage = if ($entry.ContainsKey('package')) { $entry['package'] } else { $null }
        if ($entryPackage -cnotin $allPackages) {
            $unknown.Add("API map: entry $($entry['id']) names no package of this repository")
            continue
        }
        if ($entryPackage -cnotin $Package) { continue }
        if (-not $entry.ContainsKey('api')) { continue }
        foreach ($line in $entry['api']) {
            $listings.Add([pscustomobject]@{ Line = $line; Id = $entry['id']; Package = $entryPackage })
            if (-not $ids.ContainsKey($line)) {
                $ids[$line] = [Collections.Generic.List[string]]::new()
                $first.Add($line)
            }
            $ids[$line].Add($entry['id'])
        }
    }
    $problems = @(
        $unknown
        $public | Where-Object { -not $ids.ContainsKey($_) } | ForEach-Object { "API map: no entry for '$_'" }
        $listings | Where-Object { -not $publicOf[$_.Package].Contains($_.Line) } | ForEach-Object { "API map: '$($_.Line)' (entry $($_.Id)) is not in the public API of $($_.Package)" }
        $first | Where-Object { $ids[$_].Count -gt 1 } | ForEach-Object { "API map: '$_' is listed $($ids[$_].Count) times: $($ids[$_] -join ', ')" }
    )
    if ($problems) { 'API map: FAILED.'; $problems; exit 1 }
}
finally { Pop-Location }
