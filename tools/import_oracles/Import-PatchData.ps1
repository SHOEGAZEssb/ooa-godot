# INTERAC_PATCH: ROM scripts and copied helper scripts retain distinct jump cadence.
$patchMain = Join-Path $Disassembly 'scripts\ages\scripts.s'
$patchHelper = Join-Path $Disassembly 'scripts\ages\scriptHelper.s'
$patchNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\patch.s')
$patchRoots = @('patch_upstairsRepairTuniNutScript','patch_upstairsRepairSwordScript_body',
    'patch_upstairsRepairedEverythingScript','patch_upstairsMoveToStaircaseScript',
    'patch_downstairsScript_body','patch_duringMinigameScript','patch_linkWonMinigameScript',
    'patch_giveRepairedItem','patch_linkFailedMinigameScript','patch_downstairsAfterBeatingMinigameScript')
$patchNodes = [Collections.Generic.List[object]]::new()
$patchLabels = @{}
foreach ($root in $patchRoots) {
    $copied = $root -in @('patch_upstairsRepairTuniNutScript','patch_upstairsRepairSwordScript_body','patch_downstairsScript_body')
    $path = if ($copied) { $patchHelper } else { $patchMain }
    $patchLabels[$root] = $patchNodes.Count
    foreach ($node in @(Read-AssemblyLabelNodes $path $root)) {
        if ($node.Kind -eq 'Label') {
            $key = "$root/$($node.Name)"
            if ($patchLabels.ContainsKey($key)) { throw "Duplicate Patch label $key" }
            $patchLabels[$key] = $patchNodes.Count
        } elseif ($node.Kind -in @('MacroInvocation','Instruction','Data')) {
            if ($node.Code.Trim().StartsWith('.dw')) {
                $previous = $patchNodes[$patchNodes.Count-1]
                if ($previous.Node.Name -notin @('jumptable_memoryaddress','jumptable_objectbyte')) { throw "Unexpected Patch table at $($node.Line)" }
                $previous.Targets += @($node.Code.Trim().Substring(3).Trim() -split ',\s*')
            } else { $patchNodes.Add([pscustomobject]@{Root=$root; Node=$node; Copied=$copied; Targets=@()}) }
        } elseif ($node.Kind -notin @('Blank','Comment')) { throw "$path`:$($node.Line): unsupported Patch node $($node.Code)" }
    }
}
function Resolve-PatchTarget([string]$root, [string]$label) {
    $label = $label -replace '^mainScripts\.', ''
    $key = if ($label.StartsWith('@') -or $label -eq '++') { "$root/$label" } else { $label }
    if (!$patchLabels.ContainsKey($key)) { throw "Unresolved Patch target $key" }
    return $patchLabels[$key].ToString()
}
function Read-PatchValue([string]$value) {
    switch ($value) {
        'DIR_UP' { return 0 }; 'DIR_RIGHT' { return 1 }; 'DIR_DOWN' { return 2 }; 'DIR_LEFT' { return 3 }
        'TILEINDEX_STANDARD_FLOOR' { return 0xa0 }
        'TREASURE_TRADEITEM' { return 0x41 }
    }
    if ($value -match '^\$([0-9a-f]+)$') { return [Convert]::ToInt32($Matches[1],16) }
    if ($value -match '^\d+$') { return [int]$value }
    throw "Unsupported Patch value '$value'"
}
$patchRows = [Collections.Generic.List[string]]::new()
$patchTextFlags = @{}
$patchRows.Add('# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
foreach ($entry in $patchNodes) {
    $node=$entry.Node; $op=$node.Name.ToLowerInvariant(); $args=([string]$node.OperandText).Trim()
    $parts=@($args -split ',\s*'); $actor=''; $a=''; $b=''; $payload=''
    switch ($op) {
        'initcollisions' { $actor='Patch' }
        'checkabutton' { $actor='Patch' }
        { $_ -in @('showtext','showtextnonexitable') } {
            if ($args -notmatch '^TX_(58[0-9a-f]{2})$') { throw "Unexpected Patch text $args" }
            $a=$Matches[1]; $payload=[string]$allTexts[[Convert]::ToInt32($a,16)]
            if ($a -eq '5800') { $payload=$payload.Substring(0,$payload.Length-2)+"`n"+[string]$allTexts[0x5801] }
            if (!$payload -or $payload.Replace('\call(0xff)', '') -match '\\(?:call|jump)\(') { throw "Unresolved Patch text TX_$a" }
            $patchTextFlags[$a] = if ($op -eq 'showtextnonexitable') { 2 } else { 0 }
            $op='showtext'
        }
        'jumpifmemoryset' {
            if ($parts[0] -ne 'wPastRoomFlags+(<ROOM_AGES_1be)' -or $parts[1] -ne '$06') { throw "Unsupported Patch mask $args" }
            $op='jumpifmemoryeq'; $a='01'; $b=Resolve-PatchTarget $entry.Root $parts[2]; $payload='MetPatch'
        }
        'ormemory' {
            if ($args -ne 'wPastRoomFlags+(<ROOM_AGES_1be), $06') { throw "Unsupported Patch write $args" }
            $op='native'; $payload='MetPatch'
        }
        'jumptable_objectbyte' { $op='jumptablememory'; $payload=$args+'|'+(($entry.Targets | ForEach-Object { Resolve-PatchTarget $entry.Root $_ }) -join ',') }
        'jumptable_memoryaddress' { $op='jumptablememory'; $payload=$args+'|'+(($entry.Targets | ForEach-Object { Resolve-PatchTarget $entry.Root $_ }) -join ',') }
        'jumpiftextoptioneq' { $a=(Read-PatchValue $parts[0]).ToString('x2'); $b=Resolve-PatchTarget $entry.Root $parts[1] }
        'scriptjump' { $a=Resolve-PatchTarget $entry.Root $args; if ($entry.Copied) { $op='scriptjumpyield' } }
        'callscript' { $a=Resolve-PatchTarget $entry.Root $args }
        'retscript' { $op='return' }
        'wait' { $a=(Read-PatchValue $args).ToString() }
        'scriptend' { }
        'setanimation' { $actor='Patch'; $a=(Read-PatchValue $args).ToString('x2'); $payload=Resolve-NpcAnimation 0x94 (Read-PatchValue $args) }
        { $_ -in @('moveup','moveright') } {
            $direction=if ($op -eq 'moveup') {0} else {1}
            $op='move'; $actor='Patch'; $a=($direction*8).ToString('x2'); $b=(Read-PatchValue $args).ToString('x2'); $payload=Resolve-NpcAnimation 0x94 $direction
        }
        'writememory' { $payload=$parts[0]; $a=(Read-PatchValue $parts[1]).ToString('x2') }
        'giveitem' { $reward=$treasureObjectRecords[$args]; if (!$reward) { throw "Missing Patch reward $args" }; $a=$reward.Treasure.ToString('x2'); $b=$reward.Subid.ToString('x2') }
        'asm15' {
            $handler=$parts[0] -replace '^scriptHelp\.', ''
            if ($handler -notin @('patch_jump','patch_updateTextSubstitution','patch_moveLinkPositionAtMinigameEnd','patch_turnToFaceLink','patch_restoreControlAndStairs','patch_setStairTile','fadeoutToWhiteWithDelay','fadeinFromWhiteWithDelay','loseTreasure')) { throw "Unsupported Patch helper $args" }
            $op='native'; $payload=$handler
            if ($parts.Count -eq 2) { $payload+=':'+(Read-PatchValue $parts[1]) }
        }
        default { throw "$($node.Path):$($node.Line): unsupported Patch opcode $op" }
    }
    $patchRows.Add((New-CutsceneCommandRow 'patch' ($patchRows.Count-1) $entry.Root $node.Line $op $actor $a $b $payload))
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\patch_commands.tsv'), $patchRows)
Write-GeneratedTable((Join-Path $destination 'objects\patch_text_flags.tsv'), @('# id`tflags') + @(
    $patchTextFlags.Keys | Sort-Object | ForEach-Object { "$_`t$($patchTextFlags[$_])" }))
Write-GeneratedTable((Join-Path $destination 'objects\patch_scripts.tsv'), @('# name`tentry') + @($patchRoots | ForEach-Object { "$_`t$($patchLabels[$_])" }))
$patchAnimations=@('# index`tanimation')
foreach ($i in 0..12) { $patchAnimations+="$i`t$(Resolve-NpcAnimation 0x94 $i)" }
Write-GeneratedTable((Join-Path $destination 'objects\patch_animations.tsv'), $patchAnimations)
$patchVisuals=@('# subid`tsprite`ttile-base`tpalette`tanimation')
foreach ($subid in @(2,4,5,6,7)) {
    $g=$interactionGraphics["148:$subid"]
    if (!$g -or !$gfxNames.ContainsKey($g.Gfx)) { throw "Missing Patch visual `$94:`$$subid" }
    $patchVisuals+="$subid`t$($gfxNames[$g.Gfx])`t$($g.TileBase)`t$($g.Palette)`t$(Resolve-NpcAnimation 0x94 $g.DefaultAnimation)"
}
Write-GeneratedTable((Join-Path $destination 'objects\patch_visuals.tsv'), $patchVisuals)
$patchExplosion = $interactionGraphics['86:0']
# Graphics index $00 retains the permanently loaded common OBJ tiles; it is
# not entry zero of the dynamically loaded object-graphics table.
$patchFixedGraphics = Read-ImportText (Join-Path $Disassembly 'data\ages\gfxHeaders.s')
if (!$patchExplosion -or $patchExplosion.Gfx -ne 0 -or
    $patchFixedGraphics -notmatch '(?ms)^m_GfxHeaderStart \$83, GFXH_COMMON_SPRITES\s+m_GfxHeader (?<sprite>spr_common_sprites), \$8001\s+m_GfxHeaderEnd') {
    throw 'Patch INTERAC_EXPLOSION $56:$00 no longer selects fixed GFXH_COMMON_SPRITES at $8001.'
}
$patchExplosionSprite = $Matches['sprite']
Write-GeneratedTable((Join-Path $destination 'objects\patch_explosion.tsv'), @('# sprite`ttile-base`tpalette`tanimation',
    "$patchExplosionSprite`t$($patchExplosion.TileBase)`t$($patchExplosion.Palette)`t$(Resolve-NpcAnimation 0x56 0)"))
$patchRewardSource = Read-ImportText (Join-Path $Disassembly 'data\ages\treasureObjectData.s')
$patchRewardRows = @('# name`tmode`troom-flag')
foreach ($name in @('TREASURE_OBJECT_TUNI_NUT_01','TREASURE_OBJECT_SWORD_01','TREASURE_OBJECT_SWORD_02','TREASURE_OBJECT_SWORD_04','TREASURE_OBJECT_SWORD_05')) {
    if ($patchRewardSource -notmatch ('m_TreasureSubid \$(?<flags>[0-9a-f]{2}),[^\r\n]+, ' + $name + '(?:\r?\n|$)')) { throw "Missing Patch reward mode: $name" }
    $modeByte = [Convert]::ToInt32($Matches['flags'],16)
    if (($modeByte -shr 4) -ne 0 -or ($modeByte -band 7) -notin @(1,2,3)) { throw "Unsupported Patch reward mode: $name" }
    $patchRewardRows += "$name`t$($modeByte -band 7)`t$(($modeByte -band 8) -shr 3)"
}
Write-GeneratedTable((Join-Path $destination 'objects\patch_rewards.tsv'), $patchRewardRows)
# Pin the native paths whose scalar operands are exported below.
if ($patchNative -notmatch 'ld \(hl\),60' -or
    $patchNative -notmatch '(?s)ld c,\$44.*?ld c,\$4a.*?ld c,\$75.*?ld c,\$78' -or
    $patchNative -notmatch '(?s)@extraBeetlePositions:\s+\.db \$4a \$57 \$75 \$78' -or
    $patchNative -notmatch '(?s)@gameStillGoing:\s+call objectApplySpeed.*?cp \$15.*?ld a,\$08' -or
    $patchNative -notmatch '(?s)ifdef ENABLE_US_BUGFIXES.*?call checkLinkCollisionsEnabled.*?ld a,DISABLE_LINK') {
    throw 'patch.s: US failure gate, beetle counters/positions, or minecart steering changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects\patch_constants.tsv'), @('# key`tvalue',
    "repaired-flag`t$($globalFlagValues['GLOBALFLAG_PATCH_REPAIRED_EVERYTHING'])",
    "speed`t$(Resolve-ObjectSpeed '100')", "beetle-delay`t60", "fixed-item-life`t60",
    "jump-sound`t$($soundIds['SND_ENEMY_JUMP'])", "whistle`t$($soundIds['SND_WHISTLE'])",
    "solve-sound`t$($soundIds['SND_SOLVEPUZZLE_2'])"))
Write-GeneratedTable((Join-Path $destination 'objects\patch_item_names.tsv'), @('# id`ttext-base64') + @(0x5812..0x5813 | ForEach-Object {
    "$($_.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$allTexts[$_])))"
}))
$patchResetSource = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\miscPuzzles.s')
$patchObjectSource = Read-ImportText (Join-Path $Disassembly 'objects\ages\mainData.s')
if ($patchResetSource -notmatch '(?s)miscPuzzles_subid10:\s+ld hl,wTmpcfc0.patchMinigame.fixingSword\s+ld b,\$08\s+call clearMemory' -or
    $patchObjectSource -notmatch '(?m)^group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData:\s+obj_Interaction \$90 \$10') {
    throw 'miscPuzzles.s:$90:$10 Patch temporary-state reset changed.'
}
Write-GeneratedTable((Join-Path $destination 'objects\patch_reset.tsv'), @('# group`troom', "$($Matches['group'])`t$($Matches['room'])"))
