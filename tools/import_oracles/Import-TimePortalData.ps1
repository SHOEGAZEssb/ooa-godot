# ITEM_HARP ($11) uses the complete LINK_ANIM_MODE_HARP_2 sequence. Export
# the parent-item contract and TX_5110 so playback, song effects, and the
# no-effect message remain source-derived.
$harpParentSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\itemParents\harpFluteParent.s')
$harpAnimationSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\specialObjectAnimationData.s')
if ($harpParentSource -notmatch '(?ms)^parentItemCode_harp:.*?ld a,\$ff ~ DISABLE_LINK ~ DISABLE_ALL_BUT_INTERACTIONS.*?and \$1f.*?objectCreateFloatingMusicNote.*?ld c,\$80.*?ld c,\$40' -or
    $harpParentSource -notmatch '(?s)@state1:\s+ld hl,w1Link.collisionType\s+res 7,\(hl\).*?ld \(wLinkPlayingInstrument\),a.*?and c\s+ret z.*?ld hl,w1Link.collisionType\s+set 7,\(hl\)' -or
    $harpParentSource -notmatch '(?ms)^@harp:.*?and \(TILESETFLAG_UNDERWATER\|TILESETFLAG_SIDESCROLL\|TILESETFLAG_LARGE_INDOORS\|TILESETFLAG_DUNGEON\|TILESETFLAG_INDOORS\|TILESETFLAG_MAKU\).*?\.dw @tuneOfEchoes\s+\.dw @tuneOfCurrents\s+\.dw @tuneOfAges' -or
    $harpParentSource -notmatch '(?ms)^@tuneOfEchoes:.*?ROOMFLAG_BIT_PORTALSPOT_DISCOVERED.*?@tuneOfCurrents:.*?TILESETFLAG_BIT_PAST.*?@tuneOfAges:.*?CUTSCENE_TIMEWARP' -or
    $harpParentSource -notmatch '(?ms)^@sfxList:.*?SND_FILLED_HEART_CONTAINER.*?SND_TUNE_OF_ECHOES.*?SND_TUNE_OF_CURRENTS.*?SND_TUNE_OF_AGES') {
    throw 'ITEM_HARP parent behavior no longer matches the imported playback contract.'
}
if (-not $allTexts.ContainsKey(0x5110)) {
    throw 'ITEM_HARP no-effect text TX_5110 was not decoded.'
}
$harpAnimationMatch = [regex]::Match(
    $harpAnimationSource,
    '(?ms)^animationData19faa:\s*(?<body>.*?)^animationData19fdd:')
if (-not $harpAnimationMatch.Success) {
    throw 'Could not isolate LINK_ANIM_MODE_HARP_2 animationData19faa.'
}
$harpAnimationRows = @([regex]::Matches(
    $harpAnimationMatch.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<graphic>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})\s*$'))
$expectedHarpAnimation = @(
    '14:34:00', '14:35:00', '0c:34:00',
    '14:36:01', '14:37:01', '0c:36:01',
    '14:34:00', '14:35:00', '0c:34:00',
    '14:36:01', '14:37:01', '0c:36:01',
    '14:36:01', '14:37:01', '0c:36:01',
    '01:36:81', '7f:1c:ff')
