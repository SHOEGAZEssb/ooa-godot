# Common Gel/Zol profiles; enemySetAnimation returns the selected ROM bank in A.
function Add-GelZolProfile([string]$owner,[string]$table,[int[]]$values,[string]$source) {
    for ($index=0;$index -lt $values.Count;$index++) {
        $rows.Add("$owner`t$table`t$index`t$($values[$index])`t0`t$source+$($index.ToString('x2'))")
    }
}
foreach ($pack in @(
    @{Name='ages';Bytes=$romBytes;Symbols=$agesSymbols;Root=$destination},
    @{Name='seasons';Bytes=$seasonsRomBytes;Symbols=$seasonsSymbols;Root=(Split-Path $previewDestination -Parent)}
)) {
    $rows=[Collections.Generic.List[string]]::new()
    $rows.Add("# owner`ttable`tindex`tvalue-a`tvalue-b`tsource")
    $gelCodeSource = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\gel.s')
    if ($gelCodeSource -notmatch
            '(?ms)^gel_state_uninitialized:.*?ld a,\$10' -or
        $gelCodeSource -notmatch
            '(?ms)^gel_state8:.*?ld \(hl\),\$30.*?' +
            'ld \(hl\),\$08.*?ld \(hl\),SPEED_40' -or
        $gelCodeSource -notmatch
            '(?ms)^gel_stateB:.*?ld c,\$28' -or
        $gelCodeSource -notmatch
            '(?ms)^gel_stateC:.*?ld \(hl\),120' -or
        $gelCodeSource -notmatch
            '(?ms)^gel_beginHop:.*?ld bc,-\$200.*?' +
            'ld \(hl\),SPEED_100') {
        throw 'Gel state-entry behavior profile changed.'
    }
    Add-GelZolProfile 'gel' 'state-profile' `
        @(0x10, 0x30, 0x08, 0x0a, 0x28, -0x200, 0x28, 120) `
        'object_code/common/enemies/gel.s:state-entry-operands'

    $zolCodeSource = Read-ImportText (
        Join-Path $Disassembly 'object_code\common\enemies\zol.s')
    if ($zolCodeSource -notmatch
            '(?ms)^zol_state_uninitialized:.*?ld a,SPEED_c0.*?' +
            'ld \(hl\),\$18' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid00_state8:.*?ld c,\$28.*?' +
            'ld bc,-\$200.*?ld \(hl\),\$04' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid00_state9:.*?ld c,\$28.*?ld \(hl\),\$30' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid00_stateB:.*?ld c,\$28.*?ld \(hl\),\$30' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid00_stateC:.*?ld \(hl\),40' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid01_state8:.*?ld \(hl\),\$10.*?' +
            'ld \(hl\),SPEED_80.*?ld \(hl\),\$20' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid01_stateA:.*?ld \(hl\),<\(-\$200\).*?' +
            'ld \(hl\),SPEED_100' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid01_stateB:.*?ld \(hl\),\$18' -or
        $zolCodeSource -notmatch
            '(?ms)^zol_subid01_stateC:.*?ld \(hl\),18') {
        throw 'Zol state-entry behavior profile changed.'
    }
    Add-GelZolProfile 'zol' 'state-profile' `
        @(0x28, -0x200, 0x28, 0x18, 4, 0x30, 0x1e, 40,
          0x10, 0x14, 0x20, 0x28, 0x18, 18, ([int]$pack.Symbols['zol_state_uninitialized'] -shr 14)) `
        'object_code/common/enemies/zol.s:state-entry-operands'
    $entry=Read-MenuRomBytes $pack 'zol_state_uninitialized' 4
    if (($entry -join ',') -ne '120,183,62,30') { throw "$($pack.Name): native Zol initialization/speed sequence changed." }
    Write-GeneratedTable((Join-Path $pack.Root 'objects/gel_zol.tsv'),$rows)
}
