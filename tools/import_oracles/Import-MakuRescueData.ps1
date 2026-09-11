# Room $1:$38 is the first Maku Sprout rescue. Its placed sprout creates a
# native controller, which in turn creates two scripted Moblin interactions;
# those actors replace themselves with ordinary masked-Moblin enemies. Keep
# all four source script lanes distinct so their original object update order
# and shared wTmpcfc0/wccd4 synchronization remain observable at runtime.
$makuObjectSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\mainData.s')
$makuPlacement = [regex]::Match(
    $makuObjectSource,
    '(?ms)^group1Map38ObjectData:\s*obj_Interaction \$88 \$00 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s*obj_Interaction \$6b \$15 \$(?<statuey>[0-9a-f]{2}) \$(?<statuex>[0-9a-f]{2})')
$makuObjectDataSource = Read-ImportText (
    Join-Path $Disassembly 'objects\ages\extraData3.s')
$makuMoblins = [regex]::Match(
    $makuObjectDataSource,
    '(?ms)^moblinsAttackingMakuSprout:\s*obj_Interaction \$96 \$00 \$(?<y>[0-9a-f]{2}) \$(?<leftx>[0-9a-f]{2})\s*obj_Interaction \$96 \$01 \$[0-9a-f]{2} \$(?<rightx>[0-9a-f]{2})')
if (-not $makuMoblins.Success) {
    # Some disassembly revisions keep dynamic lists in mainData.s.
    $makuMoblins = [regex]::Match(
        $makuObjectSource,
        '(?ms)^moblinsAttackingMakuSprout:\s*obj_Interaction \$96 \$00 \$(?<y>[0-9a-f]{2}) \$(?<leftx>[0-9a-f]{2})\s*obj_Interaction \$96 \$01 \$[0-9a-f]{2} \$(?<rightx>[0-9a-f]{2})')
}
if (-not $makuPlacement.Success -or -not $makuMoblins.Success -or
    $makuPlacement.Groups['y'].Value -ne '28' -or
    $makuPlacement.Groups['x'].Value -ne '50' -or
    $makuPlacement.Groups['statuey'].Value -ne '40' -or
    $makuPlacement.Groups['statuex'].Value -ne '84' -or
    $makuMoblins.Groups['y'].Value -ne '30' -or
    $makuMoblins.Groups['leftx'].Value -ne '68' -or
    $makuMoblins.Groups['rightx'].Value -ne '38') {
    throw 'Room 1:38 Maku Sprout/Moblin placements changed.'
}

$makuScriptsSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scripts.s')
$makuHelperSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuSprout.s')
$makuGateSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuGateOpening.s')
$makuMiscellaneousSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\miscellaneous1.s')
if ($makuScriptsSource -notmatch '(?ms)^makuSprout_subid01Script:.*?GLOBALFLAG_MAKU_TREE_SAVED.*?INTERAC_MISCELLANEOUS_1, \$04, \$40, \$50.*?TX_05d5' -or
    $makuScriptsSource -notmatch '(?ms)^moblin_subid00Script:.*?moblin_spawnEnemyHere.*?^moblin_subid01Script:' -or
    $makuHelperSource -notmatch '(?ms)^interaction6b_subid04Script:.*?wDisableScreenTransitions, \$01.*?INTERAC_MAKU_GATE_OPENING.*?GLOBALFLAG_MAKU_TREE_SAVED.*?wDisableScreenTransitions, \$00') {
    throw 'Room 1:38 rescue script ownership or completion predicates changed.'
}
if ($makuInteractionSource -notmatch '(?ms)^@initSubid0:.*?\.dw @state00.*?\.dw @state10' -or
    $makuInteractionSource -notmatch '(?ms)^@state03:\s*@state04:\s*@state05:\s*ldbc \$01, <TX_0570' -or
    $makuInteractionSource -notmatch '(?ms)^@state06:\s*ldbc \$00, <TX_0576.*?^@state07:\s*ldbc \$00, <TX_0578.*?^@state08:\s*ldbc \$02, <TX_057a' -or
    $makuInteractionSource -notmatch '(?ms)^@state09:\s*ldbc \$01, <TX_057c.*?^@state0a:\s*ldbc \$01, <TX_057e.*?^@state0b:\s*ldbc \$00, <TX_0580' -or
    $makuInteractionSource -notmatch '(?ms)^@state0c:\s*ldbc \$00, <TX_0582.*?^@state0d:\s*ldbc \$01, <TX_0584.*?^@state0e:\s*ldbc \$01, <TX_0586' -or
    $makuInteractionSource -notmatch '(?ms)^@state0f:\s*ldbc \$02, <TX_0588.*?^@state10:.*?checkIsLinkedGame.*?ldbc \$00, <TX_058a.*?ldbc \$01, <TX_058c' -or
    $makuInteractionSource -notmatch '(?ms)^@initializeMakuSprout:.*?interactionSetAlwaysUpdateBit' -or
    $makuInteractionSource -notmatch '(?ms)^@loadScriptAndInitGraphics:.*?>TX_0500') {
    throw 'INTERAC_MAKU_SPROUT state, text, or always-update behavior changed.'
}
if ($makuHelperSource -notmatch '(?ms)^makuSprout_subid00Script_body:.*?@mode00_showDifferentTextFirstTime_distressedAnim:.*?makuSprout_setAnimation, \$02.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$01' -or
    $makuHelperSource -notmatch '(?ms)^@mode01_happyAnimationWhileTalking:.*?makuSprout_setAnimation, \$00.*?makuSprout_setAnimation, \$01.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?wait 1.*?makuSprout_setAnimation, \$00' -or
    $makuHelperSource -notmatch '(?ms)^@mode02_showDifferentTextFirstTime:.*?makuSprout_setAnimation, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$00.*?makuTree_showTextWithOffsetAndUpdateMapText, \$01' -or
    $makuHelperSource -notmatch '(?ms)^makuTree_checkLinkedAndUpdateMapText:.*?wMakuMapTextPresent.*?ld \(hl\),c' -or
    $makuHelperSource -notmatch '(?ms)^makuTree_textOffsetsForLinked:\s*\.db \$20, \$20, \$10') {
    throw 'Maku Sprout advice script modes or linked/map-text helper changed.'
}
if ($makuMiscellaneousSource -notmatch '(?ms)^interaction6b_subid15:.*?GLOBALFLAG_FINISHEDGAME.*?wRoomCollisions.*?\$0f.*?interaction6b_subid0e@state0' -or
    $makuMiscellaneousSource -notmatch '(?ms)^interaction6b_subid0e:.*?TILESETFLAG_PAST.*?PALH_c7.*?ld bc,\$080a.*?cp \$f9.*?ld a,\$04.*?interactionAnimateAsNpc') {
    throw 'Room 1:38 postgame Link-statue behavior changed.'
}

$makuSproutGraphic = $interactionGraphics['136:0']
$makuMoblinGraphic = $interactionGraphics['150:0']
if ($null -eq $makuSproutGraphic -or $null -eq $makuMoblinGraphic -or
    $makuSproutGraphic.Gfx -ne 0x67 -or $makuMoblinGraphic.Gfx -ne 0x90) {
    throw 'Maku Sprout or scripted Moblin graphics changed.'
}
$makuSproutAnimations = @(0..2 | ForEach-Object {
    Resolve-NpcAnimation 0x88 $_
})
$makuMoblinAnimations = @(0..3 | ForEach-Object {
    Resolve-NpcAnimation 0x96 $_
})
$makuStatueGraphic = $interactionGraphics['107:21']
if ($null -eq $makuStatueGraphic -or
    $makuStatueGraphic.Gfx -ne 0x6d -or
    $gfxNames[0x6d] -ne 'spr_linkstatue' -or
    $makuStatueGraphic.TileBase -ne 0 -or
    $makuStatueGraphic.Palette -ne 6 -or
    $makuStatueGraphic.DefaultAnimation -ne 4) {
    throw 'INTERAC_MISCELLANEOUS_1 $6b:$15 graphics changed.'
}
$makuStatueAnimations = @(4..5 | ForEach-Object {
    Resolve-NpcAnimation 0x6b $_
})
$makuStatueProperties = Read-ImportText (
    Join-Path $Disassembly 'gfx_compressible\ages\spr_linkstatue.properties')
