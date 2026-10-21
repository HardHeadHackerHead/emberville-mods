# Builds each mod's own downloads (separate releases from Emberville Multiplayer, never inside its zips):
#   build\<Name>-<ver>.zip                 the mod alone: unzip into the game's BepInEx\plugins\
#   build\store\<team>-<name>-<ver>.zip    the mod-site package (Thunderstore format: manifest.json, icon.png,
#                                          README.md, CHANGELOG.md, plugins\<Name>\...), for a mod with a store.json
#                                          and an icon.png (256 x 256)
#   .\scripts\package.ps1 -MultiplayerVersion 0.2.0 [-GameDir ...] [-MultiplayerDir ...]
#   -MultiplayerVersion: the Emberville Multiplayer version the mod-site packages depend on ({multiplayer_version})
param([string]$MultiplayerVersion = "", [string]$GameDir = "", [string]$MultiplayerDir = "")
$ErrorActionPreference = "Stop"
if (-not $MultiplayerVersion) { throw "pass -MultiplayerVersion: the Emberville Multiplayer version the mod-site packages depend on" }
$mpVersion = $MultiplayerVersion
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot "build.ps1") -GameDir $GameDir -MultiplayerDir $MultiplayerDir
$out = Join-Path $root "build"
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($dir in Get-ChildItem (Join-Path $root "mods") -Directory) {
    $name = $dir.Name
    $dll = Join-Path $dir.FullName "bin\Release\netstandard2.1\$name.dll"
    if (-not (Test-Path $dll)) { "skipped $name (no $name.dll)"; continue }
    # the version: [BepInPlugin(..., "1.0.0")] or a Version constant
    $src = (Get-ChildItem $dir.FullName -Filter "*.cs" | Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
    $m = [regex]::Match($src, 'const string Version = "([^"]+)"')
    if (-not $m.Success) { $m = [regex]::Match($src, 'BepInPlugin\([^)]*"(\d+\.\d+\.\d+)"\)') }
    $version = $m.Groups[1].Value
    $license = Join-Path $dir.FullName "LICENSE.md"
    if (-not (Test-Path $license)) { throw "$name has no LICENSE.md: every mod carries its own license" }
    $stage = Join-Path $out "package-mods\$name"
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$stage\$name" | Out-Null
    Copy-Item $dll "$stage\$name\"
    foreach ($doc in "README.md", "CHANGELOG.md", "LICENSE.md") { if (Test-Path (Join-Path $dir.FullName $doc)) { Copy-Item (Join-Path $dir.FullName $doc) "$stage\$name\" } }
    $zip = Join-Path $out "$name-$version.zip"
    Remove-Item $zip -ErrorAction SilentlyContinue
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    # Its own DLL only: never the multiplayer mod's (players get that from its own download).
    $z = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try { $bad = @($z.Entries | Where-Object { $_.Name -match "^(EmbervilleMP|LiteNetLib|Steamworks\.NET|steam_api64)\." } | ForEach-Object { $_.FullName }) } finally { $z.Dispose() }
    if ($bad.Count) { throw "$zip contains the multiplayer mod's files: $($bad -join ', ')" }
    "{0}  ({1} KB)" -f $zip, [int]((Get-Item $zip).Length / 1KB)

    # The mod-site package, when the mod has its store metadata.
    $storeJson = Join-Path $dir.FullName "store.json"
    if (-not (Test-Path $storeJson)) { continue }
    $cfg = Get-Content $storeJson -Raw | ConvertFrom-Json
    $icon = Join-Path $dir.FullName "icon.png"
    if (-not (Test-Path $icon)) { "  store package skipped: $name needs mods\$name\icon.png (a 256 x 256 PNG)"; continue }
    $deps = @($cfg.dependencies | ForEach-Object { $_ -replace "\{multiplayer_version\}", $mpVersion })
    $manifest = [ordered]@{ name = $cfg.name; version_number = $version; website_url = $cfg.website; description = $cfg.description; dependencies = $deps }
    $sstage = Join-Path $out "package-mods\store-$name"
    Remove-Item -Recurse -Force $sstage -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$sstage\plugins\$name" | Out-Null
    [IO.File]::WriteAllText((Join-Path $sstage "manifest.json"), ($manifest | ConvertTo-Json), (New-Object System.Text.UTF8Encoding $false))   # (no BOM)
    Copy-Item $icon, (Join-Path $dir.FullName "README.md"), (Join-Path $dir.FullName "CHANGELOG.md") $sstage
    Copy-Item $dll, $license "$sstage\plugins\$name\"
    Copy-Item $license $sstage
    New-Item -ItemType Directory -Force (Join-Path $out "store") | Out-Null
    $szip = Join-Path $out "store\$($cfg.team)-$($cfg.name)-$version.zip"
    Remove-Item $szip -ErrorAction SilentlyContinue
    Compress-Archive -Path "$sstage\*" -DestinationPath $szip
    "{0}  ({1} KB; depends on {2})" -f $szip, [int]((Get-Item $szip).Length / 1KB), ($deps -join ", ")
}
