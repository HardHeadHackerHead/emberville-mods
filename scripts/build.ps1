# Builds every mod in mods\ against Emberville Multiplayer's DLLs. Throws on failure.
#   .\scripts\build.ps1 [-GameDir <game folder>] [-MultiplayerDir <folder with EmbervilleMP.dll>] [-Deploy]
#   -GameDir:        the game (its Emberville_Data\Managed and BepInEx folders); default: $env:EMB_GAMEDIR, else the
#                    Steam default below
#   -MultiplayerDir: where EmbervilleMP.dll and LiteNetLib.dll are; default: the game's BepInEx\plugins\EmbervilleMP
#                    (the multiplayer mod installed); the multiplayer mod's own test suite passes its build folder
#   -Deploy:         also copy each mod into the game's BepInEx\plugins\<Name>\ (never into EmbervilleMP's folder)
param([string]$GameDir = "", [string]$MultiplayerDir = "", [switch]$Deploy)
$root = Split-Path $PSScriptRoot -Parent
if (-not $GameDir) { $GameDir = $(if ($env:EMB_GAMEDIR) { $env:EMB_GAMEDIR } else { "D:\SteamLibrary\steamapps\common\Emberville Demo" }) }
$props = @("-p:GameDir=$GameDir", "-p:DeployMods=$(if ($Deploy) { 'true' } else { 'false' })")
if ($MultiplayerDir) { $props += "-p:MultiplayerOut=$MultiplayerDir" }
$mpDll = $(if ($MultiplayerDir) { Join-Path $MultiplayerDir "EmbervilleMP.dll" } else { Join-Path $GameDir "BepInEx\plugins\EmbervilleMP\EmbervilleMP.dll" })
if (-not (Test-Path $mpDll)) { throw "Emberville Multiplayer not found at ${mpDll}: install it in the game, or pass -MultiplayerDir" }
foreach ($proj in Get-ChildItem (Join-Path $root "mods") -Recurse -Filter "*.csproj" | Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }) {
    $out = dotnet build $proj.FullName -c Release -nologo @props 2>&1
    $errors = $out | Select-String " error " | Select-Object -Unique
    if ($LASTEXITCODE -ne 0 -or $errors) {
        $errors | ForEach-Object { $_.Line }
        throw "build failed: $($proj.Name)"
    }
}
"build ok"