if ($makuStatueProperties -notmatch '(?m)^invert:\s*false\s*$') {
    throw 'spr_linkstatue.properties no longer selects non-inverted grayscale.'
}
if (-not $globalFlagValues.ContainsKey('GLOBALFLAG_FINISHEDGAME') -or
    $globalFlagValues['GLOBALFLAG_FINISHEDGAME'] -ne 0x14 -or
    $paletteHeaderSource -notmatch '(?ms)m_PaletteHeaderStart \$c7, PALH_c7\s*m_PaletteHeaderSpr 6, 1, paletteData44f0') {
    throw 'Room 1:38 finished-game flag or past Link-statue palette changed.'
}
if (-not $allTextPositions.ContainsKey(0x05d4) -or
    $allTextPositions[0x05d4] -ne 2) {
    throw 'TX_05d4 no longer explicitly selects textbox position 2.'
}

$makuAdviceDefinitions = @(
    [pscustomobject]@{ State=0x00; StandardMode=0; StandardBase=0x0500; LinkedMode=0; LinkedBase=0x0520 },
    [pscustomobject]@{ State=0x03; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x04; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x05; StandardMode=1; StandardBase=0x0570; LinkedMode=1; LinkedBase=0x0590 },
    [pscustomobject]@{ State=0x06; StandardMode=0; StandardBase=0x0576; LinkedMode=0; LinkedBase=0x0596 },
    [pscustomobject]@{ State=0x07; StandardMode=0; StandardBase=0x0578; LinkedMode=0; LinkedBase=0x0598 },
    [pscustomobject]@{ State=0x08; StandardMode=2; StandardBase=0x057a; LinkedMode=2; LinkedBase=0x059a },
    [pscustomobject]@{ State=0x09; StandardMode=1; StandardBase=0x057c; LinkedMode=1; LinkedBase=0x059c },
    [pscustomobject]@{ State=0x0a; StandardMode=1; StandardBase=0x057e; LinkedMode=1; LinkedBase=0x059e },
    [pscustomobject]@{ State=0x0b; StandardMode=0; StandardBase=0x0580; LinkedMode=0; LinkedBase=0x05a0 },
    [pscustomobject]@{ State=0x0c; StandardMode=0; StandardBase=0x0582; LinkedMode=0; LinkedBase=0x05a2 },
    [pscustomobject]@{ State=0x0d; StandardMode=1; StandardBase=0x0584; LinkedMode=1; LinkedBase=0x05a4 },
    [pscustomobject]@{ State=0x0e; StandardMode=1; StandardBase=0x0586; LinkedMode=1; LinkedBase=0x05a6 },
    [pscustomobject]@{ State=0x0f; StandardMode=2; StandardBase=0x0588; LinkedMode=2; LinkedBase=0x05a8 },
    [pscustomobject]@{ State=0x10; StandardMode=1; StandardBase=0x058c; LinkedMode=0; LinkedBase=0x05aa }
)
function Get-MakuAdviceRepeatId([int]$mode, [int]$base) {
    if ($mode -eq 1) { return $base }
    return $base + 1
}
function Get-MakuAdvicePosition([int]$textId) {
    if (-not $allTextPositions.ContainsKey($textId) -or
        $allTextPositions[$textId] -ne 2) {
        throw "Maku Sprout advice TX_$($textId.ToString('x4')) no longer selects textbox position 2."
    }
    return $allTextPositions[$textId]
}
$makuAdviceRows = [Collections.Generic.List[string]]::new()
$makuAdviceRows.Add(
    "# state`tstandard-mode`tlinked-mode`tstandard-first-text-id`tstandard-first-position`tstandard-first-text-base64`tstandard-repeat-text-id`tstandard-repeat-position`tstandard-repeat-text-base64`tlinked-first-text-id`tlinked-first-position`tlinked-first-text-base64`tlinked-repeat-text-id`tlinked-repeat-position`tlinked-repeat-text-base64")
