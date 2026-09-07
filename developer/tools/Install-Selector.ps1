param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [string]$BuildOutput = "dist"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$game = (Resolve-Path $GameRoot).Path
$source = (Resolve-Path (Join-Path $repoRoot $BuildOutput)).Path
$target = Join-Path $game "BepInEx\plugins\PassTheFearBattleScarSelector"

if (-not (Test-Path -LiteralPath (Join-Path $game "PassTheFear.exe"))) {
    throw "PassTheFear.exe was not found under $game. Check -GameRoot."
}
if (-not (Test-Path -LiteralPath (Join-Path $game "BepInEx"))) {
    throw "BepInEx was not found under $game. Install BepInEx in the game folder first."
}
foreach ($name in @("BattleScarSelector.exe", "PassTheFearBattleScarSelector.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) {
        throw "Missing build output: $(Join-Path $source $name)"
    }
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source "BattleScarSelector.exe") -Destination (Join-Path $target "BattleScarSelector.exe") -Force
Copy-Item -LiteralPath (Join-Path $source "PassTheFearBattleScarSelector.dll") -Destination (Join-Path $target "PassTheFearBattleScarSelector.dll") -Force
Write-Host "Installed the selector and plugin into $target"
Write-Host "Your existing BattleScarSelector.cfg was left unchanged."
