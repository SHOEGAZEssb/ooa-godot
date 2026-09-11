# Room 3:ae contains INTERAC_HARP_OF_AGES_SPAWNER $b3:$00. It creates the
# static Harp treasure and its attached $84:$0c sparkle, then hands the
# post-pickup sequence to the native $36:$07 Nayru wrapper and
# mainScripts.nayruScript07. Export the wrapper constants and a typed expansion
# of that script; INTERAC_PLAY_HARP_SONG remains one bounded native command
# because it drives SPECIALOBJECT_LINK's exact animation state.
$harpSpawnerSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\harpOfAgesSpawner.s')
$harpNayruSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\nayru.s')
$harpSparkleSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\sparkle.s')
$harpSongSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\playHarpSong.s')
$harpScriptPath = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$harpScriptSource = Read-ImportText $harpScriptPath
$harpMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')

if ($mainObjectSource -notmatch
        '(?ms)^group3MapaeObjectData:\s+obj_Interaction \$b3 \$00 \$28 \$58\s+obj_End' -or
    $harpSpawnerSource -notmatch
        '(?ms)^@state0:.*?ROOMFLAG_BIT_ITEM.*?INTERAC_TREASURE.*?TREASURE_HARP.*?ld \(hl\),\$38.*?ld \(hl\),\$58.*?INTERAC_SPARKLE.*?ld \(hl\),\$0c.*?^@state1:.*?SNDCTRL_STOPMUSIC.*?DISABLE_ALL_BUT_INTERACTIONS.*?^@state2:.*?wTextIsActive.*?w1Link\.direction.*?^@state3:.*?set 0,\(hl\).*?ld \(hl\),40.*?fadeoutToBlackWithDelay.*?wDirtyFadeBgPalettes.*?wDirtyFadeSprPalettes.*?hideStatusBar.*?^@state4:.*?wPaletteThread_mode.*?interactionDecCounter1.*?INTERAC_NAYRU.*?ld \(hl\),\$07.*?objectCopyPosition' -or
    $harpNayruSource -notmatch
        '(?ms)^@init07:.*?ld a,\$1e.*?interactionLoadExtraGraphics.*?interactionSetAlwaysUpdateBit.*?^nayruSubid07:.*?interactionDecCounter1.*?xor \$80.*?MUS_NAYRU.*?mainScripts\.nayruScript07.*?rrca.*?cp \$07.*?createMusicNotes.*?wActiveMusic2.*?fadeinFromWhiteWithDelay.*?showStatusBar' -or
    $harpSparkleSource -notmatch
        '(?ms)^@initSubid0c:.*?relatedObj1.*?Interaction\.var38' -or
    $harpSparkleSource -notmatch
        '(?ms)^@runSubid0c:.*?Interaction\.var38.*?interactionDelete.*?objectTakePosition.*?cfc0.*?bit 0,a.*?animateAndFlicker' -or
    $harpSongSource -notmatch
        '(?ms)^@state0:.*?setLinkForceStateToState08.*?ld a,\$04.*?^@state1:.*?interactionDecCounter1.*?ld \(hl\),52.*?LINK_ANIM_MODE_HARP_2.*?^@sounds:\s+\.db SND_TUNE_OF_ECHOES.*?^@state2:.*?^@state4:.*?wFrameCounter.*?and \$1f.*?ld bc,\$f8f8.*?objectCreateFloatingMusicNote.*?^@state3:.*?^@state5:.*?ld bc,\$f808.*?^@state6:.*?set 7,\(hl\).*?LINK_ANIM_MODE_WALK') {
    throw 'Room 3:ae Harp spawner, sparkle, Nayru wrapper, or response-song state machine changed.'
}

$harpOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'wait', 'writememory', 'showtext', 'setanimation',
    'writeobjectbyte', 'asm15', 'xorcfc0bit', 'spawninteraction',
    'checkcfc0bit', 'giveitem', 'scriptend')) {
    [void]$harpOpcodes.Add($opcode)
}
$harpParsed = @(Read-AssemblyCutsceneCommands `
    $harpScriptPath 'nayruScript07' $harpOpcodes)
$harpExpected = @(
    @('wait', '12'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('showtext', 'TX_1d10'),
    @('wait', '16'),
    @('setanimation', '$07'),
    @('writeobjectbyte', 'Interaction.direction, $07'),
    @('asm15', 'playSound, SND_TUNE_OF_ECHOES'),
    @('wait', '210'),
    @('xorcfc0bit', '0'),
    @('wait', '75'),
    @('xorcfc0bit', '0'),
    @('setanimation', '$02'),
    @('writeobjectbyte', 'Interaction.direction, $02'),
    @('wait', '16'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('showtext', 'TX_1d11'),
    @('spawninteraction', 'INTERAC_PLAY_HARP_SONG, $00, $00, $00'),
    @('checkcfc0bit', '7'),
    @('wait', '36'),
    @('writememory', 'wTextboxFlags, TEXTBOXFLAG_ALTPALETTE1'),
    @('giveitem', 'TREASURE_TUNE_OF_ECHOES, $00'),
    @('wait', '16'),
    @('scriptend', '')
)
if ($harpParsed.Count -ne $harpExpected.Count) {
    throw "nayruScript07 expected 23 commands, parsed $($harpParsed.Count)."
}
for ($index = 0; $index -lt $harpExpected.Count; $index++) {
    $actualOperands = if ($null -eq $harpParsed[$index].Operands) {
        ''
    } else {
        ([string]$harpParsed[$index].Operands).Trim()
    }
    if ($harpParsed[$index].Opcode -ne $harpExpected[$index][0] -or
        $actualOperands -ne $harpExpected[$index][1]) {
        throw "nayruScript07 command $index changed from $($harpExpected[$index] -join ' ')."
    }
}

$harpTreasure = $treasureObjectRecords['TREASURE_OBJECT_HARP_00']
$echoTreasure = $treasureObjectRecords['TREASURE_OBJECT_TUNE_OF_ECHOES_00']
$harpNayruGraphic = $interactionGraphics['54:0']
$harpSparkleGraphic = $interactionGraphics['132:12']
$harpNayruExtraSprite = $gfxNames[$harpNayruGraphic.Gfx + 1]
$harpNayruIdleAnimation = Resolve-NpcAnimation 0x36 0x02
$harpNayruSingingAnimation = Resolve-NpcAnimation 0x36 0x07
$harpSparkleAnimation = Resolve-NpcAnimation 0x84 0x00
$harpSparkleGfxHeader = [regex]::Match(
    $objectGfxSource,
    '(?m)^\s*/\* \$3a \*/ m_ObjectGfxHeader spr_link, \$(?<continue>[0-9a-f]{2}), \$(?<source>[0-9a-f]{4})')
$harpSparkleSourceOffset = if ($harpSparkleGfxHeader.Success) {
    [Convert]::ToInt32(
        $harpSparkleGfxHeader.Groups['source'].Value, 16)
} else {
    -1
}
if ($null -eq $harpTreasure -or $harpTreasure.Treasure -ne 0x11 -or
    $harpTreasure.Subid -ne 0 -or $harpTreasure.Parameter -ne 0 -or
    $harpTreasure.TextId -ne 0x71 -or $harpTreasure.Graphic -ne 0x68 -or
    $null -eq $echoTreasure -or $echoTreasure.Treasure -ne 0x25 -or
    $echoTreasure.Subid -ne 0 -or $echoTreasure.Parameter -ne 0 -or
    $echoTreasure.TextId -ne 0x72 -or $echoTreasure.Graphic -ne 0x69 -or
    $null -eq $harpNayruGraphic -or $harpNayruGraphic.Gfx -ne 0x26 -or
    $harpNayruGraphic.TileBase -ne 0 -or $harpNayruGraphic.Palette -ne 1 -or
    $harpNayruGraphic.DefaultAnimation -ne 2 -or
    $harpNayruExtraSprite -ne 'spr_nayru_2' -or
    $objectGfxSource -notmatch
        '/\* \$27 \*/ m_ObjectGfxHeader spr_nayru_2, 1' -or
    [string]::IsNullOrWhiteSpace($harpNayruIdleAnimation) -or
    [string]::IsNullOrWhiteSpace($harpNayruSingingAnimation) -or
    $null -eq $harpSparkleGraphic -or $harpSparkleGraphic.Gfx -ne 0x3a -or
    $harpSparkleGraphic.TileBase -ne 0 -or $harpSparkleGraphic.Palette -ne 0 -or
    $harpSparkleGraphic.DefaultAnimation -ne 0 -or
    -not $harpSparkleGfxHeader.Success -or
    $harpSparkleGfxHeader.Groups['continue'].Value -ne '00' -or
    $harpSparkleSourceOffset -ne 0x1c00 -or
    [string]::IsNullOrWhiteSpace($harpSparkleAnimation) -or
    -not $allTexts.ContainsKey(0x1d10) -or
    -not $allTexts.ContainsKey(0x1d11) -or
    $harpMusicSource -notmatch '(?m)^\s*SND_TUNE_OF_ECHOES\s+db ; \$ad') {
    throw 'Room 3:ae Harp/Tune treasures, Nayru/sparkle visuals, text, or music changed.'
}

$harpEventRows = @(
    "# group`troom`tspawner-id`tspawner-subid`tspawner-y`tspawner-x`tharp-y`tharp-x`troom-flag`tharp-treasure`tharp-object`tsparkle-id`tsparkle-subid`tfade-delay`tfade-frames`tblack-hold`tnayru-id`tnayru-subid`tnayru-flicker`tnayru-music`ttextbox-flags`tsong-sound`tsong-initial-delay`tsong-phase-frames`tsong-phases`tsong-native-frames`tfinal-fade-delay`tfinal-fade-frames`techoes-treasure`techoes-object",
    "3`tae`tb3`t00`t28`t58`t38`t58`t20`t11`tTREASURE_OBJECT_HARP_00`t84`t0c`t2`t65`t40`t36`t07`t30`t08`t04`tad`t4`t52`t4`t214`t4`t129`t25`tTREASURE_OBJECT_TUNE_OF_ECHOES_00"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_event.tsv'),
    $harpEventRows)