foreach ($definition in $makuAdviceDefinitions) {
    $standardFirst = [int]$definition.StandardBase
    $standardRepeat = Get-MakuAdviceRepeatId $definition.StandardMode $standardFirst
    $linkedFirst = [int]$definition.LinkedBase
    $linkedRepeat = Get-MakuAdviceRepeatId $definition.LinkedMode $linkedFirst
    foreach ($textId in @($standardFirst, $standardRepeat, $linkedFirst, $linkedRepeat)) {
        if (-not $allTexts.ContainsKey($textId)) {
            throw "Missing Maku Sprout advice text TX_$($textId.ToString('x4'))."
        }
    }
    $makuAdviceRows.Add((@(
        $definition.State.ToString('x2'),
        $definition.StandardMode.ToString(),
        $definition.LinkedMode.ToString(),
        $standardFirst.ToString('x4'),
        (Get-MakuAdvicePosition $standardFirst).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$standardFirst]),
        $standardRepeat.ToString('x4'),
        (Get-MakuAdvicePosition $standardRepeat).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$standardRepeat]),
        $linkedFirst.ToString('x4'),
        (Get-MakuAdvicePosition $linkedFirst).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$linkedFirst]),
        $linkedRepeat.ToString('x4'),
        (Get-MakuAdvicePosition $linkedRepeat).ToString(),
        (ConvertTo-CutsceneCommandPayload $allTexts[$linkedRepeat])
    ) -join "`t"))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'objects\maku_sprout_advice.tsv'),
    $makuAdviceRows)

$makuRoomRows = @(
    "# group`troom`tsprout-id`tsprout-subid`tsprout-y`tsprout-x`tsprout-sprite`tsprout-tile-base`tsprout-palette`tsprout-animation-0`tsprout-animation-1`tsprout-animation-2`tsprout-radius-y`tsprout-radius-x`tsaved-flag`tsaved-text-id`tsaved-text-position`tsaved-text-base64`tfinished-flag`tstatue-id`tstatue-subid`tstatue-y`tstatue-x`tstatue-packed-position`tstatue-collision`tstatue-radius-y`tstatue-radius-x`tstatue-appearance-tile`tstatue-normal-animation`tstatue-alternate-animation`tstatue-sprite`tstatue-tile-base`tstatue-palette`tstatue-source-inverted`tsource",
    (@(
        '1','38','88','00',
        $makuPlacement.Groups['y'].Value,
        $makuPlacement.Groups['x'].Value,
        $gfxNames[$makuSproutGraphic.Gfx],
        $makuSproutGraphic.TileBase.ToString(),
        $makuSproutGraphic.Palette.ToString(),
        $makuSproutAnimations[0],
        $makuSproutAnimations[1],
        $makuSproutAnimations[2],
        '08','08','12','05d5','0',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x05d5]),
        $globalFlagValues['GLOBALFLAG_FINISHEDGAME'].ToString('x2'),
        '6b','15',
        $makuPlacement.Groups['statuey'].Value,
        $makuPlacement.Groups['statuex'].Value,
        '48','0f','08','0a','f9',
        $makuStatueAnimations[0],
        $makuStatueAnimations[1],
        $gfxNames[$makuStatueGraphic.Gfx],
        $makuStatueGraphic.TileBase.ToString(),
        $makuStatueGraphic.Palette.ToString(),
        '0',
        'mainData.s:group1Map38ObjectData; makuSprout.s:interactionCode88; miscellaneous1.s:interaction6b_subid15'
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'objects\maku_sprout_room.tsv'),
    $makuRoomRows)
Copy-GeneratedFile `
    'gfx_compressible\ages\spr_linkstatue.png' `
    'gfx\spr_linkstatue.png'
