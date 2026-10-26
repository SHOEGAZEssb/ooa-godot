# Title and file menus retain Seasons' palette headers and ordered OAM.
$seasonDestination = Split-Path $previewDestination -Parent
$paletteSource = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteData.s')
$paletteHeaders = Read-ImportText (Join-Path $Disassembly 'data/seasons/paletteHeaders.s')
function Copy-FrontendAsset([string]$name, [string]$extension) {
    $paths = @(foreach ($kind in @('gfx_compressible','gfx')) {
        foreach ($game in @('seasons','common')) {
            $relative = "$kind/$game/$name.$extension"
            if (Test-Path -LiteralPath (Join-Path $Disassembly $relative)) { $relative }
        }
    })
    if ($paths.Count -ne 1) { throw "Seasons frontend: ambiguous or missing $name.$extension." }
    $output = $name -replace '^flg_', 'flags_'
    Copy-GeneratedFile $paths[0] "menu/$output.$extension" $seasonDestination
}
foreach ($name in @('gfx_titlescreen_1','gfx_titlescreen_2','gfx_titlescreen_3','gfx_titlescreen_4','gfx_titlescreen_5','gfx_titlescreen_6',
        'spr_titlescreen_sprites','spr_fileselect_decorations','spr_nayru','spr_rod_of_seasons',
        'gfx_fileselect','gfx_messagespeed','gfx_pickafile','gfx_pickafile_2','gfx_copywhatwhere','gfx_quit_2','gfx_savescreen','gfx_gameover',
        'gfx_newfilescreen','gfx_name','gfx_copy','gfx_erase','gfx_secret_thatswrong')) {
    Copy-FrontendAsset $name 'png'
}
# The shared file presenter names the four Nayru tiles by their VRAM offset.
Copy-GeneratedFile 'gfx_compressible/seasons/spr_nayru.png' 'menu/spr_nayru_1.png' $seasonDestination
foreach ($name in @('titlescreen','file_menu_top','file_menu_middle','file_menu_bottom','file_menu_copy',
        'save_menu_middle','save_menu_bottom','file_menu_message_speed','name_entry_top','name_entry_middle','name_entry_bottom',
        'secret_entry_top','secret_entry_middle','secret_entry_bottom','secret_entry_error')) {
    Copy-FrontendAsset "map_$name" 'bin'; Copy-FrontendAsset "flg_$name" 'bin'
}
function Export-FrontendPalette([string[]]$headers, [string]$output) {
    $background = [byte[]]::new(96); $sprites = [byte[]]::new(96)
    foreach ($header in $headers) {
        $body = [regex]::Match($paletteHeaders, '(?ms)^m_PaletteHeaderStart\s+\$[0-9a-f]+,\s*' + [regex]::Escape($header) + '\s*\r?\n(?<body>.*?)(?=^m_PaletteHeaderStart|\z)')
        if (-not $body.Success) { throw "Seasons frontend: missing $header." }
        foreach ($row in [regex]::Matches($body.Groups['body'].Value, 'm_PaletteHeader(?<kind>Bg|Spr)\s+(?<first>\d+),\s*(?<count>\d+),\s*(?<label>\w+)')) {
            $first = [int]$row.Groups['first'].Value; $count = [int]$row.Groups['count'].Value
            if ($first + $count -gt 8) { throw "Seasons frontend: $header exceeds eight palette slots." }
            $bytes = Read-PaletteBytes $row.Groups['label'].Value ($count * 4) $paletteSource
            $target = $background
            if ($row.Groups['kind'].Value -eq 'Spr') { $target = $sprites }
            [Array]::Copy($bytes, 0, $target, $first * 12, $bytes.Length)
        }
    }
    Write-GeneratedBytes((Join-Path $seasonDestination "menu/palette_$output`_bg.bin"), $background)
    Write-GeneratedBytes((Join-Path $seasonDestination "menu/palette_$output`_sprites.bin"), $sprites)
}
Export-FrontendPalette @('PALH_03') 'title'
Export-FrontendPalette @('PALH_05') 'file'
Export-FrontendPalette @('PALH_05','PALH_06') 'file_erase'

