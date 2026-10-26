param(
    [string]$Project = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($Project).TrimEnd('\', '/')
$sourceRoot = Join-Path $projectRoot 'src'
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
    $relative = $file.FullName.Substring($sourceRoot.Length + 1).Replace('\', '/')
    if ($text -match '\bOracleGame\s+\w+\s*=\s*(?:OracleGame\.\w+|default(?:\(OracleGame\))?)\s*[,)]') {
        $violations.Add("$relative declares a default game parameter; callers must select a game explicitly.")
    }
    if (-not $relative.StartsWith('Games/', [StringComparison]::Ordinal) -and
        $relative -ne 'Infrastructure/GeneratedData/OracleAssetPack.cs' -and
        $text -match '(?:AgesGameContent\s*\.\s*AssetRoot|AgesWramAddress\s*\.|res://assets/oracle/ages/)') {
        $violations.Add("$relative binds shared code to Ages content or addresses.")
    }
    foreach ($match in [regex]::Matches(
        $text,
        '\b(?:class|record\s+struct|record|enum)\s+(?<name>(?:SpiritsGrave|WingDungeon)\w*)')) {
        $name = $match.Groups['name'].Value
        if (-not $allowedDungeonOwnedTypes.Contains($name)) {
            $relative = $file.FullName.Substring($projectRoot.Length + 1)
            $violations.Add(
                "$relative declares non-allowlisted dungeon-owned type $name.")
        }
    }

    foreach ($obsolete in $obsoleteSharedTypeNames) {
        if ($text -match "\b$([regex]::Escape($obsolete))\b") {
            $relative = $file.FullName.Substring($projectRoot.Length + 1)
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
            "$($path.Substring($projectRoot.Length + 1)).")
    }
}

foreach ($file in $sourceFiles) {
    $relative = $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
    if ($relative -notmatch '/Features/(?:World/Interactions/Dungeons|World/Entities/Enemies/(?:Core|Species)|Story/Dungeons)/') {
        continue
    }
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '\b(?:SpiritsGrave|WingDungeon)(?:Database|\w*RoomEntity|\w*Behavior)\b') {
        $violations.Add("$relative couples shared runtime code to a dungeon owner.")
    }
}

if ($violations.Count -ne 0) {
    throw (
        "Source ownership audit failed:`n- " +
        ($violations -join "`n- "))
}

Write-Host (
    "Validated source ownership: global dungeon handlers and enemy species " +
    "are independent of dungeon placement owners.")