if ($harpAnimationRows.Count -ne $expectedHarpAnimation.Count) {
    throw "LINK_ANIM_MODE_HARP_2 expected 17 frames, parsed $($harpAnimationRows.Count)."
}
for ($index = 0; $index -lt $expectedHarpAnimation.Count; $index++) {
    $actual = @(
        $harpAnimationRows[$index].Groups['duration'].Value,
        $harpAnimationRows[$index].Groups['graphic'].Value,
        $harpAnimationRows[$index].Groups['parameter'].Value) -join ':'
    if ($actual -ne $expectedHarpAnimation[$index]) {
        throw "LINK_ANIM_MODE_HARP_2 frame $index changed from $($expectedHarpAnimation[$index])."
    }
}
$harpAnimationParameters = @($harpAnimationRows | ForEach-Object {
    $_.Groups['parameter'].Value
}) -join ','
if ($treasureIds['TREASURE_HARP'] -ne 0x11 -or
    $treasureIds['TREASURE_TUNE_OF_ECHOES'] -ne 0x25 -or
    $treasureIds['TREASURE_TUNE_OF_CURRENTS'] -ne 0x26 -or
    $treasureIds['TREASURE_TUNE_OF_AGES'] -ne 0x27 -or
    $soundIds['SND_FILLED_HEART_CONTAINER'] -ne 0x8b -or
    $soundIds['SND_TUNE_OF_ECHOES'] -ne 0xad -or
    $soundIds['SND_TUNE_OF_CURRENTS'] -ne 0xae -or
    $soundIds['SND_TUNE_OF_AGES'] -ne 0xaf) {
    throw 'Harp item, tune treasure, or song sound constants changed.'
}
$harpItemRows = @(
    "# item`tharp-treasure`techoes-treasure`tcurrents-treasure`tages-treasure`tsong-frames`tempty-song-frames`tnote-interval`tprohibited-tileset-mask`tpast-mask`tportal-room-flag`tempty-sound`techoes-sound`tcurrents-sound`tages-sound`tanimation-parameters`tno-effect-text",
    "11`t$($treasureIds['TREASURE_HARP'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_ECHOES'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_CURRENTS'].ToString('x2'))`t$($treasureIds['TREASURE_TUNE_OF_AGES'].ToString('x2'))`t260`t261`t32`t7e`t80`t08`t$($soundIds['SND_FILLED_HEART_CONTAINER'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_ECHOES'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_CURRENTS'].ToString('x2'))`t$($soundIds['SND_TUNE_OF_AGES'].ToString('x2'))`t$harpAnimationParameters`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x5110])))"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'objects\harpItem.tsv'),
    $harpItemRows)

$fluteBody = [regex]::Match($harpAnimationSource, '(?ms)^animationData19f90:(?<body>.*?)^animationData19fa5:')
$fluteAnimation = @([regex]::Matches($fluteBody.Groups['body'].Value,
    '(?m)^\s*\.db \$(?<duration>[0-9a-f]{2}) \$(?<graphic>[0-9a-f]{2}) \$(?<parameter>[0-9a-f]{2})'))
