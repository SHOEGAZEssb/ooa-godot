$scriptPath = Join-Path $Disassembly 'scripts/common/commonScripts.s'
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@('initcollisions','enableinput','checkabutton',
    'setdisabledobjectsto91','showtext','jumpiftextoptioneq','wait','scriptjump','asm15'))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets")
foreach ($command in Read-AssemblyCutsceneCommands $scriptPath 'faroresMemoryScript' $ops) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($command.Operands))
    $rows.Add("$($command.Script)`t$($command.Label)`t$($command.Index)`t$($command.Line)`t$($command.Opcode)`t$encoded`t")
}
$texts = @{ 0x551b = (Resolve-OceanText 0x551b ([Collections.Generic.HashSet[int]]::new())) }
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/farore_memory.tsv'), (ConvertTo-NpcProgramRows @($rows) 'Book' $texts))
$graphic = Resolve-InteractionGraphics 0x1c 0
$sprite = $gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
Write-GeneratedTable((Join-Path $destination 'objects/farore_memory.tsv'), @(
    "# sprite`ttile-base`tpalette`tanimation`tsource",
    "$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$(Resolve-NpcAnimation 0x1c $graphic.DefaultAnimation)`tdata/ages/interactionData.s"
))
foreach ($asset in @('gfx_secret_list_menu.png','map_secret_list_menu.bin','flg_secret_list_menu.bin')) {
    Copy-GeneratedFile "gfx_compressible/common/$asset" "menu/$asset"
}
Write-GeneratedBytes((Join-Path $destination 'menu/secret_list_bg.bin'), (Read-PaletteBytes 'paletteData58c8' 16))
Write-GeneratedBytes((Join-Path $destination 'menu/secret_list_obj.bin'), (Read-PaletteBytes 'paletteData4138' 32))
$bank = Join-Path $Disassembly 'code/bank2.s'
$rows.Clear(); $rows.Add("# linked`tindex`tname-mode`tflag`tsecret-index`tsource")
$nodes = @(Read-AssemblyLabelNodes $bank 'secretListMenu_getSecretData')
foreach ($linked in 0..1) {
    $label = if ($linked) { '@linked' } else { '@unlinked' }
    $reading = $false; $index = 0
    foreach ($node in $nodes) {
        if ($node.Kind -eq 'Label') {
            if ($reading) { break }
            if ($node.Name -eq $label) { $reading = $true }
            continue
        }
        if (!$reading -or $node.Kind -ne 'Data') { continue }
        if ($node.Name -ne '.db') { throw "$bank`:$label unsupported directive $($node.Name)." }
        if ($node.Operands.Count -eq 1 -and (Convert-AssemblyInteger $node.Operands[0]) -eq 0) { break }
        if ($node.Operands.Count -ne 3 -or !$globalFlagValues.ContainsKey($node.Operands[1])) { throw "$bank`:$label invalid secret entry." }
        $name = Convert-AssemblyInteger $node.Operands[0]; $flag = [int]$globalFlagValues[$node.Operands[1]]
        $secret = Convert-AssemblyInteger $node.Operands[2]
        $rows.Add("$linked`t$index`t$($name.ToString('x2'))`t$($flag.ToString('x2'))`t$($secret.ToString('x2'))`tcode/bank2.s:secretListMenu_getSecretData$label")
        $index++
    }
    if ($index -ne (10 + $linked)) { throw "$bank`:$label expected $(10+$linked) secrets, got $index." }
}
Write-GeneratedTable((Join-Path $destination 'menu/secret_list_entries.tsv'),$rows)
$rows.Clear(); $rows.Add("# index`ttext-base64`tsource")
$nameNodes = @(Read-AssemblyLabelNodes $bank 'secretTextTable')
foreach ($index in 1..0x19) {
    $label = '@text' + $index.ToString('x')
    $reading = $false
    $data = @($nameNodes | Where-Object {
        if ($_.Kind -eq 'Label') { $reading = $_.Name -eq $label }
        $reading -and $_.Kind -eq 'Directive' -and $_.Name -eq '.asc'
    })
    if ($data.Count -ne 1 -or $data[0].Code.Trim() -notmatch '^\.asc\s+"([^"]*)"\s+0$') { throw "$bank`:$label expected one terminated clean-US string; count=$($data.Count), code=$($data.Code -join ';')." }
    $text = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Matches[1]))
    $rows.Add("$($index.ToString('x2'))`t$text`tcode/bank2.s:secretTextTable$label")
}
Write-GeneratedTable((Join-Path $destination 'menu/secret_list_names.tsv'),$rows)
