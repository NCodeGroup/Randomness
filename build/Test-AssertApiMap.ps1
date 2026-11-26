# Copyright © NCode Group
# SPDX-License-Identifier: Apache-2.0

#requires -version 7
<#
.SYNOPSIS
    Tests build/Assert-ApiMap.ps1 against fixture folders, offline.
.DESCRIPTION
    Covers a map that lists every public line once, whatever the files' comment, empty and *REMOVED* lines and the
    trailing white space of a line, and whichever of the Shipped and Unshipped files holds it; a public line no entry
    lists; a line an entry lists that is not public; a line two entries list; no map file; no public line; the
    -Package parameter, which leaves the other packages' lines and entries unchecked; and an entry whose package is
    not one of the four. Each fixture is a folder under the temp path, not a repository, that holds both PublicAPI
    files of the four packages and the map. Each case checks the exit code and every line the check prints. Exits 1
    on the first failed check. build/gate.ps1 runs it before the check.
#>
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Assert-ApiMap.ps1'
$utf8 = [Text.UTF8Encoding]::new($false)
$mapPath = 'docs/site/_components/data/api-map.json'
$core = 'NCode.Randomness'
$curby = 'NCode.Randomness.Curby'
$packages = @($core, $curby, 'NCode.Randomness.Nist', 'NCode.Randomness.Drand')
$type = 'NCode.Randomness.RandomnessHost'
$member = 'NCode.Randomness.RandomnessHost.Mode.get -> NCode.Randomness.RandomnessMode'
$curbyType = 'NCode.Randomness.Curby.CurbyBeacon'

# An entry of the map, which lists the lines of a package it covers.
function Entry([string]$Id, [string]$Package, [string[]]$Api) { @{ id = $Id; package = $Package; api = @($Api) } }

# $Entries is the map's entries, or $null for no map file. Each package's Shipped and Unshipped files start with a
# comment. The core package's Shipped file also holds an empty line and the type, its Unshipped file the member with
# trailing white space and a *REMOVED* line, and the Curby package's Shipped file its type. $NoPublicLines leaves
# only the comments. $Arguments are passed to the check.
function Invoke-Case {
    param(
        [string]$Name,
        [hashtable[]]$Entries,
        [string[]]$Problems = @(),
        [switch]$NoPublicLines,
        [string[]]$Arguments = @()
    )
    $root = Join-Path ([IO.Path]::GetTempPath()) "api-map-test-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        foreach ($package in $packages) {
            $folder = Join-Path $root "src/$package"
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            $shipped = "#nullable enable`n"
            $unshipped = "#nullable enable`n"
            if (-not $NoPublicLines) {
                if ($package -ceq $core) {
                    $shipped = "#nullable enable`n`n$type`n"
                    $unshipped = "#nullable enable`n$member  `n*REMOVED*NCode.Randomness.Old() -> void`n"
                }
                elseif ($package -ceq $curby) { $shipped = "#nullable enable`n$curbyType`n" }
            }
            [IO.File]::WriteAllText((Join-Path $folder 'PublicAPI.Shipped.txt'), $shipped, $utf8)
            [IO.File]::WriteAllText((Join-Path $folder 'PublicAPI.Unshipped.txt'), $unshipped, $utf8)
        }
        if ($null -ne $Entries) {
            $map = Join-Path $root $mapPath
            New-Item -ItemType Directory -Path (Split-Path -Parent $map) -Force | Out-Null
            $json = @{ groups = @(); entries = @($Entries) } | ConvertTo-Json -Depth 5
            [IO.File]::WriteAllText($map, "$json`n", $utf8)
        }

        # A child pwsh, as build/Test-AssertMarkdown.ps1 runs its check: the check's exit, a throw and its preferences
        # stay out of this session, and its exit code is the process's. Only its output is compared: what it writes to
        # stderr, such as a warning from git, is shown when a case fails.
        $stderr = [Collections.Generic.List[string]]::new()
        $printed = @(pwsh -NoProfile -NonInteractive -File $script -Root $root @Arguments 2>&1 | ForEach-Object {
                if ($_ -is [Management.Automation.ErrorRecord]) { $stderr.Add("$_") } else { "$_" }
            })

        $code = $LASTEXITCODE
        $shown = if ($stderr.Count) { "`nstderr:`n$($stderr -join "`n")" } else { '' }
        $expectedCode = if ($Problems) { 1 } else { 0 }
        if ($code -ne $expectedCode) { throw "exited $code instead of $expectedCode$shown" }
        $expected = if ($Problems) { @('API map: FAILED.') + $Problems } else { @() }
        if (($printed -join "`n") -cne ($expected -join "`n")) { throw "printed:`n$($printed -join "`n")$shown" }
    }
    catch { Write-Host "FAILED: $Name`n$_"; exit 1 }
    finally { Remove-Item -LiteralPath $root -Recurse -Force }
    Write-Host "ok: $Name"
}

Invoke-Case 'clean map' -Entries (Entry 'host' $core @($type)), (Entry 'host-mode' $core @($member)), (Entry 'curby-beacon' $curby @($curbyType))
Invoke-Case 'a public line no entry lists' -Entries (Entry 'host' $core @($type)), (Entry 'curby-beacon' $curby @($curbyType)) `
    -Problems "API map: no entry for '$member'"
Invoke-Case 'a line that is not public' -Entries (Entry 'host' $core @($type)), (Entry 'host-mode' $core @($member, 'NCode.Randomness.Gone() -> void')), (Entry 'curby-beacon' $curby @($curbyType)) `
    -Problems "API map: 'NCode.Randomness.Gone() -> void' (entry host-mode) is not in the public API of $core"
Invoke-Case 'a line two entries list' -Entries (Entry 'host' $core @($type, $member)), (Entry 'host-mode' $core @($member)), (Entry 'curby-beacon' $curby @($curbyType)) `
    -Problems "API map: '$member' is listed 2 times: host, host-mode"
Invoke-Case 'no map file' -Entries $null -Problems "API map: $mapPath does not exist"
Invoke-Case 'no public line' -Entries @() -NoPublicLines `
    -Problems "API map: no public line in $($packages -join ', ')"
Invoke-Case 'one package, with a line of another no entry lists' -Entries (Entry 'curby-beacon' $curby @($curbyType)) `
    -Arguments '-Package', $curby
Invoke-Case 'an entry of a package outside the repository' -Entries (Entry 'host' $core @($type)), (Entry 'host-mode' $core @($member)), (Entry 'curby-beacon' $curby @($curbyType)), (Entry 'other' 'NCode.Other' @('NCode.Other.Thing -> void')) `
    -Problems 'API map: entry other names no package of this repository'
# Every case passed. The last one left the check's exit code, 1, in $LASTEXITCODE, which a caller may read.
exit 0