$hostConnection = Start-AssemblySourceHost 'ROM_SEASONS;REGION_US;SEASONS_ENGINE;BUILD_VANILLA;ENABLE_US_BUGFIXES'
try {
    function Read-FrontendOam([string]$path, [string]$global, [string]$local = '') {
        $full = Join-Path $Disassembly $path
        $json = Invoke-AssemblySourceHost $hostConnection 'LABEL_NODES' "$full`0$global`0"
        $nodes = @(($json | ConvertFrom-Json) | Where-Object IsActive)
        if ($local) {
            $start = -1
            for ($index=0; $index -lt $nodes.Count; $index++) {
                if ($nodes[$index].Kind -eq 'Label' -and $nodes[$index].Name -ceq $local) { $start=$index; break }
            }
            if ($start -lt 0) { throw "${path}: missing $global$local." }
            $end = $start + 1
            while ($end -lt $nodes.Count -and $nodes[$end].Kind -ne 'Label') { $end++ }
            $nodes = @($nodes[($start + 1)..($end - 1)])
        }
        $values = @(foreach ($node in $nodes) {
            if ($node.Kind -eq 'Data' -and $node.Name -ieq '.db') {
                foreach ($operand in $node.Operands) { Convert-AssemblyInteger $operand }
            }
        })
        if ($values.Count -lt 1 -or $values.Count -ne 1 + 4 * $values[0]) { throw "${path}: $global$local has malformed OAM." }
        return ,$values
    }
    function Add-FrontendOam($rows, [string]$layout, [string]$path, [string]$global, [string]$local = '') {
        $values = Read-FrontendOam $path $global $local
        for ($part = 0; $part -lt $values[0]; $part++) {
            $fields = @($values[(1 + $part*4)..(4 + $part*4)] | ForEach-Object { $_.ToString('x2') }) -join "`t"
            $rows.Add("$layout`t$part`t$fields`t$global$local`t`t${path}:$global$local")
        }
    }
    $fileRows = [Collections.Generic.List[string]]::new()
    $header = "# layout`tpart`ty`tx`ttile`tattributes`tsource-label`talias-of`tsource"
    $fileRows.Add($header)
    foreach ($entry in @(
        @('decorations','fileSelect_redrawDecorationsAndSetWramBank4','@sprites'),
        @('acorn-cursor','fileSelectDrawAcornCursor','@sprite'), @('text-speed-cursor','fileSelectMode1','@data'),
        @('name-character-cursor','drawNameInputCursors','@cursorOnCharacterSprites'),
        @('name-lower-option-cursor','drawNameInputCursors','@lowerOptionCursorSprites'),
        @('name-entry-cursor','drawNameInputCursors','@textInputCursorSprite'),
        @('secret-entry-cursor','drawSecretInputCursors','@textInputCursorSprite'),
        @('save-quit-acorn','saveQuitMenu_drawSprites','@acornSprite'), @('hero-file','fileSelectDrawLink','@spritesb'))) {
        Add-FrontendOam $fileRows $entry[0] 'code/bank2.s' $entry[1] $entry[2]
    }
    if ($fileRows.Count -ne 28) { throw 'Seasons frontend: expected 27 file-menu OAM parts.' }
    Write-GeneratedTable((Join-Path $seasonDestination 'menu/file_oam.tsv'), $fileRows)
    $titleRows = [Collections.Generic.List[string]]::new(); $titleRows.Add($header)
    Add-FrontendOam $titleRows 'logo' 'code/bank3Cutscenes.s' 'titlescreenMakuSeedSprite'
    Add-FrontendOam $titleRows 'press-start' 'code/bank3Cutscenes.s' 'titlescreenPressStartSprites'
    if ($titleRows.Count -ne 29) { throw 'Seasons frontend: expected 28 title OAM parts.' }
    Write-GeneratedTable((Join-Path $seasonDestination 'menu/title_oam.tsv'), $titleRows)
} finally {
    [void](Invoke-AssemblySourceHost $hostConnection 'QUIT' '')
    $hostConnection.WaitForExit(); $hostConnection.Dispose()
}
