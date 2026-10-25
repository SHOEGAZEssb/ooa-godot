# Shared native eye maze (Ages bank1.s; statueEyeball.s subids $03/$04).
$ritesEyeSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/statueEyeball.s')
$ritesBank1 = Read-ImportText (Join-Path $Disassembly 'code/bank1.s')
$ritesWram = Read-ImportText (Join-Path $Disassembly 'include/wram.s')
$ritesMaze = [regex]::Match($ritesBank1, '(?ms)mapTransitionGroup5Data:\s+\.db \$(?<room>f3) \$03 ; EyePuzzle')
$ritesCount = [regex]::Match($ritesEyeSource, '(?ms)^@subid3:\s+call returnIfScrollMode01Unset\s+ld a,\(wEyePuzzleTransitionCounter\)\s+cp \$(?<count>[0-9a-f]{2})\s+ld a,\$00\s+jr nc,\+\+.*?call getRandomNumber\s+and \$03\s+cp \$(?<excluded>[0-9a-f]{2})\s+jr z,--.*?ld \(wEyePuzzleCorrectDirection\),a\s+ld e,\$04\s+jr @spawnChildren')
$ritesSpin = [regex]::Match($ritesEyeSource, '(?ms)^@substate0:\s+ld a,(?<updates>\d+)\s+ld \(de\),a.*?Interaction.counter1\s+ld \(de\),a')
if (!$ritesMaze.Success -or !$ritesCount.Success -or !$ritesSpin.Success -or
    $ritesBank1 -notmatch '(?ms)^screenTransitionEyePuzzle:.*?wEyePuzzleCorrectDirection.*?call clearEyePuzzleVars.*?wEyePuzzleTransitionCounter.*?inc \(hl\).*?\.dw @up\s+\.dw @rightOrLeft\s+\.dw screenTransitionStandard\s+\.dw @rightOrLeft.*?^@up:.*?cp \$06.*?jp screenTransitionStandard' -or
    $ritesEyeSource -notmatch '(?ms)^@@state1:.*?call interactionDecCounter1.*?call interactionIncState.*?wEyePuzzleCorrectDirection.*?ld hl,wFrameCounter\s+inc \(hl\).*?and \$03\s+cp b\s+jr z,--\s+add a\s+jp @offsetPositionTowardLookingDirection' -or
    $ritesWram -notmatch 'wEyePuzzleTransitionCounter: ; \$cc37/' -or
    $ritesWram -notmatch 'wLostWoodsTransitionCounter2: ; \$cc38/' -or
    $ritesWram -notmatch 'wEyePuzzleCorrectDirection: ; \$cca5/') {
    throw 'Room of Rites eye puzzle native transition, spinner or shared-WRAM contract changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects/eye_puzzle.tsv'), @(
    "# group`troom`tcounter-address`tsecond-counter-address`tcorrect-direction-address`trequired-transitions`tspin-updates`texcluded-direction`tsource",
    "5`t$($ritesMaze.Groups['room'].Value)`tcc37`tcc38`tcca5`t$([Convert]::ToInt32($ritesCount.Groups['count'].Value,16))`t$($ritesSpin.Groups['updates'].Value)`t$([Convert]::ToInt32($ritesCount.Groups['excluded'].Value,16))`tbank1.s:screenTransitionEyePuzzle;statueEyeball.s:`$03/`$04"))

$energySource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/finalDungeonEnergy.s')
$energyInitial = [regex]::Match($energySource, '(?ms)^@state0:.*?bit (?<bit>6),a\s+jp nz,interactionDelete\s+set 6,\(hl\).*?call setDeathRespawnPoint.*?ld \(wTextIsActive\),a\s+ld a,(?<wait>\d+)\s+ld e,Interaction.counter1\s+ld \(de\),a\s+ld bc,\$(?<y>[0-9a-f]{2})(?<x>[0-9a-f]{2})\s+jp createEnergySwirlGoingIn')
$energyReveal = [regex]::Match($energySource, '(?ms)^@substate0:.*?call interactionDecCounter1\s+ret nz.*?call interactionIncSubstate\s+ld l,Interaction.counter1\s+ld \(hl\),\$(?<delay>[0-9a-f]{2}).*?wGenericCutscene.cbb3.*?\$00.*?wGenericCutscene.cbba.*?\$ff')
$energyFade = [regex]::Match($energySource, '(?ms)^@substate1:.*?or a\s+jr nz,\+\+\s+call setLinkForceStateToState08.*?set 7,\(hl\).*?call interactionDecCounter1.*?ld b,\$01\s+call flashScreen.*?call interactionIncSubstate\s+ld a,\$(?<delay>[0-9a-f]{2})\s+jp fadeinFromWhiteWithDelay')
$puzzleSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/miscPuzzles.s')
$decorationSource = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/decoration.s')
if (!$energyInitial.Success -or !$energyReveal.Success -or !$energyFade.Success -or
    $energySource -notmatch '(?ms)^@substate2:.*?wPaletteThread_mode.*?ret nz\s+xor a\s+ld \(wDisabledObjects\),a\s+ld \(wMenuDisabled\),a.*?ld \(wUseSimulatedInput\),a.*?jp interactionDelete' -or
    $puzzleSource -notmatch '(?ms)^miscPuzzles_subid1e:\s+call returnIfScrollMode01Unset\s+ld a,\(wScreenTransitionDirection\)\s+or a\s+jp nz,interactionDelete\s+ld a,SND_SOLVEPUZZLE\s+call playSound\s+jp interactionDelete' -or
    $decorationSource -notmatch '(?ms)^@state1:.*?\.dw interactionAnimate.*?^@state0:\s+call interactionInitGraphics\s+call interactionIncState\s+call objectSetVisible83.*?\.dw @stub') {
    throw 'finalDungeonEnergy.s:$b5:$00, decoration.s:$80:$00 or miscPuzzles.s:$90:$1e entry contract changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects/final_dungeon_entry.tsv'), @(
    "# flag`twait-updates`treveal-delay`tfade-delay`ty`tx`tsource",
    "40`t$($energyInitial.Groups['wait'].Value)`t$([Convert]::ToInt32($energyReveal.Groups['delay'].Value,16))`t$([Convert]::ToInt32($energyFade.Groups['delay'].Value,16))`t$($energyInitial.Groups['y'].Value)`t$($energyInitial.Groups['x'].Value)`tfinalDungeonEnergy.s:interactionCodeb5"))
