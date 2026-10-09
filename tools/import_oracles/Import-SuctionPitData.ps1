# PART_SEA_EFFECTS' dungeon suction branch. Overworld pollution/whirlpools
# remain outside this feature; retain the original collision-mode dispatch.
function Read-SuctionTable([string]$file, [string]$label, [string]$pointerDirective) {
    $bytes = [Collections.Generic.List[int]]::new()
    $labels = @{}
    $pointers = [Collections.Generic.List[string]]::new()
    foreach ($node in Read-AssemblyLabelNodes (Join-Path $Disassembly $file) $label) {
        if ($node.Kind -eq 'Label' -and $node.Name.StartsWith('@')) { $labels[$node.Name] = $bytes.Count }
        if ($node.Name -ieq $pointerDirective) { $pointers.Add($node.Operands[0]); continue }
        if ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
            foreach ($operand in $node.Operands) {
                # Only the overworld rows use named tile constants.
                $value = switch ($operand) {
                    'TILEINDEX_POLLUTION' { 0xeb }
                    'TILEINDEX_WHIRLPOOL' { 0xe9 }
                    default { Convert-AssemblyInteger $operand }
                }
                $bytes.Add($value)
            }
        }
    }
    if ($pointers.Count -ne 6) { throw "${file}:${label}: expected six collision-mode pointers." }
    return @{ Bytes = $bytes; Labels = $labels; Pointers = $pointers }
}
$spawn = Read-SuctionTable 'data/ages/tile_properties/seaEffectTiles1.s' 'seaEffectTileTable' 'dbrel'
$effects = Read-SuctionTable 'data/ages/tile_properties/seaEffectTiles2.s' 'harmfulWaterTilesCollisionTable' '.dw'
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# active-collisions`torder`ttile`teffect`tsource")
foreach ($mode in @(2, 5)) {
    $spawnOffset = $spawn.Labels[$spawn.Pointers[$mode]]
    $effectOffset = $effects.Labels[$effects.Pointers[$mode]]
    $order = 0
    while ($effects.Bytes[$effectOffset] -ne 0) {
        $tile = $effects.Bytes[$effectOffset++]
        $effect = $effects.Bytes[$effectOffset++]
        if ($effect -ne 2 -or $spawn.Bytes[$spawnOffset++] -ne $tile) {
            throw "seaEffectTiles1/2.s: collision-mode $mode suction spawn/effect order differs."
        }
        $rows.Add("$mode`t$order`t$('{0:x2}' -f $tile)`t$effect`tseaEffectTiles2.s:@dungeon")
        $order++
    }
    if ($spawn.Bytes[$spawnOffset] -ne 0 -or $order -ne 4) { throw "seaEffectTiles1.s: mode $mode expected four terminated suction tiles." }
}
Write-GeneratedTable((Join-Path $destination 'metadata/suction_pit_tiles.tsv'), $rows)
$sourcePath = Join-Path $Disassembly 'object_code/ages/parts/seaEffects.s'
$offsets = @(Read-AssemblyLiteralValues $sourcePath '@positionOffsets')
$speeds = @(Read-AssemblyLiteralValues $sourcePath '@speedValues')
if ($offsets.Count -ne 20 -or $speeds.Count -ne 8) { throw 'seaEffects.s: suction offsets/speed table shape changed.' }
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# order`ty`tx`tsource")
for ($i = 0; $i -lt 5; $i++) {
    $rows.Add("$i`t$($offsets[$i * 2])`t$($offsets[$i * 2 + 1])`tseaEffects.s:@positionOffsets")
}
Write-GeneratedTable((Join-Path $destination 'metadata/suction_pit_probes.tsv'), $rows)
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# order`tspeed`tsource")
for ($i = 0; $i -lt $speeds.Count; $i++) { $rows.Add("$i`t$('{0:x2}' -f $speeds[$i])`tseaEffects.s:@speedValues") }
Write-GeneratedTable((Join-Path $destination 'metadata/suction_pit_speeds.tsv'), $rows)
# Guard the instruction contract whose counters/gates the runtime implements.
$source = Read-ImportText $sourcePath
if ($source -notmatch '(?s)@initialized:.*?sub LINK_STATE_NORMAL\s+ret nz\s+ld \(wDisableScreenTransitions\),a\s+call checkLinkCollisionsEnabled.*?ld \(hl\),\$06.*?@collision2:.*?inc a\s+cp \$03.*?ld a,\$02\s+ld \(wLinkForceState\),a\s+xor a\s+ld \(wLinkStateParameter\),a\s+jp clearAllParentItems' -or
    $source -notmatch '(?s)@func_63d6:\s+ld a,\$ff\s+ld \(wDisableScreenTransitions\),a.*?call partCommon_decCounter1IfNonzero.*?and \$1c\s+rrca\s+rrca.*?cp \$19.*?ld \(hl\),\$00.*?jp updateLinkPositionGivenVelocity' -or
    $source -notmatch '(?s)@setCounter1To20:\s+ld e,\$c6\s+ld a,\$20\s+ld \(de\),a') {
    throw 'seaEffects.s: dungeon suction update contract changed.'
}
