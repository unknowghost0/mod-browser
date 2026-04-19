param(
    [Parameter(Mandatory = $true)] [string]$OutRoot,
    [Parameter(Mandatory = $true)] [string]$Name,
    [string]$Category = "mod",
    [string]$Author = "unknowghost",
    [string]$Maintainers = "",
    [string]$Summary = "One-paragraph summary of what this entry does.",
    [string]$Description = "Describe what your project does, why people would want it, and how to install it.",
    [string]$GameVersion = "v1.9.9.8.5 Steam",
    [string]$CastleForgeVersion = "core-v0.1.0+",
    [string]$License = "MIT",
    [string]$SourceRepo = "https://github.com/you/repo",
    [string]$ReleasesUrl = "",
    [string]$Tags = "community, mod",
    [string]$PreviewFile = "preview.png"
)

$ErrorActionPreference = "Stop"

function Normalize-Category([string]$Value) {
    $raw = if ($null -eq $Value) { "" } else { $Value }
    $raw = $raw.Trim().ToLowerInvariant()
    if ($raw.Contains("texture")) { return "texture-pack" }
    if ($raw.Contains("weapon")) { return "weapon-addon" }
    return "mod"
}

function Get-CategoryFolder([string]$Value) {
    switch (Normalize-Category $Value) {
        "texture-pack" { return "TexturePacks" }
        "weapon-addon" { return "WeaponAddons" }
        default { return "Mods" }
    }
}

function Get-Slug([string]$Value) {
    $raw = if ($null -eq $Value) { "" } else { $Value }
    $raw = $raw.Trim().ToLowerInvariant()
    $chars = New-Object System.Text.StringBuilder
    $lastDash = $false
    foreach ($ch in $raw.ToCharArray()) {
        if ([char]::IsLetterOrDigit($ch)) {
            [void]$chars.Append($ch)
            $lastDash = $false
        }
        elseif (-not $lastDash) {
            [void]$chars.Append('-')
            $lastDash = $true
        }
    }
    $slug = $chars.ToString().Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) { return "new-mod" }
    return $slug
}

function Split-List([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    return $Value.Split(@(',', ';', "`r", "`n"), [System.StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Ensure-PreviewFileName([string]$Value) {
    $raw = if ($null -eq $Value) { "" } else { $Value }
    $name = [System.IO.Path]::GetFileName($raw.Trim())
    if ([string]::IsNullOrWhiteSpace($name)) { $name = "preview.png" }
    if ([string]::IsNullOrWhiteSpace([System.IO.Path]::GetExtension($name))) { $name += ".png" }
    return $name
}

function Write-PlaceholderPreview([string]$Path) {
    if (Test-Path -LiteralPath $Path) { return }
    $ext = [System.IO.Path]::GetExtension($Path)
    if ($ext -ieq ".gif") {
        $bytes = [System.Convert]::FromBase64String("R0lGODdhAQABAIAAAP///////ywAAAAAAQABAAACAkQBADs=")
    }
    else {
        $bytes = [System.Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Zx0kAAAAASUVORK5CYII=")
    }
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

$category = Normalize-Category $Category
$categoryFolder = Get-CategoryFolder $category
$slug = Get-Slug $Name
$previewFileName = Ensure-PreviewFileName $PreviewFile
$maintainerList = Split-List $Maintainers
if ($maintainerList.Count -eq 0) { $maintainerList = @($Author) }
$tagList = Split-List $Tags
if ($tagList.Count -eq 0) { $tagList = @("community", $category) }
if ([string]::IsNullOrWhiteSpace($ReleasesUrl)) { $ReleasesUrl = $SourceRepo.TrimEnd('/') + "/releases" }

$entryRoot = Join-Path $OutRoot (Join-Path $categoryFolder $Name)
New-Item -ItemType Directory -Force -Path $entryRoot | Out-Null

$modJsonPath = Join-Path $entryRoot "mod.json"
$readmePath = Join-Path $entryRoot "README.md"
$previewPath = Join-Path $entryRoot $previewFileName
$guidePath = Join-Path $entryRoot "OPEN-PR.txt"

$manifest = [ordered]@{
    name                = $Name
    slug                = $slug
    category            = $category
    author              = $Author
    maintainers         = $maintainerList
    summary             = $Summary
    game_version        = $GameVersion
    castleforge_version = $CastleForgeVersion
    license             = $License
    source_repo         = $SourceRepo
    releases_url        = $ReleasesUrl
    readme_path         = "$categoryFolder/$Name/README.md"
    preview_path        = "$categoryFolder/$Name/$previewFileName"
    tags                = $tagList
    dependencies        = @()
    conflicts           = @()
    official            = $false
}

$readme = @"
# $Name

- Category: $category
- Author: $Author
- Maintainers: $($maintainerList -join ', ')
- Game Version: $GameVersion
- CastleForge Version: $CastleForgeVersion
- License: $License

## Summary

$Summary

## Description

$Description

## Links

- Source Repo: $SourceRepo
- Releases: $ReleasesUrl

## Install

1. Download the latest release.
2. Follow the project instructions in the source repository or release notes.
3. If this is a gameplay mod DLL, place it in `!Mods`.

## Preview

Replace `$previewFileName` with your real screenshot or GIF before opening your PR.
"@

$guide = @"
CastleForge Community Mods PR guide
==================================

1. Fork https://github.com/RussDev7/CastleForge-CommunityMods
2. Copy this generated folder into $categoryFolder/$Name inside your fork
3. Replace the placeholder preview with your real image or GIF
4. Double-check mod.json, README.md, source_repo, and releases_url
5. Commit your changes
6. Open a pull request back to RussDev7/CastleForge-CommunityMods
"@

[System.IO.File]::WriteAllText($modJsonPath, ($manifest | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($readmePath, $readme.TrimStart() + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($guidePath, $guide.TrimStart() + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
Write-PlaceholderPreview $previewPath

Write-Host "Created community entry scaffold at: $entryRoot"