if ($fluteAnimation.Count -ne 7) { throw 'LINK_ANIM_MODE_FLUTE source animation changed.' }
$fluteRows = @("# kind`tindex`tvalue`tsource")
for ($index=0; $index -lt 7; $index++) {
    $fluteRows += "parameter`t$index`t$($fluteAnimation[$index].Groups['parameter'].Value)`tspecialObjectAnimationData.s:animationData19f90"
}
$fluteSoundNames = @('SND_FILLED_HEART_CONTAINER','SND_FLUTE_RICKY','SND_FLUTE_DIMITRI','SND_FLUTE_MOOSH')
for ($index=0; $index -lt 4; $index++) {
    $fluteRows += "sound`t$index`t$($soundIds[$fluteSoundNames[$index]].ToString('x2'))`tharpFluteParent.s:@sfxList"
}
foreach ($textId in @(0x510c,0x510f)) {
    $fluteRows += "text`t$textId`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[$textId])))`tcompanionSpawner.s:@fluteCall"
}
$fluteCallableSource = Read-ImportText (Join-Path $Disassembly 'data\ages\companionCallableRooms.s')
$fluteCallableBytes = @([regex]::Matches($fluteCallableSource, '%([01]{8})'))
if ($fluteCallableBytes.Count -ne 32) { throw 'Ages companionCallableRooms must contain 256 room bits.' }
for ($index=0; $index -lt 256; $index++) {
    if ($fluteCallableBytes[[int][Math]::Floor($index/8)].Groups[1].Value[$index%8] -eq '1') {
        $fluteRows += "room`t$index`t01`tcompanionCallableRooms.s:companionCallableRooms/dbrev"
    }
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\flute.tsv'), $fluteRows)

# INTERAC_TIMEPORTAL_SPAWNER ($e1) is a scenery interaction rather than an
# NPC, but it uses the same interaction graphics, animation, and OAM tables.
# Export every placed portal spot so runtime activation stays data-driven.
$portalSpawnerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\timeportalSpawner.s')
if ($portalSpawnerSource -notmatch '(?ms)^@subid1Init:.*?GLOBALFLAG_MAKU_TREE_SAVED.*?jr nz,@commonInit\s+jr @setSubidBit7\s+^@subid2Init:.*?TREASURE_SEED_SATCHEL.*?jr c,@commonInit\s+^@setSubidBit7:.*?set 7,\(hl\)') {
    throw 'INTERAC_TIMEPORTAL_SPAWNER subtype $01/$02 activation predicates changed.'
}
$portalGraphic = $interactionGraphics['225:0']
if ($null -eq $portalGraphic) {
    throw 'Could not resolve INTERAC_TIMEPORTAL_SPAWNER graphics.'
}
$portalAnimation = Resolve-NpcAnimation 0xe1 $portalGraphic.DefaultAnimation
$portalAnimationLabel = $npcAnimationTables['interactione1Animations'][$portalGraphic.DefaultAnimation]
$portalAnimationBlock = [regex]::Match(
    $interactionAnimationSource,
    "(?ms)^$portalAnimationLabel`:(?<intro>.*?)(?:^${portalAnimationLabel}Loop:)(?<loop>.*?)(?=^interactionAnimation[0-9a-f]+:|\z)")
if (-not $portalAnimation -or -not $portalAnimationBlock.Success) {
    throw 'Could not resolve INTERAC_TIMEPORTAL_SPAWNER graphics and animation.'
}
$portalLoopStart = [regex]::Matches(
    $portalAnimationBlock.Groups['intro'].Value,
    '\.db\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}\s+\$[0-9a-f]{2}').Count
$portalRows = [Collections.Generic.List[string]]::new()
$portalRows.Add("# group`troom`tsubid`ty`tx`tsprite`ttile-base`tpalette`tloop-start`tanimation")
$currentGroup = -1
$currentRoom = -1
foreach ($line in $mainObjectLines) {
    if ($line -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $currentGroup = [Convert]::ToInt32($Matches['group'], 10)
        $currentRoom = [Convert]::ToInt32($Matches['room'], 16)
        continue
    }
    if ($currentGroup -lt 0 -or
        $line -notmatch 'obj_Interaction\s+\$e1\s+\$(?<subid>[0-9a-f]{2})\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})') {
        continue
    }
    $portalRows.Add("$currentGroup`t$($currentRoom.ToString('x2'))`t$($Matches['subid'])`t$($Matches['y'])`t$($Matches['x'])`tspr_makuflower_book_seedling_weirdswirl_block`t$($portalGraphic.TileBase)`t$($portalGraphic.Palette)`t$portalLoopStart`t$portalAnimation")
}
if ($portalRows.Count -ne 22) {
    throw "Expected 21 positioned time-portal spawners, parsed $($portalRows.Count - 1)."
}
if ($portalLoopStart -ne 3) {
    throw "INTERAC_TIMEPORTAL_SPAWNER animation loop moved from frame 3 to $portalLoopStart."
}
$initialPortal = $portalRows | Where-Object { $_ -match '^0\t39\t01\t28\t28\t' }
$makuReturnPortal = $portalRows | Where-Object { $_ -match '^1\t48\t02\t48\t58\t' }
if (-not $initialPortal -or -not $makuReturnPortal) {
    throw 'The initial 0:39 or post-rescue 1:48 active portal was not extracted.'
}
Copy-GeneratedFile `
    'gfx_compressible\ages\spr_makuflower_book_seedling_weirdswirl_block.png' `
    'gfx\spr_makuflower_book_seedling_weirdswirl_block.png'
$portalPath = Join-Path $destination 'objects\timePortals.tsv'
Write-CutsceneGeneratedTable($portalPath, $portalRows)

# $dc:$03/$04 run before the $e1 spawners. They reveal a portal only after
# the covered tile becomes standard ground, and persist each spot separately.
$portalRevealSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous2.s')
$portalRevealTiles = Read-ImportText (
    Join-Path $Disassembly 'constants\common\tileIndices.s')
if ($portalRevealTiles -notmatch '(?m)^\.define TILEINDEX_OVERWORLD_STANDARD_GROUND\s+\$3a\b' -or
    $portalRevealTiles -notmatch '(?m)^\.define TILEINDEX_PORTAL_SPOT\s+\$d7\b') {
    throw 'interactiondc_subid3And4_state1 ground $3a / portal spot $d7 constants changed.'
}
foreach ($variant in @(@('03', '02'), @('04', '04'))) {
    $subid = $variant[0]
    $mask = $variant[1]
    if ($portalRevealSource -notmatch "(?ms)^interactiondc_subid${subid}:\s+call checkInteractionState\s+jr nz,interactiondc_subid3And4_state1\s+@state0:\s+call getThisRoomFlags\s+and \`$$mask\s+jp nz,interactionDelete\s+ld e,Interaction.var03\s+ld a,\`$$mask\s+ld \(de\),a\s+jp interactionIncState") {
        throw "miscellaneous2.s:interactiondc_subid$subid portal reveal initialization changed."
    }
}
if ($portalRevealSource -notmatch '(?ms)^interactiondc_subid3And4_state1:\s+call objectGetTileAtPosition\s+cp TILEINDEX_OVERWORLD_STANDARD_GROUND\s+ret nz\s+ld a,TILEINDEX_PORTAL_SPOT\s+ld c,l\s+call setTile\s+call getThisRoomFlags\s+ld e,Interaction.var03\s+ld a,\(de\)\s+or \(hl\)\s+ld \(hl\),a\s+ld a,SND_SOLVEPUZZLE\s+call playSound\s+jp interactionDelete') {
    throw 'miscellaneous2.s:interactiondc_subid3And4_state1 portal reveal behavior changed.'
}
$portalRevealRows = [Collections.Generic.List[string]]::new()
$portalRevealRows.Add("# group`troom`torder`tsubid`ty`tx`troom-flag`tsource")
$portalRevealGroup = -1
$portalRevealRoom = -1
$portalRevealOrder = 0
foreach ($node in (Read-AssemblyNodes (Join-Path $Disassembly 'objects\ages\mainData.s'))) {
    if ($node.Code -match '^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:') {
        $portalRevealGroup = [int]$Matches['group']
        $portalRevealRoom = [Convert]::ToInt32($Matches['room'], 16)
        $portalRevealOrder = 0
        continue
    }
    if ($portalRevealGroup -lt 0 -or $node.Code -notmatch '^\s*obj_(?!End)') { continue }
    if ($node.Code -match '^\s*obj_Interaction\s+\$dc\s+\$(?<subid>03|04)\s+\$(?<y>[0-9a-f]{2})\s+\$(?<x>[0-9a-f]{2})\s*$') {
        $subid = $Matches['subid']
        $mask = if ($subid -eq '03') { '02' } else { '04' }
        $portalRevealRows.Add("$portalRevealGroup`t$($portalRevealRoom.ToString('x2'))`t$portalRevealOrder`t$subid`t$($Matches['y'])`t$($Matches['x'])`t$mask`tmiscellaneous2.s:interactiondc_subid$subid")
    }
    $portalRevealOrder++
}
if ($portalRevealRows.Count -ne 3 -or
    $portalRevealRows[1] -ne "0`t13`t0`t03`t48`t28`t02`tmiscellaneous2.s:interactiondc_subid03" -or
    $portalRevealRows[2] -ne "0`t13`t1`t04`t48`t78`t04`tmiscellaneous2.s:interactiondc_subid04") {
    throw 'Expected the two ordered $dc:$03/$04 portal reveal placements in room 0:13.'
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\portal_reveals.tsv'), $portalRevealRows)

# Direct Tune of Currents/Ages warps create INTERAC_TIMEPORTAL ($de) at the
# arrival position. Unlike the placed $e1 spawner, it uses common sprites,
# remains visible, cycles OBJ palettes, and is restored from wPortalPos when
# its room is parsed again.
$temporaryPortalSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\timeportal.s')
$timewarpEntryTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\timewarpEntryTileReplacement.s')
$timewarpReturnTileSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\tile_properties\timewarpReturnTileReplacement.s')
$temporaryPortalGraphic = $interactionGraphics['222:0']
if ($null -eq $temporaryPortalGraphic) {
    throw 'Could not resolve INTERAC_TIMEPORTAL graphics.'
}
$temporaryPortalAnimation = Resolve-NpcAnimation `
    0xde $temporaryPortalGraphic.DefaultAnimation
if ($temporaryPortalGraphic.Gfx -ne 0 -or
    $temporaryPortalGraphic.TileBase -ne 0x4a -or
    $temporaryPortalGraphic.Palette -ne 1 -or
    -not $temporaryPortalAnimation -or
    $temporaryPortalSource -notmatch '(?ms)^interactionCodede:.*?ld a,\$03\s+call objectSetCollideRadius.*?ld a,\(wPortalPos\).*?ld a,\$ff\s+ld \(wPortalGroup\),a' -or
    $temporaryPortalSource -notmatch '(?ms)^timeportal_updatePalette:.*?and \$01.*?inc a\s+and \$0b.*?interactionAnimate') {
    throw 'INTERAC_TIMEPORTAL graphics, persistence, collision, or palette behavior changed.'
}
$readTimewarpTileReplacements = {
    param([string]$source, [string]$label)
    $block = [regex]::Match(
        $source,
        ('(?ms)^' + [regex]::Escape($label) +
            ':\s*(?<body>.*?)^\s*\.db \$00\s*$'))
    if (-not $block.Success) {
        throw "Could not isolate $label."
    }
    $rows = @([regex]::Matches(
        $block.Groups['body'].Value,
        '(?m)^\s*\.db \$(?<source>[0-9a-f]{2}) \$(?<replacement>[0-9a-f]{2})(?:\s*;.*)?$'))
    if ($rows.Count -eq 0) {
        throw "$label contains no replacement rows."
    }
    return @($rows | ForEach-Object {
        "$($_.Groups['source'].Value):$($_.Groups['replacement'].Value)"
    }) -join ','
}
$entryTileReplacements = & $readTimewarpTileReplacements `
    $timewarpEntryTileSource 'timewarpEntryTileReplacementDict'
$returnTileReplacements = & $readTimewarpTileReplacements `
    $timewarpReturnTileSource 'timewarpReturnTileReplacementDict'
if ($entryTileReplacements -ne 'c5:3a,c8:3a,04:3a' -or
    $returnTileReplacements -ne
        'c0:3a,c3:3a,c5:3a,c8:3a,ce:3a,db:3a,f2:3a,cd:3a,04:3a') {
    throw 'Time-warp entry/return breakable-tile dictionaries changed.'
}
$temporaryPortalRows = @(
    "# sprite`ttile-base`tpalette`tcontact-radius`tanimation`tentry-tile-replacements`treturn-tile-replacements",
    "spr_common_sprites`t$($temporaryPortalGraphic.TileBase)`t$($temporaryPortalGraphic.Palette)`t9`t$temporaryPortalAnimation`t$entryTileReplacements`t$returnTileReplacements"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'objects\temporaryTimePortal.tsv'),
    $temporaryPortalRows)

# CUTSCENE_TIMEWARP uses INTERAC_TIMEWARP ($dd), PART_TIMEWARP_ANIMATION
# Arrival rejection is a separate Link state machine, including the original
# dbrev room bitset and the Mermaid Suit exception (not ordinary swimming).
$timeWarpLandingRows = @("# kind`tkey`tvalue`tsource")
$invalidWarpPath = Join-Path $Disassembly 'data\ages\tile_properties\timewarpInvalidTiles.s'
$invalidWarpNodes = @(Read-AssemblyDataDirectives $invalidWarpPath 'invalidTimewarpTileList' '.db')
foreach ($node in $invalidWarpNodes) {
    if ($node.Operands.Count -eq 1 -and $node.Operands[0] -eq '$00') { continue }
    if ($node.Operands.Count -ne 2 -or $node.Operands[0] -notmatch '^\$[0-9a-f]{2}$' -or
        $node.Operands[1] -notmatch '^\$0[01]$') {
        throw "$($node.Path):$($node.Line): unsupported invalidTimewarpTileList row."
    }
    $timeWarpLandingRows += "tile`t$($node.Operands[0].Substring(1))`t$($node.Operands[1].Substring(1))`ttimewarpInvalidTiles.s:invalidTimewarpTileList:$($node.Line)"
}
if ($invalidWarpNodes.Count -ne 11) { throw 'invalidTimewarpTileList must have ten pairs and a terminator.' }
$timeWarpCutscenePath = Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s'
$strangeForceIndex = 0
foreach ($node in Read-AssemblyMacroInvocations $timeWarpCutscenePath '@sentBackByStrangeForceTable' 'dbrev') {
    foreach ($operand in $node.Operands) {
        if ($operand -notmatch '^%[01]{8}$') { throw "$($node.Path):$($node.Line): malformed strange-force dbrev byte." }
        foreach ($bit in $operand.Substring(1).ToCharArray()) {
            if ($bit -eq '1') {
                $timeWarpLandingRows += "room`t$($strangeForceIndex.ToString('x2'))`t01`tmiscCutscenes.s:@sentBackByStrangeForceTable:$($node.Line)"
            }
            $strangeForceIndex++
        }
    }
}
if ($strangeForceIndex -ne 256) { throw 'Timewarp strange-force table must contain 256 room bits.' }
if (-not $allTexts.ContainsKey(0x5112)) { throw 'Timewarp TX_5112 was not decoded.' }
$timeWarpLandingRows += "text`t5112`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($allTexts[0x5112])))`tlink.s:warpTransition6"
# Native NPC initialization publishes occupied metatiles independently of
# ordinary Link collision rectangles. Retain handler identity for the owner.
foreach ($path in Get-ChildItem (Join-Path $Disassembly 'object_code\ages\interactions') -File -Filter '*.s' | Sort-Object Name) {
    $nodes = @(Read-AssemblyNodes $path.FullName)
    $mark = @($nodes | Where-Object { $_.Operands -contains 'objectMarkSolidPosition' } | Select-Object -First 1)
    if ($mark.Count -eq 0) { continue }
    $handler = @($nodes | Where-Object { $_.Kind -eq 'Label' -and $_.Name -match '^interactionCode[0-9a-f]{2}(?:_body)?$' } | Select-Object -First 1)
    if ($handler.Count -ne 1 -or $handler[0].Name -notmatch '^interactionCode(?<id>[0-9a-f]{2})(?:_body)?$') {
        throw "$($path.FullName): objectMarkSolidPosition has no interaction handler identity."
    }
    $timeWarpLandingRows += "solid-npc`t$($Matches['id'])`t01`t$($path.Name):$($mark[0].Line):objectMarkSolidPosition"
}
Write-CutsceneGeneratedTable((Join-Path $destination 'objects\timewarp_landing.tsv'), $timeWarpLandingRows)

# CUTSCENE_TIMEWARP uses INTERAC_TIMEWARP ($dd), PART_TIMEWARP_ANIMATION
# ($2b), and INTERAC_SPARKLE ($84:$01) after a portal spawner transfers Link
# to its center. Export the complete source/destination sprite records and the
# two PALH_c1/PALH_c2 beam palettes; runtime should not approximate the effect
# with a full-screen color fade.
$timeWarpSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\timewarp.s')
$timeWarpCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$linkWarpSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\specialObjects\link.s')
$partDataSourceForTimeWarp = Read-ImportText (
    Join-Path $Disassembly 'data\ages\partData.s')
$timeWarpPartSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\parts\timewarpAnimation.s')
$sparkleSourceForTimeWarp = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')

$timeWarpGraphics = @($interactionGraphics['221:0'], $interactionGraphics['221:1'])
$timeWarpTrailGraphic = $interactionGraphics['221:2']
$timeWarpBeamGraphic = $interactionGraphics['221:3']
$sparkleGraphic = $interactionGraphics['132:1']
if ($timeWarpGraphics.Count -ne 2 -or
    $timeWarpGraphics[0].Gfx -ne 0x6a -or $timeWarpGraphics[1].Gfx -ne 0x6a -or
    $timeWarpGraphics[0].TileBase -ne 0 -or $timeWarpGraphics[0].Palette -ne 0 -or
    $timeWarpTrailGraphic.Gfx -ne 0 -or $timeWarpTrailGraphic.TileBase -ne 0x10 -or
    $timeWarpTrailGraphic.Palette -ne 3 -or
    $timeWarpBeamGraphic.Gfx -ne 0x6a -or $timeWarpBeamGraphic.Palette -ne 7 -or
    $sparkleGraphic.Gfx -ne 0x6b -or $sparkleGraphic.TileBase -ne 0x0a -or
    $sparkleGraphic.Palette -ne 2) {
    throw 'INTERAC_TIMEWARP / INTERAC_SPARKLE graphics no longer match the time-portal effect.'
}

$timeWarpAnimations = @(0..5 | ForEach-Object { Resolve-NpcAnimation 0xdd $_ })
$sparkleAnimation = Resolve-NpcAnimation 0x84 $sparkleGraphic.DefaultAnimation
if (($timeWarpAnimations | Where-Object { -not $_ }).Count -ne 0 -or
    -not $sparkleAnimation) {
    throw 'Could not resolve all six INTERAC_TIMEWARP animations and sparkle animation $01.'
}

$timeWarpPart = [regex]::Match(
    $partDataSourceForTimeWarp,
    '(?m)^\s*\.db \$(?<gfx>[0-9a-f]{2}) \$00 \$00 \$00 \$40 \$(?<tile>[0-9a-f]{2}) \$(?<flags>[0-9a-f]{2}) \$00\s*; \$2b')
if (-not $timeWarpPart.Success -or
    [Convert]::ToInt32($timeWarpPart.Groups['gfx'].Value, 16) -ne 0x6a -or
    [Convert]::ToInt32($timeWarpPart.Groups['tile'].Value, 16) -ne 0x1e -or
    [Convert]::ToInt32($timeWarpPart.Groups['flags'].Value, 16) -ne 0x04) {
    throw 'PART_TIMEWARP_ANIMATION no longer resolves to gfx $6a, tile base $1e, palette $04.'
}

# The original Object.visible low bits place the circular $dd:$00/$01 object
# and $2b particles below Link, while the purple $dd:$03/$04 beam, rising
# $dd:$02 trail, and its $84:$01 sparkles are drawn in front of him.
$timeWarpPriorityMatches = @(
    [regex]::Match($timeWarpSource,
        '(?ms)^timewarp_common_state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpSource,
        '(?ms)^itemwarp_subid3Or4_state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpSource,
        '(?ms)^timewarp_subid2:.*?@state0:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($timeWarpPartSource,
        '(?ms)^partCode2b:.*?objectSetVisible8(?<priority>[0-3])'),
    [regex]::Match($sparkleSourceForTimeWarp,
        '(?ms)^@initSubid00:\s*^@initSubid01:.*?objectSetVisible8(?<priority>[0-3])')
)
if (($timeWarpPriorityMatches | Where-Object { -not $_.Success }).Count -ne 0) {
    throw 'Could not resolve all time-warp Object.visible draw priorities.'
}
$timeWarpPriorities = @($timeWarpPriorityMatches | ForEach-Object {
    [Convert]::ToInt32($_.Groups['priority'].Value, 16)
})
if (($timeWarpPriorities -join ',') -ne '3,2,1,3,1') {
    throw "Time-warp ground/beam/trail/particle/sparkle priorities changed from 3,2,1,3,1."
}

$particleBlock = [regex]::Match(
    $timeWarpSource,
    '(?ms)^@data:\s*(?<body>.*?)^timewarp_animateUntilFinished:')
$particleRows = @(
    [regex]::Matches(
        $particleBlock.Groups['body'].Value,
        '(?m)^\s*\.db SPEED_(?<speed>[0-9a-f]+), \$(?<x>[0-9a-f]{2}), \$(?<subid>[0-9a-f]{2}), \$00') |
        ForEach-Object {
            $x = [Convert]::ToInt32($_.Groups['x'].Value, 16)
            if ($x -ge 0x80) { $x -= 0x100 }
            $speedFixed = [Convert]::ToInt32($_.Groups['speed'].Value, 16)
            $subid = [Convert]::ToInt32($_.Groups['subid'].Value, 16)
            "$speedFixed,$x,$subid"
        }
)
if (-not $particleBlock.Success -or $particleRows.Count -ne 8 -or
    ($particleRows -join '|') -ne
        '640,-4,0|704,9,3|576,-9,2|704,4,1|576,-4,0|640,4,1|704,-9,2|576,9,3') {
    throw 'INTERAC_TIMEWARP particle speed/offset/subid table no longer matches its eight records.'
}

# State 1 performs six queued graphics-buffer writes for each of eight masks.
# State 2 then owns independent 120 and 60 update counters. Destination
# transition $06 waits 30, creates the effect, waits 16, and flickers for 30.
if ($timeWarpCutsceneSource -notmatch '(?ms)^func_03_7244:.*?ld a,\$08\s+ld \(\$cbb7\),a.*?@@cbb3_00:.*?@@cbb3_05:.*?ld a,120.*?ld \(wTmpcbb4\),a.*?ld \(hl\),\$3c' -or
    $linkWarpSource -notmatch '(?ms)^warpTransition6:.*?ld \(hl\),\$1e.*?ld \(hl\),\$10.*?SND_TIMEWARP_COMPLETED.*?ld \(hl\),\$1e') {
    throw 'CUTSCENE_TIMEWARP or TRANSITION_DEST_TIMEWARP timing no longer matches 8x6, 120/60, and 30/16/30.'
}
if ($timeWarpCutsceneSource -notmatch '(?ms)^@@cbb3_03:\s+call timewarpCutscene_decCBB4\s+ret nz\s+call fastFadeinFromBlack\s+jp timewarpCutscene_incCBB3\s+@@cbb3_04:\s+ld a,\(wPaletteThread_mode\)\s+or a\s+ret nz\s+call fadeoutToWhite') {
    throw 'CUTSCENE_TIMEWARP no longer hands fastFadeinFromBlack directly to fadeoutToWhite.'
}
if ($timeWarpCutsceneSource -notmatch '(?ms)^func_03_7244:.*?ld a,\(wTilesetFlags\)\s+and \$80\s+ld a,\$02\s+jr nz,\+\s+dec a\s+\+\s+ld l,Interaction.var03\s+ld \(hl\),a\s+ld \(wcc50\),a' -or
    $linkWarpSource -notmatch '(?ms)^@createDestinationTimewarpAnimation:.*?ld a,\(wcc50\)\s+inc l\s+ld \(hl\),a') {
    throw 'Time-warp PALH_c1/PALH_c2 selection no longer carries the source tileset flag through wcc50.'
}

$timeWarpPalette = [byte[]]::new(24)
$timeWarpOutdoorPalette = Read-PaletteBytes 'paletteData5928' 4
$timeWarpIndoorPalette = Read-PaletteBytes 'paletteData5930' 4
[Array]::Copy($timeWarpOutdoorPalette, 0, $timeWarpPalette, 0, 12)
[Array]::Copy($timeWarpIndoorPalette, 0, $timeWarpPalette, 12, 12)
$timeWarpPalettePath = Join-Path $destination 'metadata\time_warp_palettes.bin'
Write-GeneratedBytes($timeWarpPalettePath, $timeWarpPalette)

$timeWarpSprite = $gfxNames[0x6a]
$sparkleSprite = $gfxNames[0x6b]
Copy-GeneratedFile "gfx_compressible\ages\$timeWarpSprite.png" "gfx\$timeWarpSprite.png"
Copy-GeneratedFile "gfx_compressible\ages\$sparkleSprite.png" "gfx\$sparkleSprite.png"
$timeWarpRows = @(
    "# timewarp-sprite`tcommon-sprite`tsparkle-sprite`tprimary-tile-base`tprimary-palette`tbeam-palette`ttrail-tile-base`ttrail-palette`tparticle-tile-base`tparticle-palette`tsparkle-tile-base`tsparkle-palette`tprimary-priority`tbeam-priority`ttrail-priority`tparticle-priority`tsparkle-priority`tdissolve-frames`tsource-effect-frames`tsource-trail-frames`tarrival-wait-frames`tarrival-effect-frames`tarrival-flicker-frames`texpand-animation`tcontract-animation`tbeam-intro-animation`tbeam-loop-animation`tbeam-contract-animation`ttrail-animation`tsparkle-animation`tparticles",
    "$timeWarpSprite`tspr_common_sprites`t$sparkleSprite`t$($timeWarpGraphics[0].TileBase)`t$($timeWarpGraphics[0].Palette)`t$($timeWarpBeamGraphic.Palette)`t$($timeWarpTrailGraphic.TileBase)`t$($timeWarpTrailGraphic.Palette)`t$([Convert]::ToInt32($timeWarpPart.Groups['tile'].Value, 16))`t$([Convert]::ToInt32($timeWarpPart.Groups['flags'].Value, 16) -band 7)`t$($sparkleGraphic.TileBase)`t$($sparkleGraphic.Palette)`t$($timeWarpPriorities -join "`t")`t48`t120`t60`t30`t16`t30`t$($timeWarpAnimations[0])`t$($timeWarpAnimations[1])`t$($timeWarpAnimations[2])`t$($timeWarpAnimations[3])`t$($timeWarpAnimations[4])`t$($timeWarpAnimations[5])`t$sparkleAnimation`t$($particleRows -join '|')"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'objects\timeWarpEffects.tsv'),
    $timeWarpRows)
