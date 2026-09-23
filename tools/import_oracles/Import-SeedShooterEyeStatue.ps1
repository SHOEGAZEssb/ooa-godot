$mainPath = Join-Path $Disassembly 'objects/ages/mainData.s'
$main = Read-ImportText $mainPath
$definition = Read-ImportText (Join-Path $Disassembly 'data/ages/partData.s')
$code = Read-ImportText (Join-Path $Disassembly 'object_code/ages/parts/seedShooterEyeStatue.s')
$timer = [regex]::Match($code, '(?ms)partCode46:\s+jr z,@normalStatus\s+ld h,d\s+ld l,\$c6\s+ld \(hl\),\$(?<timer>[0-9a-f]{2})')
if (-not $timer.Success -or $definition -notmatch '(?m)^\s*\.db \$8c \$f9 \$88 \$00 \$40 \$1e \$00 \$00 ; \$46\s*$') {
    throw 'PART_SEED_SHOOTER_EYE_STATUE $46: unsupported definition or activation counter.'
}
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# group`troom`torder`tsubid`tpacked-position`tactive-counter`tgfx`tenemy-collision-mode`tradius-y`tradius-x`tsource")
foreach ($section in [regex]::Matches($main, '(?ms)^(?<label>group(?<group>[0-7])Map(?<room>[0-9a-f]{2})ObjectData):(?<body>.*?)(?=^\w+:|\z)')) {
    if ($section.Groups['body'].Value -notmatch 'obj_Part \$46 ') { continue }
    $order = 0
    foreach ($node in @(Read-AssemblyMacroInvocations $mainPath $section.Groups['label'].Value)) {
        if ($node.Name -eq 'obj_Part' -and $node.Operands[0] -eq '$46') {
            if ($node.Operands.Count -ne 3 -or $node.Operands[1] -notmatch '^\$0[0-7]$' -or $node.Operands[2] -notmatch '^\$[0-9a-f]{2}$') {
                throw ('PART $46: unsupported placement in {0}.' -f $section.Groups['label'].Value)
            }
            $rows.Add("$($section.Groups['group'].Value)`t$($section.Groups['room'].Value)`t$order`t$($node.Operands[1].Substring(1))`t$($node.Operands[2].Substring(1))`t$($timer.Groups['timer'].Value)`t8c`tf9`t08`t08`tobjects/ages/mainData.s:$($section.Groups['label'].Value);object_code/ages/parts/seedShooterEyeStatue.s:partCode46;data/ages/partData.s:partData+0230")
        }
        if ($node.Name -notin @('obj_End','obj_EndPointer','obj_Pointer','obj_IfRoomFlag','obj_IfRoomFlagUnset','obj_EndIf')) { $order++ }
    }
}
if ($rows.Count -ne 8 -or [regex]::Matches($main, 'obj_Part \$46 ').Count -ne 7) {
    throw 'Expected all seven native seed-shooter eye-statue placements.'
}
Write-GeneratedTable((Join-Path $destination 'objects/seed_shooter_eye_statues.tsv'), $rows)

# collisionType is PART ID $46; enemyCollisionMode is definition $f9 & $7f.
$active = Read-ImportText (Join-Path $Disassembly 'data/ages/partActiveCollisions.s')
$mask = [regex]::Match($active, '(?m)^\s*dbrev (?<bits>%[01]{8} %[01]{8} %[01]{8} %[01]{8}) ; 0x46\s*$')
$effectsSource = Read-ImportText (Join-Path $Disassembly 'data/ages/objectCollisionTable.s')
$effectRows = [regex]::Matches($effectsSource, '(?m)^\s*\.db(?<values>(?:\s+\$[0-9a-f]{2}){16})\s*$')
$effectCode = Read-ImportText (Join-Path $Disassembly 'code/collisionEffects.s')
if (-not $mask.Success -or $effectRows.Count -ne 256 -or
    $effectCode -notmatch '(?ms)collisionEffect31:\s+ld a,ENEMYDMG_34\s+jp applyDamageToEnemyOrPart' -or
    $effectCode -notmatch '\.db \$60 \$e4 \$00 \$00 ; ENEMYDMG_34') {
    throw 'PART $46 requires collision effect31 and ENEMYDMG34 pending-hit / signed invincibility writes.'
}
$bits = $mask.Groups['bits'].Value.Replace('%','').Replace(' ','')
$effects = [regex]::Matches(($effectRows[242].Value + $effectRows[243].Value),'\$(?<value>[0-9a-f]{2})')
$hitRows = [Collections.Generic.List[string]]::new()
$hitRows.Add("# item`tenabled`teffect`tinvincibility`tsource")
for ($item = 0; $item -lt 32; $item++) {
    $effect = $effects[$item].Groups['value'].Value
    if ($bits[$item] -eq '1' -and $effect -ne '31') { throw 'Unsupported PART $46 enabled collision effect.' }
    $hitRows.Add("$($item.ToString('x2'))`t$($bits[$item])`t$effect`te4`tdata/ages/partActiveCollisions.s:partActiveCollisions+0118;data/ages/objectCollisionTable.s:objectCollisionTable+$((0xf20 + $item).ToString('x4'));code/collisionEffects.s:collisionEffect31")
}
Write-GeneratedTable((Join-Path $destination 'objects/seed_shooter_eye_statue_collisions.tsv'), $hitRows)
