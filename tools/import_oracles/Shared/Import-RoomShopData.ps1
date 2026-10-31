# Native graphics initialization selects shops independently of their actor streams.
foreach ($pack in @(
    @{ Name = 'ages'; Root = $destination; Bytes = $romBytes; Symbols = $agesSymbols; Rows = 35; Shops = 4; Maximum = 10 },
    @{ Name = 'seasons'; Root = (Split-Path $previewDestination -Parent); Bytes = $seasonsRomBytes; Symbols = $seasonsSymbols; Rows = 46; Shops = 12; Maximum = 15 }
)) {
    $roomGfxPath = Join-Path $Disassembly "code/$($pack.Name)/roomGfxChanges.s"
    $roomGfxSource = Read-ImportText $roomGfxPath
    if ($roomGfxSource -notmatch '(?ms)^roomTileChangesAfterLoad04:\s*ld hl,wInShop\s*set 1,\(hl\)\s*ld a,TREE_GFXH_03\s*jp loadTreeGfx') {
        throw 'roomGfxChanges.s:roomTileChangesAfterLoad04 must set wInShop bit$01 before loading price graphics.'
    }
    $afterGfxBytes = [Collections.Generic.List[byte]]::new()
    $afterGfxLabels = @{}
    $afterGfxPointers = [Collections.Generic.List[string]]::new()
    $captureAfterGfxPointers = $false
    foreach ($node in Read-AssemblyLabelNodes $roomGfxPath 'applyRoomSpecificTileChangesAfterGfxLoad') {
        if ($node.Kind -eq 'Label') {
            if ($node.Name -eq '@tileChangesGroupTable') { $captureAfterGfxPointers = $true }
            elseif ($node.Name -match '^@group[0-7]$') {
                $captureAfterGfxPointers = $false
                $afterGfxLabels[$node.Name] = $afterGfxBytes.Count
            }
        }
        if ($captureAfterGfxPointers -and $node.Kind -eq 'Data' -and $node.Name -ieq '.dw') {
            foreach ($operand in $node.Operands) { $afterGfxPointers.Add($operand) }
        }
        if ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
            foreach ($operand in $node.Operands) { $afterGfxBytes.Add([byte](Convert-AssemblyInteger $operand)) }
        }
    }
    if ($afterGfxPointers.Count -ne 8) {
        throw "roomGfxChanges.s:@tileChangesGroupTable requires8 pointers, got$($afterGfxPointers.Count)."
    }
    $shopFlags = [byte[]]::new(8 * 256)
    $afterGfxRecords = 0
    $shopRooms = 0
    for ($group = 0; $group -lt 8; $group++) {
        $pointer = $afterGfxPointers[$group]
        if (-not $afterGfxLabels.ContainsKey($pointer)) {
            throw "roomGfxChanges.s:@tileChangesGroupTable group$group points to missing$pointer."
        }
        $cursor = [int]$afterGfxLabels[$pointer]
        $seenRooms = @{}
        while ($true) {
            if ($cursor -ge $afterGfxBytes.Count) { throw "roomGfxChanges.s:$pointer lost its zero terminator." }
            $room = [int]$afterGfxBytes[$cursor++]
            if ($room -eq 0) { break }
            if ($cursor -ge $afterGfxBytes.Count) { throw "roomGfxChanges.s:${pointer}: truncated room `$$($room.ToString('x2')) handler." }
            $handler = [int]$afterGfxBytes[$cursor++]
            if ($handler -gt $pack.Maximum -or $seenRooms.ContainsKey($room)) {
                throw "roomGfxChanges.s:${pointer}: invalid/duplicate room `$$($room.ToString('x2')), handler `$$($handler.ToString('x2'))."
            }
            $seenRooms[$room] = $true
            $afterGfxRecords++
            if ($handler -eq 4) { $shopFlags[$group * 256 + $room] = 2; $shopRooms++ }
        }
    }
    if ($afterGfxRecords -ne $pack.Rows -or $shopRooms -ne $pack.Shops) {
        throw "roomGfxChanges.s: expected$($pack.Rows) after-load room rows/$($pack.Shops) shop initializations, got$afterGfxRecords/$shopRooms."
    }
    Write-GeneratedBytes((Join-Path $pack.Root 'metadata/roomShopFlags.bin'), $shopFlags)

    $pointers = Read-MenuRomBytes $pack 'applyRoomSpecificTileChangesAfterGfxLoad@tileChangesGroupTable' 16
    $bank = [int]$pack.Symbols['applyRoomSpecificTileChangesAfterGfxLoad'] -band -0x4000
    for ($group = 0; $group -lt 8; $group++) {
        $cursor = $bank + ([BitConverter]::ToUInt16($pointers,$group*2) -band 0x3fff)
        $nativeFlags = [byte[]]::new(256)
        for ($row = 0; $pack.Bytes[$cursor] -ne 0; $row++) {
            if ($row -ge 256) { throw "$($pack.Name): unterminated after-gfx group$group." }
            $room = $pack.Bytes[$cursor++]
            $handler = $pack.Bytes[$cursor++]
            if ($handler -eq 4) { $nativeFlags[$room] = 2 }
        }
        for ($room = 0; $room -lt 256; $room++) {
            if ($nativeFlags[$room] -ne $shopFlags[$group*256+$room]) {
                throw "$($pack.Name): compiled shop room$${group}:$($room.ToString('x2')) differs from source."
            }
        }
    }
}
