#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates reference-only stub DLLs from the Restory game installation
    and saves them to libs/ for use in CI builds.

.DESCRIPTION
    Run this script once after cloning, or any time the game updates.
    The stubs contain the full public API but have empty method bodies,
    so the mod project can compile without the actual game installed.

    After running, commit the updated libs/ folder to the repository.

.PARAMETER GamePath
    Path to the Restory installation folder.
    Defaults to the value in Directory.Build.props.

.EXAMPLE
    .\generate-stubs.ps1
    .\generate-stubs.ps1 -GamePath "D:\SteamLibrary\steamapps\common\Restory"
#>
param(
    [string] $GamePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve GamePath ──────────────────────────────────────────────────────────
if (-not $GamePath) {
    $propsFile = Join-Path $PSScriptRoot "Directory.Build.props"
    if (-not (Test-Path $propsFile)) {
        Write-Error "Directory.Build.props not found. Pass -GamePath explicitly."
    }
    [xml]$props = Get-Content $propsFile
    $GamePath = $props.Project.PropertyGroup.GamePath
    if (-not $GamePath) {
        Write-Error "Could not read <GamePath> from Directory.Build.props."
    }
}

if (-not (Test-Path $GamePath)) {
    Write-Error "Game path does not exist: '$GamePath'"
}

Write-Host "Game path: $GamePath" -ForegroundColor Cyan

# ── Build StubGenerator ───────────────────────────────────────────────────────
$toolProject = Join-Path $PSScriptRoot "tools\StubGenerator\StubGenerator.csproj"
$toolOut     = Join-Path $PSScriptRoot "tools\StubGenerator\out"

Write-Host "`nBuilding StubGenerator..." -ForegroundColor Yellow
dotnet build $toolProject -c Release -o $toolOut --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "StubGenerator build failed." }

$stubExe = Join-Path $toolOut "StubGenerator.dll"

# ── Define DLLs to stub ───────────────────────────────────────────────────────
$managedSrc  = Join-Path $GamePath "Restory_Data\Managed"
$melonSrc    = Join-Path $GamePath "MelonLoader\net35"

$managedDlls = @(
    "Restory.Assembly.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UI.dll",
    "Sirenix.Serialization.dll",
    "Rewired_Core.dll",
    "RewiredAssembly.dll",
    "Zenject-usage.dll"
)

$melonDlls = @(
    "MelonLoader.dll",
    "0Harmony.dll"
)

# ── Generate stubs ────────────────────────────────────────────────────────────
$libsManaged = Join-Path $PSScriptRoot "libs\Managed"
$libsMelon   = Join-Path $PSScriptRoot "libs\MelonLoader"

New-Item -ItemType Directory -Force -Path $libsManaged | Out-Null
New-Item -ItemType Directory -Force -Path $libsMelon   | Out-Null

$errors = 0

Write-Host "`nGenerating Managed stubs..." -ForegroundColor Yellow
foreach ($dll in $managedDlls) {
    $src = Join-Path $managedSrc $dll
    if (-not (Test-Path $src)) {
        Write-Warning "Not found (skipping): $src"
        continue
    }
    dotnet $stubExe $src $libsManaged
    if ($LASTEXITCODE -ne 0) { $errors++ }
}

Write-Host "`nGenerating MelonLoader stubs..." -ForegroundColor Yellow
foreach ($dll in $melonDlls) {
    $src = Join-Path $melonSrc $dll
    if (-not (Test-Path $src)) {
        Write-Warning "Not found (skipping): $src"
        continue
    }
    dotnet $stubExe $src $libsMelon
    if ($LASTEXITCODE -ne 0) { $errors++ }
}

# ── Summary ───────────────────────────────────────────────────────────────────
if ($errors -gt 0) {
    Write-Error "$errors stub(s) failed to generate. Check the output above."
} else {
    Write-Host "`nAll stubs generated successfully!" -ForegroundColor Green
    Write-Host "Files written to:" -ForegroundColor Cyan
    Write-Host "  $libsManaged" -ForegroundColor White
    Write-Host "  $libsMelon" -ForegroundColor White
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "  git add libs/" -ForegroundColor White
    Write-Host "  git commit -m 'chore: add/update reference stub DLLs for CI'" -ForegroundColor White
    Write-Host "  git push" -ForegroundColor White
}
