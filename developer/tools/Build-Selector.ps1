param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [string]$AssetRoot = "developer\private-assets",
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$game = (Resolve-Path $GameRoot).Path
$assets = (Resolve-Path (Join-Path $repoRoot $AssetRoot)).Path
$out = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Path $out -Force | Out-Null

# Keep SDK/NuGet state inside the checkout so the build does not depend on a
# machine-wide NuGet.Config or a particular user's profile directory.
$buildState = Join-Path $repoRoot ".build"
$env:APPDATA = Join-Path $buildState "AppData\Roaming"
$env:LOCALAPPDATA = Join-Path $buildState "AppData\Local"
$env:DOTNET_CLI_HOME = Join-Path $buildState "dotnet"
$env:NUGET_PACKAGES = Join-Path $buildState "nuget-packages"
New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES -Force | Out-Null

$required = @(
    (Join-Path $assets "home-background.png"),
    (Join-Path $assets "button-frame.png"),
    (Join-Path $assets "battle_scar_ids.csv"),
    (Join-Path $assets "battle_scar_effects.tsv")
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing private build input: $path" }
}
$icons = @(Get-ChildItem -LiteralPath (Join-Path $assets "icons") -Filter "*.png" -File)
if ($icons.Count -eq 0) { throw "No private icon PNG files found under $assets\icons" }

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { throw "The .NET Framework x64 C# compiler was not found." }
$selectorSource = Join-Path $repoRoot "developer\src\BattleScarSelector\Program.cs"
$selectorExe = Join-Path $out "BattleScarSelector.exe"
$cscArgs = @(
    "/nologo", "/target:winexe", ("/out:" + $selectorExe),
    "/reference:System.dll", "/reference:System.Core.dll",
    "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
    ("/resource:" + (Join-Path $assets "home-background.png") + ",assets.home-background.png"),
    ("/resource:" + (Join-Path $assets "button-frame.png") + ",assets.button-frame.png"),
    ("/resource:" + (Join-Path $assets "battle_scar_ids.csv") + ",data.battle_scar_ids.csv"),
    ("/resource:" + (Join-Path $assets "battle_scar_effects.tsv") + ",data.battle_scar_effects.tsv")
)
foreach ($icon in $icons) {
    $cscArgs += "/resource:$($icon.FullName),icons.$($icon.BaseName).png"
}
$cscArgs += $selectorSource
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Selector compilation failed." }

$pluginProject = Join-Path $repoRoot "developer\src\PassTheFearBattleScarSelector\PassTheFearBattleScarSelector.csproj"
$nugetConfig = Join-Path $repoRoot "developer\NuGet.Config"
& dotnet restore $pluginProject --configfile $nugetConfig --nologo --property:GameRoot=$game
if ($LASTEXITCODE -ne 0) { throw "Plugin restore failed." }
& dotnet build $pluginProject --configuration Release --nologo --no-restore --property:GameRoot=$game
if ($LASTEXITCODE -ne 0) { throw "Plugin compilation failed." }
$pluginDll = Join-Path $repoRoot "developer\src\PassTheFearBattleScarSelector\bin\Release\netstandard2.1\PassTheFearBattleScarSelector.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) { throw "Compiled plugin DLL was not found." }
Copy-Item -LiteralPath $pluginDll -Destination (Join-Path $out "PassTheFearBattleScarSelector.dll") -Force

Write-Host "Built $selectorExe"
Write-Host "Built $(Join-Path $out 'PassTheFearBattleScarSelector.dll')"
