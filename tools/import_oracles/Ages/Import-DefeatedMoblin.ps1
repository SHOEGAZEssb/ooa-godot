$native = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/kingMoblinDefeated.s')
$helper = Read-ImportText (Join-Path $Disassembly 'scripts/ages/scriptHelper.s')
$remote = Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/remoteMakuCutscene.s')
if ($mainObjectSource -notmatch '(?ms)^group0Map09ObjectData:\s*obj_Interaction \$72 \$00 \$(?<y>[0-9a-f]{2}) \$(?<x>[0-9a-f]{2})\s') {
    throw 'INTERAC_KING_MOBLIN_DEFEATED: missing room 0:09 placement.'
}
$y=$Matches.y; $x=$Matches.x
if ($native -notmatch '(?s)@subid0State0:.*?bit 6,a.*?GLOBALFLAG_MOBLINS_KEEP_DESTROYED.*?setDeathRespawnPoint.*?ld a,\$80.*?wDisabledObjects.*?wMenuDisabled.*?ld \(hl\),\$38.*?ld \(hl\),\$78.*?ld hl,\$cfd0.*?ld b,\$04.*?clearMemory.*?ld a,\$02.*?fadeinFromWhiteWithDelay' -or
    $helper -notmatch '(?s)kingMoblinDefeated_spawnInteraction8a:.*?INTERAC_REMOTE_MAKU_CUTSCENE.*?Interaction.var03.*?ld \(hl\),\$06' -or
    $remote -notmatch '(?s)@val06:.*?GLOBALFLAG_MOBLINS_KEEP_DESTROYED.*?TX_05b6') {
    throw 'Defeated Moblin native initialization or remote Maku handoff changed.'
}
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# key`tvalues`tsource")
$rows.Add("placement`t$y,$x`tobjects/ages/mainData.s:group0Map09ObjectData")
foreach($spec in @(@('gorons',$native,'@goronData',16),@('directions',$helper,'@directionTable',8))) {
    $scope=if($spec[0] -eq 'directions') { [regex]::Match($helper,'(?s)kingMoblinDefeated_setGoronDirection:.*?kingMoblinDefeated_spawnInteraction8a:').Value } else {$spec[1]}
    $match=[regex]::Match($scope,'(?ms)^'+$spec[2]+':\s*\r?\n(?<body>(?:\s*\.db[^\r\n]+\r?\n)+)')
    $values=@([regex]::Matches(($match.Groups['body'].Value -replace ';[^\r\n]*',''),'\$([0-9a-f]{2})') | ForEach-Object {$_.Groups[1].Value})
    if($values.Count -ne $spec[3]) {throw "Defeated Moblin: malformed $($spec[0]) source table."}
    $rows.Add("$($spec[0])`t$($values -join ',')`tkingMoblinDefeated:$($spec[2])")
}
$rows.Add("speeds`t$((Resolve-ObjectSpeed '180').ToString('x2')),$((Resolve-ObjectSpeed '80').ToString('x2'))`tkingMoblinDefeated.s:initializers")
Write-GeneratedTable((Join-Path $destination 'cutscenes/defeated_moblin_native.tsv'),$rows)
$animations=@{}
foreach($i in 0..7) { $animations[$i]=Resolve-NpcAnimation 0x72 $i; if(!$animations[$i]) {throw "Defeated Moblin animation $i missing."} }
$rows=[Collections.Generic.List[string]]::new()
$rows.Add("# subid`tsprite`ttile-base`tpalette`tdefault-animation`tanimations-base64")
foreach($subid in 0..2) {
    $graphic=$interactionGraphics["114:$subid"]
    if($null -eq $graphic){throw "Defeated Moblin subid $subid graphics missing."}
    $sprite=$gfxNames[$graphic.Gfx]
    $spritePath=if(Test-Path (Join-Path $Disassembly "gfx_compressible/ages/$sprite.png")){"gfx_compressible/ages/$sprite.png"}else{"gfx_compressible/common/$sprite.png"}
    Copy-GeneratedFile $spritePath "gfx/$sprite.png"
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((0..7|ForEach-Object{$animations[$_]}) -join "`n"))
    $rows.Add("$subid`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$($graphic.DefaultAnimation)`t$encoded")
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/defeated_moblin_visuals.tsv'),$rows)
$ops=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($op in @('wait','showtext','writememory','setanimation','applyspeed','scriptend','checkmemoryeq','scriptjump','setspeed','asm15','giveitem')){[void]$ops.Add($op)}
$commands=@(Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') 'kingMoblinDefeated_kingScript' $ops 'ghiniHarassingMoosh_subid00Script')
$bindings=@{'asm15|scriptHelp.kingMoblinDefeated_spawnInteraction8a'=@{Opcode='native';Payload='RemoteMaku'}}
foreach($i in 1..3){$bindings['asm15|scriptHelp.kingMoblinDefeated_setGoronDirection, $'+$i.ToString('x2')]=@{Opcode='native';Payload="Direction$i"}}
$rows=ConvertTo-CutsceneCommandRows $commands 'Actor' -symbols @{
    'wTmpcfc0.genericCutscene.cfd0'='MoblinSignal'; SPEED_100=(Resolve-ObjectSpeed '100'); TREASURE_BOMB_FLOWER=$treasureIds['TREASURE_BOMB_FLOWER']
} -animations $animations -texts $allTexts -positions $allTextPositions -bindings $bindings
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/defeated_moblin_commands.tsv'),$rows)
