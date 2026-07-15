$ErrorActionPreference = "Stop"
$project = Split-Path $importRoot -Parent
$destination = Join-Path $project "assets\oracle"

# Remove cutscene outputs from their former menu/object categories. They are
# generated again below under cutscenes, which owns their runtime behavior.
foreach ($legacyCutsceneAsset in @(
    'menu\new_game_intro.tsv',
    'menu\new_game_intro_sprites.tsv',
    'objects\maku_tree_cutscene.tsv',
    'objects\ralph_portal_event.tsv'
)) {
    $legacyCutscenePath = Join-Path $destination $legacyCutsceneAsset
    if (Test-Path -LiteralPath $legacyCutscenePath) {
        Remove-Item -LiteralPath $legacyCutscenePath -Force
    }
}

# Remove the eight flat files produced by the original four-room prototype.
# All generated data now lives in purpose-specific subdirectories.
foreach ($legacyName in @(
    'gfx_tileset08.png', 'spr_link.png',
    'tilesetMappings06.bin', 'tilesetCollisions06.bin',
    'room0000.bin', 'room0001.bin', 'room0010.bin', 'room0011.bin'
)) {
    $legacyPath = Join-Path $destination $legacyName
    if (Test-Path -LiteralPath $legacyPath) {
        Remove-Item -LiteralPath $legacyPath -Force
    }
    if (Test-Path -LiteralPath "${legacyPath}.import") {
        Remove-Item -LiteralPath "${legacyPath}.import" -Force
    }
}

if (-not (Test-Path -LiteralPath $Rom)) {
    throw "ROM not found: $Rom"
}

$romBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Rom))
if ($romBytes.Length -ne 1048576) {
    throw "Expected the 1 MiB US Oracle of Ages ROM, got $($romBytes.Length) bytes."
}

$hash = (Get-FileHash -LiteralPath $Rom -Algorithm MD5).Hash
$cleanUsHash = "C4639CC61C049E5A085526BB6CAC03BB"
if ($hash -ne $cleanUsHash) {
    throw "ROM hash $hash is not the supported clean US Oracle of Ages hash $cleanUsHash."
}

function Copy-GeneratedFile([string]$relativeSource, [string]$relativeDestination) {
    $source = Join-Path $Disassembly $relativeSource
    $target = Join-Path $destination $relativeDestination
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Disassembly asset not found: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