$entryPlacements = [Collections.Generic.List[string]]::new()
$entryPlacements.Add("# group`troom`torder`tid`tsubid`ty`tx`tsource")
$entryMain = Join-Path $Disassembly 'objects/ages/mainData.s'
foreach ($room in @('f2','f4')) {
    $order = 0
    foreach ($node in Read-AssemblyMacroInvocations $entryMain "group5Map${room}ObjectData" 'obj_Interaction') {
        $v = @($node.Operands | ForEach-Object {Convert-AssemblyInteger $_})
        if ($v.Count -notin @(2,4) -or "$($v[0]):$($v[1])" -notin @('144:30','181:0','128:0')) {
            throw ('Room $5:${0}: unsupported final dungeon entry placement at {1}.' -f $room,$node.Line)
        }
        $y = if ($v.Count -eq 4) {$v[2]} else {0}; $x = if ($v.Count -eq 4) {$v[3]} else {0}
        $entryPlacements.Add("5`t$room`t$order`t$($v[0].ToString('x2'))`t$($v[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`tmainData.s:group5Map${room}ObjectData")
        $order++
    }
}
if ($entryPlacements.Count -ne 4) {throw 'Room $5:$f2/$f4 entry object stream changed.'}
Write-GeneratedTable((Join-Path $destination 'objects/final_dungeon_entry_placements.tsv'), $entryPlacements)
$entryGraphic = $interactionGraphics['128:0']
if (!$entryGraphic) {throw 'Decoration $80:$00 graphics are absent.'}
$entrySprite = $gfxNames[$entryGraphic.Gfx]; Copy-EnemySprite $entrySprite
$entryAnimation = Resolve-NpcAnimation 0x80 $entryGraphic.DefaultAnimation
if (!$entryAnimation) {throw 'Decoration $80:$00 animation is absent.'}
Write-GeneratedTable((Join-Path $destination 'objects/final_dungeon_decoration.tsv'), @(
    "# sprite`ttile-base`tpalette`tsource-grayscale-inverted`tanimation`tsource",
    "$entrySprite`t$($entryGraphic.TileBase)`t$($entryGraphic.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $entrySprite))`t$entryAnimation`tdecoration.s:`$80:`$00;interactionData.s"))

$flameSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/twinrovaFlame.s')
$approachSource = Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/zeldaApproachTrigger.s')
if ($flameSource -notmatch '(?ms)call interactionInitGraphics\s+call interactionSetAlwaysUpdateBit.*?cp \$03.*?call getThisRoomFlags\s+and \$80\s+jp nz,interactionDelete.*?and \$03.*?add a\s+add a\s+add a\s+ld l,Interaction.animCounter.*?add \(hl\)\s+ld \(hl\),a' -or
    $approachSource -notmatch '(?ms)^@state0:.*?ROOMFLAG_80.*?PALH_ac.*?^@state1:\s+call checkLinkVulnerable.*?SCROLLMODE_08 \| SCROLLMODE_04 \| SCROLLMODE_02.*?cp \$09.*?CUTSCENE_WARP_TO_TWINROVA_FIGHT.*?wMenuDisabled.*?INTERAC_TWINROVA_FLAME.*?res 7,\(hl\)') {
    throw 'twinrovaFlame.s / zeldaApproachTrigger.s ritual initialization or approach contract changed.'
}
# Select the Ages rows explicitly; the surrounding table also contains Seasons-only pairs.
$positionMatch = [regex]::Match($flameSource, '(?ms)\t\.db \$40 \$a8\s+\.db \$40 \$48\s+\.db \$10 \$78.*?\.endif\s+\.db \$50 \$a8\s+\.db \$50 \$48\s+\.db \$20 \$78\s+\.db \$50 \$a8\s+\.db \$50 \$48\s+\.db \$20 \$78')
if (!$positionMatch.Success) {throw 'twinrovaFlame.s: Ages positions changed.'}
$ritualRows = [Collections.Generic.List[string]]::new()
$ritualRows.Add("# subid`ty`tx`tsprite`ttile-base`tpalette`tinverted`tanimation`tsave-room`tsave-add`tsource")
$flameAnimationSource=Read-ImportText (Join-Path $Disassembly 'data/ages/interactionAnimations.s')
$flameStructSource=Read-ImportText (Join-Path $Disassembly 'include/structs.s')
$flamePointer=[regex]::Match($flameAnimationSource,'(?ms)^interactiona9OamDataPointers:.*?\.dw interactionOamData(?<address>[0-9a-f]+)')
if (!$flamePointer.Success -or $flameAnimationSource -notmatch '(?ms)^interactiona9Animations:\s+\.dw interactionAnimation5a358' -or
    $flameAnimationSource -notmatch '(?ms)^interactionAnimation5a358:\s+\.db \$06 \$00 \$00' -or
    $flameStructSource -notmatch '(?ms)\.enum \$40\s+Interaction\s+instanceof InteractionStruct' -or
    $flameStructSource -notmatch '(?m)^\s*animCounter\s+db ; \$20\s*$' -or
    $ritesBank1 -notmatch '(?ms)^cutscene17:.*?^@state1:.*?wGenericCutscene.cbb7.*?\$1e.*?^@state2:.*?updateInteractionsAndDrawAllSprites.*?\$03.*?\$14.*?\$05.*?^@state3:.*?updateInteractionsAndDrawAllSprites.*?fadeoutToWhite.*?^@state4:.*?m_HardcodedWarpA ROOM_TWINROVA_FIGHT, \$05, \$77, \$00') {
    throw 'Ritual flame initial OAM/absolute animCounter or cutscene17 warp contract changed.'
}
# interactionInitGraphics leaves E at oamDataAddress+1 ($5f). Following
# getThisRoomFlags, ld A,(DE) reads that OAM high byte, not the subid.
$flameInitialOamHigh=(([Convert]::ToInt32($flamePointer.Groups['address'].Value,16) -shr 8) -band 0x3f) -bor 0x40
$flameSaveAdd=($flameInitialOamHigh -band 3)*8
$positions = @(@(0x40,0xa8),@(0x40,0x48),@(0x10,0x78),@(0x50,0xa8),@(0x50,0x48),@(0x20,0x78),@(0x50,0xa8),@(0x50,0x48),@(0x20,0x78))
for ($subid=0; $subid -lt 9; $subid++) {
    $graphic=$interactionGraphics["169:$subid"]
    if (!$graphic -or $graphic.DefaultAnimation -ne 0) {throw ('Missing twinrovaFlame $a9:${0:x2} initial graphics/animation.' -f $subid)}
    $sprite=$gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
    $animation=Resolve-NpcAnimation 0xa9 $graphic.DefaultAnimation
    if (!$animation) {throw 'Missing twinrovaFlame animation.'}
    $ritualRows.Add("$($subid.ToString('x2'))`t$($positions[$subid][0].ToString('x2'))`t$($positions[$subid][1].ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$([int](Get-EnemySpriteSourceGrayscaleInverted $sprite))`t$animation`t60`t$($flameSaveAdd.ToString('x2'))`ttwinrovaFlame.s;interactionData.s")
}
Write-GeneratedTable((Join-Path $destination 'objects/twinrova_flames.tsv'),$ritualRows)
Write-GeneratedBytes((Join-Path $destination 'objects/ritual_palette_ac.bin'),(Read-PaletteBytes 'paletteData58f0' 8))
Write-GeneratedBytes((Join-Path $destination 'objects/ritual_palette_af.bin'),(Read-PaletteBytes 'paletteData5900' 4))
$ritualRows.Clear(); $ritualRows.Add("# group`troom`torder`tid`tsubid`ty`tx`tsource")
$order=0
foreach ($node in Read-AssemblyMacroInvocations $entryMain 'group5Mapf1ObjectData' 'obj_Interaction') {
    $v=@($node.Operands | ForEach-Object {Convert-AssemblyInteger $_})
    if ($v.Count -notin @(2,4) -or "$($v[0]):$($v[1])" -notin @('173:0','218:0','169:3','169:4','169:5')) {throw 'Room $5:$f1 ritual object stream changed.'}
    $y=if($v.Count -eq 4){$v[2]}else{0}; $x=if($v.Count -eq 4){$v[3]}else{0}
    $ritualRows.Add("5`tf1`t$order`t$($v[0].ToString('x2'))`t$($v[1].ToString('x2'))`t$($y.ToString('x2'))`t$($x.ToString('x2'))`tmainData.s:group5Mapf1ObjectData")
    $order++
}
if($ritualRows.Count -ne 6){throw 'Room $5:$f1 requires Zelda, one approach trigger and three flames.'}
Write-GeneratedTable((Join-Path $destination 'objects/room_of_rites_ritual.tsv'),$ritualRows)