Export-PaletteBlock `
    'paletteData44f0' 4 'objects\maku_sprout_statue_palette.bin'

$makuActorRows = @(
    "# actor`tid`tsubid`ty`tx`tsprite`ttile-base`tpalette`tup-animation`tright-animation`tdown-animation`tleft-animation",
    (@('Sprout','88','00','28','50',$gfxNames[0x67],$makuSproutGraphic.TileBase,$makuSproutGraphic.Palette,
        $makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0],$makuSproutAnimations[0]) -join "`t"),
    (@('MoblinLeft','96','00','30','68',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t"),
    (@('MoblinRight','96','01','30','38',$gfxNames[0x90],$makuMoblinGraphic.TileBase,$makuMoblinGraphic.Palette,
        $makuMoblinAnimations[0],$makuMoblinAnimations[1],$makuMoblinAnimations[2],$makuMoblinAnimations[3]) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_actors.tsv'),
    $makuActorRows)

$makuEventRows = @(
    "# group`troom`tsprout-id`tsprout-subid`tcontroller-y`tcontroller-x`tmoblin-id`tmoblin-y`tleft-x`tright-x`tinitial-gate-position`tclear-tile`tgate-left`tgate-inner-left`tgate-inner-right`tgate-right`troom-flag`tadvice-flag`tsaved-flag`tstate-min`tstate-max`tmap-text-low`ttrigger-radius-y`ttrigger-radius-x`tjump-speed-z`tjump-gravity`tjump-sound`tgate-counter`tshake-counter`tfinal-text-position`tpost-text-id`tpost-text-base64",
    (@('1','38','88','00','40','50','96','30','68','38','52','f9','73','74','75','76','80','3f','12','01','02','d6','04','50','-512','30','53','30','06',
        $allTextPositions[0x05d4].ToString(),'05d5',
        (ConvertTo-CutsceneCommandPayload $allTexts[0x05d5])) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_sprout_rescue_event.tsv'),
    $makuEventRows)

function Write-MakuRescueCommands {
    param(
        [string]$file,
        [string]$script,
        [string]$label,
        [string]$sourceText,
        [object[]]$specs)
    $line = Get-AssemblySourceLine $sourceText "(?m)^$([regex]::Escape($label))\s*:" $label
    $rows = [Collections.Generic.List[string]]::new()
    $rows.Add("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
    for ($index = 0; $index -lt $specs.Count; $index++) {
        $spec = $specs[$index]
        $rows.Add((New-CutsceneCommandRow $script $index $label $line `
            $spec[0] $spec[1] $spec[2] $spec[3] $spec[4]))
    }
    Write-CutsceneGeneratedTable(
        (Join-Path $destination "cutscenes\$file"),
        $rows)
}
function Maku-Text([int]$id) { return $allTexts[$id] }

$sproutSpecs = @(
    @('nativeyield','','','','SpawnController'),
    @('setanimation','Sprout','02','',$makuSproutAnimations[2]),
    @('setcollisionradii','Sprout','08','08',''),
    @('checkmemoryeq','','09','','CutsceneState'),
    @('wait','','2','',''),
    @('nativeblock','','1','','WaitForAtMostOneEnemy'),
    @('jumpifmemoryeq','','00','12','RoomEnemyCount'),
    @('setanimation','Sprout','01','',$makuSproutAnimations[1]),
    @('wait','','90','',''),
    @('setanimation','Sprout','00','',$makuSproutAnimations[0]),
    @('wait','','60','',''),
    @('checkmemoryeq','','00','','RoomEnemyCount'),
    @('setanimation','Sprout','01','',$makuSproutAnimations[1]),
    @('wait','','90','',''),
    @('setanimation','Sprout','00','',$makuSproutAnimations[0]),
    @('setcollisionradii','Sprout','08','08',''),
    @('makeabuttonsensitive','Sprout','','',''),
    @('native','','','','EnterNpcLoop'),
    @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_sprout.tsv' `
    'makuSprout_subid01Script' 'makuSprout_subid01Script' `
    $makuScriptsSource $sproutSpecs

$controllerSpecs = @(
    @('disableinput','','','',''), @('native','','','','RestartSound'),
    @('native','','','','DisableScreenTransitions'), @('native','','','','LoadMoblins'),
    @('wait','','60','',''), @('nativeyield','','','','SpawnInitialPuff'),
    @('wait','','4','',''), @('native','','','','SetInitialGateTile'),
    @('writememory','','01','','CutsceneState'), @('checkmemoryeq','','02','','CutsceneState'),
    @('wait','','30','',''), @('showtext','','1202','',(Maku-Text 0x1202)),
    @('wait','','30','',''), @('writememory','','03','','CutsceneState'),
    @('checkmemoryeq','','04','','CutsceneState'), @('wait','','30','',''),
    @('showtext','','05d0','',(Maku-Text 0x05d0)), @('wait','','30','',''),
    @('nativeyield','','','','PlayDisasterMusic'), @('writememory','','05','','CutsceneState'),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForLinkCollision'),
    @('disableinput','','','',''), @('native','','','','SetLinkUp'),
    @('writememory','','06','','CutsceneState'), @('checkmemoryeq','','08','','CutsceneState'),
    @('wait','','30','',''), @('showtext','','1203','',(Maku-Text 0x1203)),
    @('playsound','','c8','',''), @('wait','','40','',''),
    @('writememory','','09','','CutsceneState'), @('wait','','2','',''),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForAtMostOneEnemy'),
    @('jumpifmemoryeq','','00','39','RoomEnemyCount'), @('wait','','20','',''),
    @('showtext','','05d1','',(Maku-Text 0x05d1)), @('checkmemoryeq','','00','','RoomEnemyCount'),
    @('wait','','20','',''), @('showtext','','05d2','',(Maku-Text 0x05d2)),
    @('wait','','30','',''), @('disableinput','','','',''),
    @('native','','','','RestartSound'), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','20','',''),
    @('playsound','','c8','',''), @('wait','','30','',''),
    @('nativeblock','','1','','MoveLinkToPosition'), @('wait','','1','',''),
    @('checkmemoryeq','','01','','PlayerMoveComplete'), @('wait','','30','',''),
    @('showtext','','05d3','',(Maku-Text 0x05d3)), @('wait','','30','',''),
    @('nativeyield','','','','SpawnGateOpening'), @('checkmemoryeq','','01','','RoomGateOpen'),
    @('wait','','40','',''), @('setglobalflag','','3f','',''),
    @('showtext','','05d6','',(Maku-Text 0x05d6)), @('native','','','','WriteMakuMapText'),
    @('setglobalflag','','12','',''), @('native','','','','IncMakuState'),
    @('native','','','','LayoutSwap'), @('native','','','','ResetMusic'),
    @('enableinput','','','',''), @('nativeblock','','1','','WaitForScreenEdge'),
    @('showtext','','05d4','',(Maku-Text 0x05d4)),
    @('native','','','','EnableScreenTransitions'), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_controller.tsv' `
    'interaction6b_subid04Script' 'interaction6b_subid04Script' `
    $makuHelperSource $controllerSpecs

$leftMoblinSpecs = @(
    @('setanimation','MoblinLeft','03','',$makuMoblinAnimations[3]),
    @('checkmemoryeq','','01','','CutsceneState'),
    @('writeobjectbyte','MoblinLeft','3f','01',''),
    @('jump','MoblinLeft','-512','30','53'),
    @('writeobjectbyte','MoblinLeft','3f','00',''),
    @('writememory','','02','','CutsceneState'),
    @('checkmemoryeq','','05','','CutsceneState'),
    @('writeobjectbyte','MoblinLeft','3f','01',''),
    @('jump','MoblinLeft','-512','30','53'),
    @('writeobjectbyte','MoblinLeft','3f','00',''),
    @('jumpifmemoryeq','','06','13','CutsceneState'),
    @('wait','','30','',''), @('scriptjump','','7','',''),
    @('native','','','','FaceMoblinLeft'), @('native','','','','AddMoblinSync'),
    @('checkmemoryeq','','02','','MoblinSync'), @('native','','','','IncrementCutsceneState'),
    @('checkmemoryeq','','09','','CutsceneState'), @('native','','','','SpawnMaskedMoblinLeft'),
    @('wait','','1','',''), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_moblin_left.tsv' `
    'moblin_subid00Script' 'moblin_subid00Script' `
    $makuScriptsSource $leftMoblinSpecs

$rightMoblinSpecs = @(
    @('setanimation','MoblinRight','01','',$makuMoblinAnimations[1]),
    @('checkmemoryeq','','03','','CutsceneState'),
    @('writeobjectbyte','MoblinRight','3f','01',''),
    @('jump','MoblinRight','-512','30','53'),
    @('writeobjectbyte','MoblinRight','3f','00',''),
    @('writememory','','04','','CutsceneState'),
    @('checkmemoryeq','','05','','CutsceneState'), @('wait','','30','',''),
    @('writeobjectbyte','MoblinRight','3f','01',''),
    @('jump','MoblinRight','-512','30','53'),
    @('writeobjectbyte','MoblinRight','3f','00',''),
    @('jumpifmemoryeq','','06','14','CutsceneState'),
    @('wait','','30','',''), @('scriptjump','','8','',''),
    @('native','','','','FaceMoblinRight'), @('native','','','','AddMoblinSync'),
    @('checkmemoryeq','','02','','MoblinSync'), @('native','','','','IncrementCutsceneState'),
    @('checkmemoryeq','','09','','CutsceneState'), @('native','','','','SpawnMaskedMoblinRight'),
    @('wait','','1','',''), @('scriptend','','','','')
)
Write-MakuRescueCommands 'maku_sprout_rescue_moblin_right.tsv' `
    'moblin_subid01Script' 'moblin_subid01Script' `
    $makuScriptsSource $rightMoblinSpecs
