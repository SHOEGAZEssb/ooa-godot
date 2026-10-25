$nativePath = Join-Path $Disassembly 'object_code/ages/interactions/monkeyMain.s'
$visuals = [Collections.Generic.List[string]]::new()
$visuals.Add("# id`tsubid`tsprite`ttile-base`tpalette`tanimation`tsource")
$animations = [Collections.Generic.List[string]]::new()
$animations.Add("# id`tindex`tanimation`tsource")
foreach ($spec in @(@(0x39,5,8), @(0x63,0x3d,0x0a))) {
    $id,$subid,$last = $spec
    $graphic = Resolve-InteractionGraphics $id $subid
    if (!$graphic) { throw ('Missing native monkey graphics ${0:x2}:${1:x2}.' -f $id,$subid) }
    $sprite = $gfxNames[$graphic.Gfx]; Copy-EnemySprite $sprite
    $visuals.Add("$($id.ToString('x2'))`t$($subid.ToString('x2'))`t$sprite`t$($graphic.TileBase)`t$($graphic.Palette)`t$(Resolve-NpcAnimation $id $graphic.DefaultAnimation)`tdata/ages/interactionData.s")
    foreach ($index in 0..$last) {
        $animation = Resolve-NpcAnimation $id $index
        if (!$animation) { throw ('Missing monkey/accessory animation ${0:x2}:${1:x2}.' -f $id,$index) }
        $animations.Add("$($id.ToString('x2'))`t$($index.ToString('x2'))`t$animation`tdata/ages/interactionAnimations.s")
    }
}
Write-GeneratedTable((Join-Path $destination 'objects/monkey_visuals.tsv'),$visuals)
Write-GeneratedTable((Join-Path $destination 'objects/monkey_animations.tsv'),$animations)
$positions = @(Read-AssemblyDataDirectives $nativePath '@monkeyPositions' '.db' | ForEach-Object { $_.Operands })
$jumps = @(Read-AssemblyDataDirectives $nativePath '@jumpSpeeds' '.dw' | ForEach-Object { $_.Operands })
$offsets = @(Read-AssemblyDataDirectives (Join-Path $Disassembly 'object_code/ages/interactions/accessory.s') '@data' '.db' | ForEach-Object { $_.Operands })
if ($positions.Count -ne 40 -or $jumps.Count -ne 4 -or $offsets.Count -ne 36) { throw 'Monkey positions/jump speeds/bowtie offsets changed.' }
Write-GeneratedTable((Join-Path $destination 'objects/monkey_tables.tsv'), @(
    "# name`tvalues`tsource",
    "positions`t$(($positions | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x4') }) -join ',')`tobject_code/ages/interactions/monkeyMain.s:@monkeyPositions",
    "jumps`t$(($jumps | ForEach-Object { ((Convert-AssemblyInteger $_) -band 0xffff).ToString('x4') }) -join ',')`tobject_code/ages/interactions/monkeyMain.s:@jumpSpeeds",
    "bowtie`t$(($offsets | ForEach-Object { (Convert-AssemblyInteger $_).ToString('x4') }) -join ',')`tobject_code/ages/interactions/accessory.s:@data"
))
$ops = [Collections.Generic.HashSet[string]]::new([string[]]@('initcollisions','enableinput','checkabutton',
    'disableinput','jumpifobjectbyteeq','wait','scriptjump','asm15','showloadedtext','jumpifglobalflagset','rungenericnpclowindex'))
$rows = [Collections.Generic.List[string]]::new()
$rows.Add("# script`tlabel`tindex`tsource-line`topcode`toperands-base64`tjump-targets")
foreach ($script in @('monkeySubid5Script','monkeySubid5Script_bowtieMonkey')) {
    foreach ($command in Read-AssemblyCutsceneCommands (Join-Path $Disassembly 'scripts/ages/scripts.s') $script $ops) {
        $operands = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($command.Operands))
        $rows.Add("$script`t$($command.Label)`t$($command.Index)`t$($command.Line)`t$($command.Opcode)`t$operands`t")
    }
}
$texts = @{}
$textRows = [Collections.Generic.List[string]]::new()
$textRows.Add("# id`tmessage-base64`tsource")
foreach ($id in 0x5707..0x5710) {
    $texts[$id] = Resolve-OceanText $id ([Collections.Generic.HashSet[int]]::new())
    $textRows.Add("$($id.ToString('x4'))`t$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($texts[$id])))`tscripts/ages/scripts.s:monkeySubid5Script;scriptHelper.s:monkey_decideTextIndex")
}
Write-GeneratedTable((Join-Path $destination 'objects/monkey_texts.tsv'),$textRows)
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes/monkey_scripts.tsv'),
    (ConvertTo-NpcProgramRows @($rows) 'Monkey' $texts))
