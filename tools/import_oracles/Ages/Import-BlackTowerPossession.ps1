$main=Join-Path $Disassembly 'scripts/ages/scripts.s'
$source=Read-ImportText (Join-Path $Disassembly 'object_code/ages/interactions/ambi.s')
if ($source -notmatch '(?ms)^@initSubid06:.*?bit 7,a.*?PALH_85.*?wNumEnemies.*?INTERAC_GHOST_VERAN.*?\$f000.*?SNDCTRL_STOPMUSIC.*?\$cc93.*?LINK_STATE_FORCE_MOVEMENT.*?\$16' -or
    $source -notmatch '(?ms)^ambi_runSubid06:\s*ambi_runSubid07:\s*call interactionRunScript\s+jp nc,interactionAnimate') {
    throw 'ambi.s: $4d:$06/$07 initialization or script dispatch changed.'
}
$visuals=[Collections.Generic.List[string]]::new(); $visuals.Add("# id`tsubid`tsprite`ttile-base`tpalette`tdefault-animation`tsource")
$rows=[Collections.Generic.List[string]]::new();$rows.Add("# id`tindex`tanimation`tsource")
$animations=@{}
foreach ($spec in @(@(0x4d,6),@(0x4d,7),@(0x3e,1),@(0x3e,2))) {
    $id=[int]$spec[0];$subid=[int]$spec[1];$gfx=$interactionGraphics["${id}:$subid"]
    if ($null -eq $gfx) {$gfx=$interactionGraphics["${id}:0"]}
    if ($null -eq $gfx) {throw "Black Tower missing graphics `${id}:`$subid."}
    $sprite=$gfxNames[$gfx.Gfx];Copy-EnemySprite $sprite
    $visuals.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($gfx.TileBase)`t$($gfx.Palette)`t$($gfx.DefaultAnimation)`tdata/ages/interactionData.s")
    if (-not $animations.ContainsKey($id)) {
        $animations[$id]=@{}
        foreach($index in 0..$(if($id -eq 0x4d){10}else{0})) {
            $encoded=Resolve-NpcAnimation $id $index
            if(-not $encoded){throw "Black Tower missing animation `${id}:`$index."}
            $animations[$id][$index]=$encoded
            $rows.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$encoded`tdata/ages/interactionAnimations.s")
        }
    }
}
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_possession_visuals.tsv'),$visuals)
Write-GeneratedTable((Join-Path $destination 'cutscenes/black_tower_possession_animations.tsv'),$rows)
$ops=[Collections.Generic.HashSet[string]]@('disableinput','enableinput','checkcfc0bit','spawnenemyhere','wait','scriptend','showtext','writememory','setspeed','movedown','spawninteraction','checkmemoryeq','applyspeed','xorcfc0bit')
foreach($spec in @(@('intro','ambiSubid06Script','ambiSubid07Script',0x4d),@('rescue','ambiSubid07Script','ambiSubid08Script',0x4d),@('ghost','ghostVeranSubid1Script','boy2Subid0Script',0x3e))) {
    $commands=@(Read-AssemblyCutsceneCommands $main $spec[1] $ops $spec[2]);$bindings=@{}
    foreach($command in $commands) {
        $key="$($command.Opcode)|$($command.Operands)"
        switch($command.Opcode) {
            'spawnenemyhere' {
                if($command.Operands -ne 'ENEMY_VERAN_POSSESSION_BOSS, $01'){throw 'Unexpected Ambi enemy spawn.'}
                $bindings[$key]=@{Opcode='nativeyield';Payload='SpawnBoss'}
            }
            'checkcfc0bit' {$bindings[$key]=@{Opcode='checkmemoryeq';Arg0='01';Payload='GhostSignal'}}
            'xorcfc0bit' {$bindings[$key]=@{Opcode='nativeyield';Payload='ToggleGhost'}}
            'checkmemoryeq' {
                if($command.Operands -ne 'wcc93, $00'){throw 'Unexpected ghost entry gate.'}
                $bindings[$key]=@{Opcode='checkmemoryeq';Arg0='00';Payload='BossShutters'}
            }
            'writememory' {
                if($command.Operands -ne 'wLinkForceState, LINK_STATE_AMBI_UNPOSSESSED_CUTSCENE'){throw 'Unexpected Ambi Link state.'}
                # scriptCmd_writeMemory sets carry: setspeed dispatches in the
                # same update, before the first movedown update.
                $bindings[$key]=@{Opcode='native';Payload='LinkAmbiState'}
            }
            'spawninteraction' {
                if($command.Operands -ne 'INTERAC_GHOST_VERAN, $02, $00, $28'){throw 'Unexpected Ambi ghost spawn.'}
                $bindings[$key]=@{Opcode='nativeyield';Payload='SpawnGhost'}
            }
        }
    }
    Write-GeneratedTable((Join-Path $destination "cutscenes/black_tower_possession_$($spec[0]).tsv"),
        (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols @{'SPEED_180'=0x3c} `
            -texts $allTexts -positions $allTextPositions -animations $animations[[int]$spec[3]] -bindings $bindings -yieldOnJump $false))
}
