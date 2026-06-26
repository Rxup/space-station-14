#!/usr/bin/env pwsh
# Checks all entity prototypes with DamageableComponent.
#
# Usage:
#   .\Tools\check-damageable-prototypes.ps1              # static YAML audit + full runtime report
#   .\Tools\check-damageable-prototypes.ps1 -StaticOnly  # fast YAML-only (~seconds)
#   .\Tools\check-damageable-prototypes.ps1 -RuntimeOnly # one integration test, all failures at once
#
# Static audit finds missing Injurable in prototype YAML (with inheritance).
# Runtime report spawns every prototype once in a single test pool and lists all failures.

[CmdletBinding()]
param(
    [switch]$StaticOnly,
    [switch]$RuntimeOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Stale testhost processes can lock Content.IntegrationTests.dll after interrupted runs.
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force

$project = "Content.IntegrationTests/Content.IntegrationTests.csproj"
$buildArgs = @("build", $project, "-v", "q")
if ($NoBuild) {
    $buildArgs += "--no-restore"
}

function Invoke-DamageableTest {
    param(
        [Parameter(Mandatory)]
        [string]$Filter
    )

    $args = @(
        "test", $project,
        "--filter", $Filter,
        "--logger", "console;verbosity=normal"
    )

    if ($NoBuild) {
        $args += "--no-build"
    }

    Write-Host ">> dotnet $($args -join ' ')" -ForegroundColor Cyan
    & dotnet @args
    return $LASTEXITCODE
}

$exitCode = 0

if (-not $RuntimeOnly) {
    Write-Host "`n=== Static audit (missing Injurable in YAML) ===" -ForegroundColor Yellow
    if (-not $NoBuild) {
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $staticFilter = "FullyQualifiedName~DamageablePrototypeStaticTest.TestDamageableMissingInjurableStatic"
    $code = Invoke-DamageableTest -Filter $staticFilter
    if ($code -ne 0) { $exitCode = $code }
}

if (-not $StaticOnly) {
    Write-Host "`n=== Runtime audit (all damageable prototypes, single report) ===" -ForegroundColor Yellow
    if (-not $NoBuild -and -not $RuntimeOnly) {
        # already built above
    }
    elseif (-not $NoBuild) {
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $runtimeFilter = "FullyQualifiedName~DamageAllPrototypesTest.TestAllDamageableComponentsReport"
    $code = Invoke-DamageableTest -Filter $runtimeFilter
    if ($code -ne 0) { $exitCode = $code }
}

exit $exitCode
