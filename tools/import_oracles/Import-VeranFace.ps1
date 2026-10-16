# INTERAC_VERAN_CUTSCENE_FACE $2d: the main script aliases this helper body.
$path=Join-Path $Disassembly 'scripts/ages/scriptHelper.s'
$ops=[Collections.Generic.HashSet[string]]::new([string[]]@('disableinput','checkpalettefadedone','wait','writememory','playsound','writeobjectbyte','showtext','scriptend'))
$commands=@(Read-AssemblyCutsceneCommands $path 'veranFaceCutsceneScript' $ops 'oldMan_takeRupees')
$symbols=@{'w1Link.direction'='LinkDirection';'Interaction.visible'=0x5a}
$texts=@{0x5613=(Resolve-PalaceText 0x5613 ([Collections.Generic.HashSet[int]]::new()))}
$bindings=@{'checkpalettefadedone|'=@{Opcode='gate';Payload='PaletteDone'};
    'playsound|SND_LIGHTTORCH'=@{Opcode='playsound';Arg0=(Resolve-SoundConstant 'SND_LIGHTTORCH').ToString('x2')}}
Write-GeneratedTable((Join-Path $destination 'cutscenes/veran_face_commands.tsv'),
    (ConvertTo-CutsceneCommandRows -commands $commands -actor 'Actor' -symbols $symbols -animations @{} -texts $texts -positions $allTextPositions -bindings $bindings))
$source='object_code/ages/interactions/veranCutsceneFace.s'
$native=Read-ImportText (Join-Path $Disassembly $source)
$warp=[regex]::Match($native,'(?ms)^@warpDestVariables:\s*m_HardcodedWarpA ROOM_AGES_(?<room>[0-9a-f]{3}),\s*\$(?<transition>[0-9a-f]{2}),\s*\$(?<position>[0-9a-f]{2}),\s*\$(?<request>[0-9a-f]{2})')
if(!$warp.Success){throw 'veranCutsceneFace.s:@warpDestVariables: missing native warp.'}
$room=[Convert]::ToInt32($warp.Groups['room'].Value,16)
Write-GeneratedTable((Join-Path $destination 'cutscenes/veran_face_tables.tsv'),@("# profile`tvalues`tsource",
    "warp`t$((0x80 -bor ($room -shr 8)).ToString('x2')),$(($room -band 255).ToString('x2')),$($warp.Groups['transition'].Value),$($warp.Groups['position'].Value),$($warp.Groups['request'].Value)`t${source}:@warpDestVariables"))
Write-GeneratedBytes((Join-Path $destination 'cutscenes/veran_face_palettes.bin'),(Read-PaletteBytes 'paletteData4978' 8))
