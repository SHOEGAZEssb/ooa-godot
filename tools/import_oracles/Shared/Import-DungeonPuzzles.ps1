# Source-owned common dungeon parameters, independent of Ages room catalogs.
$doorControllerSource=Read-ImportText (Join-Path $Disassembly 'object_code/common/interactions/doorController.s')
$commonScriptSource=Read-ImportText (Join-Path $Disassembly 'scripts/common/commonScripts.s')
$commonScriptHelperSource=Read-ImportText (Join-Path $Disassembly 'scripts/common/scriptHelper.s')
foreach($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    [int[]]$enemyStairTiles = Read-MenuRomBytes $pack 'interactionCode12@replacementTiles' 4
    if (($enemyStairTiles -join ',') -ne '70,71,68,69') {
        throw 'dungeonStuff.s:@replacementTiles must map $40..$43 to $46,$47,$44,$45.'
    }
    $doorScriptConstants = [Collections.Generic.List[string]]::new()
    foreach ($entry in [regex]::Matches($doorControllerSource,
        '(?m)^\s*/\* \$(?<subid>1[0-7]) \*/ \.dw mainScripts\.(?<label>\w+)\s*$')) {
        $subid = $entry.Groups['subid'].Value
        $label = $entry.Groups['label'].Value
        $profile = [regex]::Match($commonScriptSource,
            '(?m)^' + [regex]::Escape($label) + ':\s+setcollisionradii \$(?<y>[0-9a-f]{2}), \$(?<x>[0-9a-f]{2})\s+setangle \$(?<angle>[0-9a-f]{2})(?:\s+setspeed \$(?<speed>[0-9a-f]{2}))?')
        if (-not $profile.Success) { throw "commonScripts.s:$label has no supported door radii/angle/speed profile." }
        foreach ($field in @('y','x','angle','speed')) {
            $value = if ($profile.Groups[$field].Success) {
                [Convert]::ToInt32($profile.Groups[$field].Value,16)
            } else { 0 }
            $doorScriptConstants.Add("door-$field-$subid`t$value")
        }
    }
    $torchWait = [regex]::Match($commonScriptSource,
        '(?ms)^doorController_shutUntilTorchesLit:\s+callscript doorController_updateRespawnWhenLinkNotTouching\s+setstate \$03\s+@loop:\s+asm15 scriptHelp\.doorController_checkEnoughTorchesLit\s+jumptable_memoryaddress wTmpcec0\s+\.dw @loop\s+\.dw @torchesLit\s+@torchesLit:\s+wait (?<wait>\d+)\s+playsound SND_SOLVEPUZZLE\s+incstate\s+scriptend')
    $entryFlip = [regex]::Match($commonScriptSource,
        '(?m)^doorController_closeDoorWhenLinkNotTouchingAndFlipcfc0:\s+callscript doorController_updateRespawnWhenLinkNotTouching\s+setstate \$03\s+xorcfc0bit (?<bit>[0-7])\s+scriptend')
    if ($doorScriptConstants.Count -ne 32 -or -not $torchWait.Success -or -not $entryFlip.Success -or
        $commonScriptHelperSource -notmatch '(?ms)^doorController_checkEnoughTorchesLit:\s+ld a,\(wNumTorchesLit\)\s+ld b,a\s+ld e,Interaction.speed\s+ld a,\(de\)\s+cp b\s+ld a,\$01\s+jr z,\+\s+dec a\s+\+\s+ld \(wTmpcec0\),a\s+ret') {
        throw 'INTERAC$1e:$10-$17 entry/torch scripts or exact torch comparison changed.'
    }
    $doorScriptConstants.Add("door-torch-wait`t$($torchWait.Groups['wait'].Value)")
    $doorScriptConstants.Add("door-entry-scratch-mask`t$(1 -shl [int]$entryFlip.Groups['bit'].Value)")
    $rows=@(
        "# key`tvalue"
        $doorScriptConstants
        "enemy-stair-tile-0`t$($enemyStairTiles[0])"
        "enemy-stair-tile-1`t$($enemyStairTiles[1])"
        "enemy-stair-tile-2`t$($enemyStairTiles[2])"
        "enemy-stair-tile-3`t$($enemyStairTiles[3])"
        "pushable-block`t29"
        "push-delay`t30"
        "solve-wait`t8"
        "door-frame-wait`t6"
        "open-tile`t160"
        "closed-up`t120"
        "closed-right`t121"
        "closed-down`t122"
        "closed-left`t123"
        "solve-sound`t77"
        "door-sound`t112"
        "button-tile`t12"
        "pressed-button-tile`t13"
        "button-radius-y`t2"
        "button-radius-x`t2"
        "button-object-release-delay`t28"
        "button-sound`t135"
    )
    if($rows.Count -ne 56){throw "$($pack.Name): expected 55 common dungeon parameter rows, got $($rows.Count-1)."}
    $shutters=Read-MenuRomBytes $pack 'interactionCode1e@shutterTiles' 24
    if(($shutters -join ',') -ne '160,112,160,113,160,114,160,115,160,116,160,117,160,118,160,119,160,120,160,121,160,122,160,123'){
        throw "$($pack.Name): native key/shutter mapping changed."
    }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/dungeon_puzzle_constants.tsv'),$rows)
}
