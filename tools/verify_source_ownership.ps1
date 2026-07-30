param(
    [string]$Project = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$sourceRoot = Join-Path $Project 'src'
$violations = [Collections.Generic.List[string]]::new()

$allowedDungeonOwnedTypes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
foreach ($name in @(
        'SpiritsGraveDatabase',
        'SpiritsGraveMovingPlatformSpawner',
        'SpiritsGraveTorchStairs',
        'WingDungeonDatabase',
        'WingDungeonStateController',
        'WingDungeonCollapseDatabase',
        'WingDungeonCollapseEvent',
        'WingDungeonCollapseMapRecord',
        'WingDungeonCollapseRecord',
        'WingDungeonCollapseStage'
    )) {
    [void]$allowedDungeonOwnedTypes.Add($name)
}

$obsoleteSharedTypeNames = @(
    'SpiritsGraveColoredCube',
    'SpiritsGraveCubeFlame',
    'SpiritsGraveCubeSensor',
    'SpiritsGraveEssence',
    'SpiritsGraveMovingPlatform',
    'SpiritsGravePuzzleState',
    'SpiritsGraveRewardController',
    'SpiritsGraveVisualEntity',
    'WingDungeonBossProjectile',
    'WingDungeonCircularSidePlatform',
    'WingDungeonEnemyBehavior',
    'WingDungeonEnemyChest',
    'WingDungeonFloorColorChanger',
    'WingDungeonMinecart',
    'WingDungeonMinecartGate',
    'WingDungeonMinecartState',
    'WingDungeonPatternKey',
    'WingDungeonRewardController',
    'WingDungeonSideScrollPlatform',
    'WingDungeonSwitchTileToggler',
    'WingDungeonToggleFloor'
)

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs')
foreach ($file in $sourceFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches(
        $text,
        '\b(?:class|record\s+struct|record|enum)\s+(?<name>(?:SpiritsGrave|WingDungeon)\w*)')) {
        $name = $match.Groups['name'].Value
        if (-not $allowedDungeonOwnedTypes.Contains($name)) {
            $relative = [IO.Path]::GetRelativePath($Project, $file.FullName)
            $violations.Add(
                "$relative declares non-allowlisted dungeon-owned type $name.")
        }
    }

    foreach ($obsolete in $obsoleteSharedTypeNames) {
        if ($text -match "\b$([regex]::Escape($obsolete))\b") {
            $relative = [IO.Path]::GetRelativePath($Project, $file.FullName)
            $violations.Add(
                "$relative still references obsolete shared type $obsolete.")
        }
    }
}

$enemyRoot = Join-Path $sourceRoot 'Features\World\Entities\Enemies'
foreach ($dungeonDirectory in @('SpiritsGrave', 'WingDungeon')) {
    $path = Join-Path $enemyRoot $dungeonDirectory
    if ([IO.Directory]::Exists($path) -and
        @(Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*.cs').Count -gt 0) {
        $violations.Add(
            "Enemy species must not be owned by dungeon directory " +
            "$([IO.Path]::GetRelativePath($Project, $path)).")
    }
}

foreach ($relativeRoot in @(
    'src\Features\World\Interactions\Dungeons',
    'src\Features\World\Entities\Enemies\Core',
    'src\Features\World\Entities\Enemies\Species',
    'src\Features\Story\Dungeons'
)) {
    $path = Join-Path $Project $relativeRoot
    foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*.cs') {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match '\b(?:SpiritsGrave|WingDungeon)(?:Database|\w*RoomEntity|\w*Behavior)\b') {
            $relative = [IO.Path]::GetRelativePath($Project, $file.FullName)
            $violations.Add(
                "$relative couples shared runtime code to a dungeon owner.")
        }
    }
}

if ($violations.Count -ne 0) {
    throw (
        "Dungeon/source ownership audit failed:`n- " +
        ($violations -join "`n- "))
}

Write-Host (
    "Validated source ownership: global dungeon handlers and enemy species " +
    "are independent of dungeon placement owners.")
