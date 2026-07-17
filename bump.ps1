# KOFplanner Auto-Bump
# Synchronisiert die Versionsnummer in version.json, KOFplanner.csproj und installer.iss,
# erhoeht sie um ein Patch-Inkrement (oder auf eine angegebene Version), committed,
# taggt und pusht atomar auf master.
#
# Aufruf:
#   .\bump.ps1                 -> Patch +1 (z.B. 1.1.58.0 -> 1.1.59.0)
#   .\bump.ps1 -Version 1.2.0.0
#   .\bump.ps1 -Message "Mein Text"
#   .\bump.ps1 -NoPush         -> nur lokal committen + taggen

[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Message = "",
    [switch]$NoPush
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

function Get-CurrentVersion {
    $v = (Get-Content (Join-Path $root "version.json") | ConvertFrom-Json).version
    return $v
}

function Bump-Patch([string]$v) {
    $parts = $v.Split('.')
    if ($parts.Length -ne 4) { throw "Unerwartetes Versionsformat: $v" }
    $parts[3] = ([int]$parts[3] + 1).ToString()
    return $parts -join '.'
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Bump-Patch (Get-CurrentVersion)
}

Write-Host "Neue Version: $Version"

# --- Dateien aktualisieren ---
$csproj = Join-Path $root "KOFplanner.csproj"
$iss = Join-Path $root "installer.iss"
$json = Join-Path $root "version.json"

# csproj: <Version>1.1.58.0</Version>
$cs = Get-Content $csproj
$cs = $cs -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
Set-Content $csproj $cs

# installer.iss: #define MyAppVersion "1.1.58.0"
$is = Get-Content $iss
$is = $is -replace '#define MyAppVersion "[^"]+"', "#define MyAppVersion `"$Version`""
Set-Content $iss $is

# version.json: "version": "1.1.58.0"
$js = Get-Content $json
if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "Release v$Version"
}
$js = $js -replace '"version":\s*"[^"]+"', "`"version`": `"$Version`""
Set-Content $json $js

# --- Git ---
Set-Location $root
git add -A
git commit -q -m "$Message"
git tag -f "v$Version"

if ($NoPush) {
    Write-Host "Lokal erledigt (kein Push). Tag: v$Version"
    exit 0
}

git fetch origin master 2>&1 | Out-Null
git push --atomic origin HEAD:master "v$Version"
Write-Host "Gepusht: v$Version"
