$pack=@{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
$essence=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/essence.s')
$script=Read-ImportText (Join-Path $Disassembly 'scripts/common/commonScripts.s')
if($essence -notmatch '(?ms)^@state1:.*?and \$03.*?wLinkInAir.*?wLinkGrabState.*?ld b,\$04.*?ld c,\$14.*?cp \$04.*?wDisabledObjects.*?wDisableLinkCollisionsAndMenu' -or
    $script -notmatch '(?ms)^essenceScript_essenceGetCutscene:\s+playsound MUS_ESSENCE\s+asm15 scriptHelp.essence_createEnergySwirl\s+wait 180\s+wait 180\s+playsound SND_FADEOUT\s+wait 20\s+playsound SND_FADEOUT\s+wait 20\s+playsound SND_FADEOUT\s+wait 40\s+playsound SND_FADEOUT\s+asm15 scriptHelp.essence_stopEnergySwirl\s+scriptend') {
    throw 'INTERAC_ESSENCE $7f source approach or get-script timing changed.'
}
$oam=Read-MenuRomBytes $pack 'interaction7f_subid00@essenceOamData' 24
$texts=Read-MenuRomBytes $pack 'interaction7f_subid00@getEssenceTextTable' 8
$warps=Read-MenuRomBytes $pack 'interaction7f_subid00@essenceWarps' 32
$graphics=Read-MenuRomBytes $pack 'interaction7fSubidData' 9
$leafGraphics=Read-MenuRomBytes $pack 'interactionData' (0x49*3)
$sparkleGraphics=Read-MenuRomBytes $pack 'interaction84SubidData' 9
$energyProperties=Read-MenuRomBytes $pack 'partData' (0x54*8)
if(($oam[0..2]-join ',') -ne '20,0,2' -or $texts[0] -ne 14 -or
    ($warps[0..3]-join ',') -ne '128,150,68,1' -or ($graphics-join ',') -ne '98,0,0,91,0,64,91,6,67') {
    throw 'Seasons Fertile Soil Essence visual/text/exit or pedestal/glow graphics changed.'
}
if(($leafGraphics[(0x48*3)..(0x48*3+2)]-join ',') -ne '81,22,0' -or
    ($sparkleGraphics[6..8]-join ',') -ne '80,10,1' -or
    ($energyProperties[(0x53*8)..(0x53*8+7)]-join ',') -ne '107,0,0,0,64,0,4,0' -or
    $seasonsSymbols['wDeleteEnergyBeads'] -ne 0xcd2d) {throw 'Seasons Maku leaf/sparkle or PART$53 native properties changed.'}
Write-GeneratedTable((Join-Path $pack.Root 'objects/root_dungeon_essence.tsv'),@(
    "# index`ttext-id`ttext-base64`tdestination-group`tdestination-room`tdestination-position`tdestination-transition`tsource",
    "0`t000e`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($seasonNpcTexts[0x000e])))`t0`t96`t44`t01`tessence.s:@essenceOamData/@getEssenceTextTable/@essenceWarps"))
$connection=Start-AssemblySourceHost 'ROM_SEASONS;SEASONS_ENGINE;REGION_US;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    $headers=@((Invoke-AssemblySourceHost $connection 'MACRO_INVOCATIONS' "$(Join-Path $Disassembly 'data/seasons/objectGfxHeaders.s')`0objectGfxHeaderTable`0m_ObjectGfxHeader"|ConvertFrom-Json)|Where-Object IsActive)
    $rows=@("# key`tsprites`ttile-base`tpalette`tsource-grayscale-inverted`tanimations-base64")
    foreach($spec in @(@('essence',0x7f,@(2),0x62,20,0),@('pedestal',0x7f,@(0),0x5b,0,4),
        @('glow',0x7f,@(3),0x5b,6,4),@('energy-bead',0x53,(0..15),0x6b,0,4),
        @('maku-leaf',0x48,@(0),0x51,22,0),@('leaf-sparkle',0x84,@(1),0x50,10,0))) {
        $part=$spec[0] -eq 'energy-bead';$kind=if($part){'part'}else{'interaction'}
        $path=Join-Path $Disassembly "data/seasons/$($kind)Animations.s"
        $prefix=$kind+$spec[1].ToString('x2')
        $animations=Read-NativeObjectPointerLabels $pack $path ($prefix+'Animations') $connection
        $poses=Read-NativeObjectPointerLabels $pack $path ($prefix+'OamDataPointers') $connection
        $encoded=@(foreach($index in $spec[2]){(Read-NativeEnemyAnimation $pack $animations[$index] $poses $false ($part -or $spec[0] -eq 'leaf-sparkle')).Encoded}) -join "`n"
        $sprite=$headers[$spec[3]].Operands[0]
        Copy-Item -LiteralPath (Find-ImportSpriteSource $sprite $pack.Name).FullName -Destination (Join-Path $pack.Root "gfx/$sprite.png") -Force
        $rows+="$($spec[0])`t$sprite`t$($spec[4])`t$($spec[5])`t$([int](Get-ImportSpriteSourceGrayscaleInverted $sprite $pack.Name))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($encoded)))"
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/root_dungeon_essence_visuals.tsv'),$rows)
} finally {[void](Invoke-AssemblySourceHost $connection 'QUIT' '');$connection.WaitForExit();$connection.Dispose()}
$energy=Read-ImportText (Join-Path $Disassembly 'object_code/common/parts/blueEnergyBead.s')
if($energy -notmatch 'ld \(hl\),\$78' -or $energy -notmatch 'and \$07' -or
    $energy -notmatch 'ld a,\$38\s+jp objectSetPositionInCircleArc') {throw 'PART_BLUE_ENERGY_BEAD $53 constants changed.'}
Write-GeneratedTable((Join-Path $pack.Root 'objects/blue_energy_bead.tsv'),@(
    "# id`tcount`tspeed`tradius`tdelay-mask`tdelete-address`tsource",
    "53`t08`t78`t38`t07`tcd2d`tobject_code/common/parts/blueEnergyBead.s:partCode53/createEnergySwirlGoingIn_body"))
$rows=@("# profile`tindex`tvalue`tsource")
foreach($spec in @(@('leaf-delays','interactionCode48@table_700e',4),@('leaf-positions','interactionCode48@table_701e',14),
    @('leaf-motion','interactionCode48@table_702c',73),@('maku-map-text','makuTreeTextIndices',13))) {
    $values=Read-MenuRomBytes $pack $spec[1] $spec[2]
    for($index=0;$index -lt $values.Length;$index++){$rows+="$($spec[0])`t$index`t$($values[$index])`t$($spec[1])"}
}
Write-GeneratedTable((Join-Path $pack.Root 'objects/root_dungeon_maku_tables.tsv'),$rows)
$leaf=Read-ImportText (Join-Path $Disassembly 'object_code/seasons/interactions/makuLeaf.s')
$common=Read-ImportText (Join-Path $Disassembly 'code/bank0.s')
if($leaf -notmatch '(?ms)^@state3:.*?Interaction\.var3c.*?call z,playSound\s+\+\s+ld l,\$4d.*?ld l,\$7b.*?call z,@func_7018.*?call interactionDecCounter1' -or
    $common -notmatch '(?ms)^playSound:\s+or a\s+ret z\s+ld h,a.*?ld h,>wMusicQueue\s+ldi \(hl\),a' -or
    ((Read-MenuRomBytes $pack 'interactionCode48@table_701e' 2)-join ',') -ne '24,242') {
    throw 'Maku leaf sparkle reset or post-playSound H=$c0 alias changed.'
}
$path=Join-Path $Disassembly 'scripts/seasons/scripts.s'
$ops=[Collections.Generic.HashSet[string]]::new([string[]]@('disableinput','orroomflag','writememory','setmusic','wait','asm15',
    'checkpalettefadedone','spawninteraction','callscript','resetmusic','enableinput','scriptend'))
$parsed=@(Read-AssemblyCutsceneCommands $path 'makuTreeScript_remoteCutscene' $ops 'script4e25')
$expected=@('disableinput|','orroomflag|$40','writememory|$cbae, $04','setmusic|MUS_MAKU_TREE','wait|40',
    'asm15|hideStatusBar','asm15|scriptHelp.seasonsFunc_15_571a, $02','checkpalettefadedone|',
    'spawninteraction|INTERAC_MAKU_LEAF, $00, $40, $50','wait|240','wait|60','callscript|script4e25',
    'asm15|showStatusBar','asm15|clearFadingPalettes','asm15|fadeinFromWhiteWithDelay, $02','checkpalettefadedone|',
    'resetmusic|','enableinput|','asm15|scriptHelp.seasonsFunc_15_576c','scriptend|')
if($parsed.Count -ne $expected.Count){throw 'Seasons remote Maku script command count changed.'}
$rows=@("# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
for($index=0;$index -lt $parsed.Count;$index++) {
    $cmd=$parsed[$index]
    if(($cmd.Opcode+'|'+$cmd.Operands) -ne $expected[$index]){throw "Remote Maku command$index changed: $($cmd.Opcode) $($cmd.Operands)"}
    $opcode=$cmd.Opcode;$arg='';$payload=''
    switch($opcode) {
        'orroomflag'{$arg='40'}
        'writememory'{$opcode='native';$payload='TextboxFlags'}
        'setmusic'{$arg='1e'}
        'wait'{$arg=$cmd.Operands}
        'asm15'{$opcode='native';$payload=$cmd.Operands}
        'checkpalettefadedone'{$opcode='nativeblock';$arg='1';$payload='PaletteDone'}
        'spawninteraction'{$opcode='nativeyield';$payload='SpawnLeaves'}
        'callscript'{$arg=[string]$parsed.Count}
        'resetmusic'{$opcode='setmusic';$arg='ff'}
    }
    $rows+=New-CutsceneCommandRow 'remote-maku-d1' $index $cmd.Label $cmd.Line $opcode '' $arg '' $payload
}
$ops=[Collections.Generic.HashSet[string]]::new([string[]]@('jumptable_objectbyte','asm15','retscript','jumpifobjectbyteeq','showtext'))
$branches=@(Read-AssemblyCutsceneCommands $path 'script4e25' $ops 'script4e83')
$selected=@($branches | Where-Object {$_.Opcode -eq 'asm15' -and $_.Operands -eq 'scriptHelp.seasonsFunc_15_5735, <TX_1704'})
if($selected.Count -ne 1){throw 'Seasons remote Maku D1 text branch must resolve TX_1704.'}
$index=$parsed.Count
$rows+=New-CutsceneCommandRow 'remote-maku-d1' $index $branches[0].Label $branches[0].Line 'scriptjumpyield' '' ($index+1) '' ''
$rows+=New-CutsceneCommandRow 'remote-maku-d1' ($index+1) $selected[0].Label $selected[0].Line 'native' '' '' '' ("ShowMakuText`0"+$seasonNpcTexts[0x1704])
$return=$branches[$selected[0].Index+1]
if($return.Opcode -ne 'retscript'){throw 'Seasons remote Maku D1 text branch lost its return.'}
$rows+=New-CutsceneCommandRow 'remote-maku-d1' ($index+2) $return.Label $return.Line 'return' '' '' '' ''
$commandPath=Join-Path $pack.Root 'cutscenes/root_dungeon_maku_commands.tsv'
Test-GeneratedCutsceneCommandStreams @([pscustomobject]@{Path=$commandPath;Rows=$rows}) $cutsceneCommandSchemas
Write-GeneratedTable($commandPath,$rows)