$harpVisualRows = @(
    "# key`tid`tsubid`tsprite`textra-sprite`ttile-base`tpalette`tsource-offset`tanimation-0`tanimation-2`tanimation-7",
    "Nayru`t36`t07`t$($gfxNames[$harpNayruGraphic.Gfx])`t$harpNayruExtraSprite`t$($harpNayruGraphic.TileBase)`t$($harpNayruGraphic.Palette)`t0000`t`t$harpNayruIdleAnimation`t$harpNayruSingingAnimation",
    "Sparkle`t84`t0c`t$($gfxNames[$harpSparkleGraphic.Gfx])`t`t$($harpSparkleGraphic.TileBase)`t$($harpSparkleGraphic.Palette)`t$($harpSparkleSourceOffset.ToString('x4'))`t$harpSparkleAnimation`t`t"
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_visuals.tsv'),
    $harpVisualRows)

$harpCommandRows = [Collections.Generic.List[string]]::new()
$harpCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
$harpCommandSpecs = @(
    @($harpParsed[0],  'wait', '', '12', '', ''),
    @($harpParsed[1],  'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[2],  'showtext', '', '1d10', '', $allTexts[0x1d10]),
    @($harpParsed[3],  'wait', '', '16', '', ''),
    @($harpParsed[4],  'setanimation', 'Nayru', '07', '', $harpNayruSingingAnimation),
    @($harpParsed[5],  'writeobjectbyte', 'Nayru', '08', '07', ''),
    @($harpParsed[6],  'playsound', '', 'ad', '', ''),
    @($harpParsed[7],  'wait', '', '210', '', ''),
    @($harpParsed[8],  'native', '', '', '', 'ToggleNayruAnimation'),
    @($harpParsed[9],  'wait', '', '75', '', ''),
    @($harpParsed[10], 'native', '', '', '', 'ToggleNayruAnimation'),
    @($harpParsed[11], 'setanimation', 'Nayru', '02', '', $harpNayruIdleAnimation),
    @($harpParsed[12], 'writeobjectbyte', 'Nayru', '08', '02', ''),
    @($harpParsed[13], 'wait', '', '16', '', ''),
    @($harpParsed[14], 'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[15], 'showtext', '', '1d11', '', $allTexts[0x1d11]),
    @($harpParsed[16], 'nativeblock', '', '214', '', 'PlayHarpSong'),
    @($harpParsed[18], 'wait', '', '36', '', ''),
    @($harpParsed[19], 'writememory', '', '04', '', 'TextboxFlags'),
    @($harpParsed[20], 'giveitem', '', '25', '00', ''),
    @($harpParsed[21], 'wait', '', '16', '', ''),
    @($harpParsed[22], 'scriptend', '', '', '', '')
)
for ($index = 0; $index -lt $harpCommandSpecs.Count; $index++) {
    $spec = $harpCommandSpecs[$index]
    $sourceCommand = $spec[0]
    $harpCommandRows.Add((New-CutsceneCommandRow `
        'nayruScript07' $index $sourceCommand.Label $sourceCommand.Line `
        $spec[1] $spec[2] $spec[3] $spec[4] $spec[5]))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\harp_of_ages_commands.tsv'),
    $harpCommandRows)
