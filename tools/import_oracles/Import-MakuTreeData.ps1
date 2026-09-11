# The first present Maku Tree visit is interaction $87 subid $01, selected
# from room 0:38's $87:$00 object while wMakuTreeState and GLOBALFLAG_0c are
# both clear. Export its complete simulated-input/script timing, all five tree
# animations, text, hardcoded destination, initial PALH_8f load, and four
# cycling background-palette states instead of encoding disassembly-only
# details in runtime code.
$makuTreeSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\ages\interactions\makuTree.s')
$makuScriptSource = Read-ImportText (
    Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
$makuCutsceneSource = Read-ImportText (
    Join-Path $Disassembly 'code\ages\cutscenes\miscCutscenes.s')
$makuInputMatch = [regex]::Match(
    $makuTreeSource,
    '(?ms)@simulatedInput:\s*dwb\s+(?<idle>\d+)\s+\$00\s+dwb\s+(?<right>\d+)\s+BTN_RIGHT\s+dwb\s+(?<stop>\d+)\s+\$00\s+dwb\s+(?<up>\d+)\s+BTN_UP\s+dwb\s+(?<tail>\d+)\s+\$00')
if (-not $makuInputMatch.Success) {
    throw 'Could not parse the Maku Tree disappearance simulated-input record.'
}
$makuInitialPaletteMatch = [regex]::Match(
    $makuTreeSource,
    '(?ms)Subid 1 only:.*?ld a,(?<palette>PALH_[A-Za-z0-9_]+)\s+call loadPaletteHeader\s+ld hl,@simulatedInput')
if (-not $makuInitialPaletteMatch.Success -or
    $makuInitialPaletteMatch.Groups['palette'].Value -ne 'PALH_8f') {
    throw 'Could not resolve the Maku Tree disappearance initial PALH_8f load.'
}
$makuPaletteSymbols = @('PALH_9a', 'PALH_c4', 'PALH_8f', 'PALH_c5')
$makuPaletteTableMatch = [regex]::Match(
    $makuCutsceneSource,
    '(?ms)@paletteHeaders:\s*\.db\s+\$9a\s+\$c4\s+\$8f\s+\$c5')
if (-not $makuPaletteTableMatch.Success) {
    throw 'Could not resolve the Maku Tree $9a/$c4/$8f/$c5 palette cycle.'
}
$makuInitialPaletteIndex = [Array]::IndexOf(
    $makuPaletteSymbols, $makuInitialPaletteMatch.Groups['palette'].Value)
if ($makuInitialPaletteIndex -lt 0) {
    throw 'The initial Maku Tree palette is absent from its cycling palette table.'
}
$makuScriptMatch = [regex]::Match(
    $makuScriptSource,
    '(?ms)makuTree_subid01Script_body:(?<body>.*?)(?=^makuTree_subid02Script_body:)')
if (-not $makuScriptMatch.Success) {
    throw 'Could not parse makuTree_subid01Script_body.'
}
$makuWaits = @([regex]::Matches($makuScriptMatch.Groups['body'].Value, '(?m)^\s*wait\s+(?<frames>\d+)') |
    ForEach-Object { [int]$_.Groups['frames'].Value })
if ($makuWaits.Count -ne 6 -or ($makuWaits -join ',') -ne '210,60,60,210,210,150') {
    throw "Unexpected Maku Tree disappearance waits: $($makuWaits -join ',')."
}
$makuWarpMatch = [regex]::Match(
    $makuCutsceneSource,
    'm_HardcodedWarpA\s+ROOM_AGES_(?<room>[0-9a-f]{3}),\s*\$(?<source>[0-9a-f]{2}),\s*\$(?<position>[0-9a-f]{2}),\s*\$(?<transition2>[0-9a-f]{2})')
if (-not $makuWarpMatch.Success -or $makuWarpMatch.Groups['room'].Value -ne '038') {
    throw 'Could not parse the Maku Tree disappearance hardcoded warp.'
}
$makuAnimations = @(0..4 | ForEach-Object { Resolve-NpcAnimation 0x87 $_ })
if (($makuAnimations | Where-Object { -not $_ }).Count -ne 0) {
    throw 'Could not resolve all five INTERAC_MAKU_TREE animations.'
}
# interactionLoadExtraGraphics follows object graphics header $04 until the
# stop bit on $05, appending the second 16-tile sheet after the first.
$makuGfxIndex = $interactionGraphics['135:0'].Gfx
$makuExtraSprite = $gfxNames[$makuGfxIndex + 1]
$objectGfxSource = Read-ImportText (
    Join-Path $Disassembly 'data\ages\objectGfxHeaders.s')
if ($makuGfxIndex -ne 0x04 -or $makuExtraSprite -ne 'spr_makuadultsprites_2' -or
    $objectGfxSource -notmatch '/\* \$05 \*/ m_ObjectGfxHeader spr_makuadultsprites_2, 1') {
    throw 'Could not resolve the Maku Tree extra object-graphics header chain $04-$05.'
}
$makuExtraSource = Get-ChildItem $Disassembly -Directory -Filter 'gfx*' |
    ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -Filter "$makuExtraSprite.png" } |
    Select-Object -First 1
if ($null -eq $makuExtraSource) { throw "Maku Tree extra sprite not found: $makuExtraSprite.png" }
Copy-Item -LiteralPath $makuExtraSource.FullName -Destination (
    Join-Path $destination "gfx\$makuExtraSprite.png") -Force
foreach ($textId in @(0x0564, 0x0540, 0x0541)) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve Maku Tree cutscene text TX_$($textId.ToString('x4'))."
    }
    if (-not $allTextPositions.ContainsKey($textId) -or $allTextPositions[$textId] -ne 2) {
        throw "Expected Maku Tree cutscene text TX_$($textId.ToString('x4')) to use \\pos(2)."
    }
}
$makuColumns = [Collections.Generic.List[string]]::new()
$makuColumns.AddRange([string[]]@(
    '0', '38', '87', '00',
    $makuInitialPaletteIndex.ToString(),
    $makuInputMatch.Groups['idle'].Value,
    $makuInputMatch.Groups['right'].Value,
    $makuInputMatch.Groups['stop'].Value,
    $makuInputMatch.Groups['up'].Value,
    $makuInputMatch.Groups['tail'].Value
))
foreach ($wait in $makuWaits) { $makuColumns.Add($wait.ToString()) }
$transition2 = [Convert]::ToInt32($makuWarpMatch.Groups['transition2'].Value, 16)
$makuColumns.AddRange([string[]]@(
    [Convert]::ToInt32($makuWarpMatch.Groups['source'].Value, 16).ToString(),
    '0',
    $makuWarpMatch.Groups['room'].Value.Substring(1),
    $makuWarpMatch.Groups['position'].Value,
    (($transition2 -shr 4) -band 0x07).ToString(),
    ($transition2 -band 0x03).ToString()
))
$makuColumns.AddRange([string[]]$makuAnimations)
$makuColumns.Add($makuExtraSprite)
$makuColumns.Add('2')
foreach ($textId in @(0x0564, 0x0540, 0x0541)) {
    $makuColumns.Add([Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes($allTexts[$textId])))
}
$makuEventRows = @(
    "# group`troom`tid`tsubid`tinitial-palette`tinput-idle`tinput-right`tinput-stop`tinput-up`tinput-tail`tintro-delay`tpost-intro`tfrown-delay`tdisappearance`tpost-ahh`tfinish-delay`tsource-transition`tdestination-group`tdestination-room`tdestination-position`tdestination-parameter`tdestination-transition`tanimation0`tanimation1`tanimation2`tanimation3`tanimation4`textra-sprite`ttextbox-position`tintro-base64`tahh-base64`thelp-base64",
    ($makuColumns -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_cutscene.tsv'),
    $makuEventRows)

$makuMusicSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\music.s')
$makuStopSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\.define\s+SNDCTRL_STOPMUSIC\s+\$(?<value>[0-9a-f]{2})')
$makuDisappearSound = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_MAKUDISAPPEAR\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuCutsceneConstants = Read-ImportText (
    Join-Path $Disassembly 'constants\common\cutsceneIndices.s')
$makuCutsceneIndex = [regex]::Match(
    $makuCutsceneConstants,
    '(?m)^\s*CUTSCENE_MAKU_TREE_DISAPPEARING\s+db\s*;\s*0x(?<value>[0-9a-f]{2})')
if (-not $makuStopSound.Success -or $makuStopSound.Groups['value'].Value -ne 'f0' -or
    -not $makuDisappearSound.Success -or $makuDisappearSound.Groups['value'].Value -ne 'b2' -or
    -not $makuCutsceneIndex.Success -or $makuCutsceneIndex.Groups['value'].Value -ne '07') {
    throw 'Could not resolve Maku Tree STOPMUSIC $f0, disappearance sound $b2, or cutscene $07.'
}

$makuBodyStart = $makuScriptMatch.Groups['body'].Index
$makuBodyEnd = $makuBodyStart + $makuScriptMatch.Groups['body'].Length
$findMakuSourceLine = {
    param([string]$pattern, [int]$occurrence = 0)
    return Find-CutsceneCommandSourceLine `
        $makuScriptSource $makuBodyStart $makuBodyEnd $pattern `
        'makuTree_subid01Script_body' $occurrence
}
$newMakuCommandRow = {
    param(
        [int]$index,
        [int]$line,
        [string]$opcode,
        [string]$actor,
        [string]$arg0,
        [string]$arg1,
        [string]$payload)
    return New-CutsceneCommandRow `
        'makuTree_subid01Script_body' $index 'makuTree_subid01Script_body' `
        $line $opcode $actor $arg0 $arg1 $payload
}
$makuCommandRows = @(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64",
    (& $newMakuCommandRow 0 (& $findMakuSourceLine '(?m)^\s*disablemenu\s*$') 'disablemenu' '' '' '' ''),
    (& $newMakuCommandRow 1 (& $findMakuSourceLine '(?m)^\s*asm15\s+makuTree_setAnimation,\s*\$00\s*$') 'setanimationcontinue' 'MakuTree' '00' '' $makuAnimations[0]),
    (& $newMakuCommandRow 2 (& $findMakuSourceLine '(?m)^\s*setcollisionradii\s+\$08,\s*\$08\s*$') 'setcollisionradii' 'MakuTree' '08' '08' ''),
    (& $newMakuCommandRow 3 (& $findMakuSourceLine '(?m)^\s*makeabuttonsensitive\s*$') 'makeabuttonsensitive' 'MakuTree' '' '' ''),
    (& $newMakuCommandRow 4 (& $findMakuSourceLine '(?m)^\s*checkpalettefadedone\s*$') 'gate' '' '' '' 'palette-fade-done'),
    (& $newMakuCommandRow 5 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$') 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 6 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0564\s*$') 'showtext' '' '0564' '' $allTexts[0x0564]),
    (& $newMakuCommandRow 7 (& $findMakuSourceLine '(?m)^\s*wait\s+60\s*$') 'wait' '' '60' '' ''),
    (& $newMakuCommandRow 8 (& $findMakuSourceLine '(?m)^\s*playsound\s+SNDCTRL_STOPMUSIC\s*$') 'playsound' '' $makuStopSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 9 (& $findMakuSourceLine '(?m)^\s*asm15\s+makuTree_setAnimation,\s*\$04\s*$') 'setanimationcontinue' 'MakuTree' '04' '' $makuAnimations[4]),
    (& $newMakuCommandRow 10 (& $findMakuSourceLine '(?m)^\s*wait\s+60\s*$' 1) 'wait' '' '60' '' ''),
    (& $newMakuCommandRow 11 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$') 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 12 (& $findMakuSourceLine '(?m)^\s*writememory\s+wCutsceneTrigger,\s*CUTSCENE_MAKU_TREE_DISAPPEARING\s*$') 'writememory' '' $makuCutsceneIndex.Groups['value'].Value '' 'wCutsceneTrigger'),
    (& $newMakuCommandRow 13 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$' 1) 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 14 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0540\s*$') 'showtext' '' '0540' '' $allTexts[0x0540]),
    (& $newMakuCommandRow 15 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$' 1) 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 16 (& $findMakuSourceLine '(?m)^\s*wait\s+210\s*$' 2) 'wait' '' '210' '' ''),
    (& $newMakuCommandRow 17 (& $findMakuSourceLine '(?m)^\s*showtextlowindex\s+<TX_0541\s*$') 'showtext' '' '0541' '' $allTexts[0x0541]),
    (& $newMakuCommandRow 18 (& $findMakuSourceLine '(?m)^\s*playsound\s+SND_MAKUDISAPPEAR\s*$' 2) 'playsound' '' $makuDisappearSound.Groups['value'].Value '' ''),
    (& $newMakuCommandRow 19 (& $findMakuSourceLine '(?m)^\s*wait\s+150\s*$') 'wait' '' '150' '' ''),
    (& $newMakuCommandRow 20 (& $findMakuSourceLine '(?m)^\s*writememory\s+wTmpcfc0\.genericCutscene\.state,\s*\$01\s*$') 'writememory' '' '01' '' 'wTmpcfc0.genericCutscene.state'),
    (& $newMakuCommandRow 21 (& $findMakuSourceLine '(?m)^\s*asm15\s+incMakuTreeState\s*$') 'native' '' '' '' 'incMakuTreeState'),
    (& $newMakuCommandRow 22 (& $findMakuSourceLine '(?m)^\s*scriptend\s*$') 'scriptend' '' '' '' '')
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_commands.tsv'),
    $makuCommandRows)

# Immediately after the young Maku Tree is saved, wMakuTreeState=$02 selects
# the adult-tree script in present room 0:38. Export the complete looping
# script, including its choice branch and the persistent falling Seed Satchel,
# rather than reducing the event to a one-shot dialogue/reward.
$makuSavedTextIds = @(0x0542..0x0550) + @(0x0561)
foreach ($textId in $makuSavedTextIds) {
    if (-not $allTexts.ContainsKey($textId)) {
        throw "Could not resolve saved Maku Tree text TX_$($textId.ToString('x4'))."
    }
    if (-not $allTextPositions.ContainsKey($textId) -or
        $allTextPositions[$textId] -ne 2) {
        throw "Expected saved Maku Tree TX_$($textId.ToString('x4')) to use \\pos(2)."
    }
}
if ($allTexts[0x054a] -notmatch '\\opt\(\).*\\opt\(\)') {
    throw 'Saved Maku Tree TX_054a no longer contains its Yes/No options.'
}

$makuSavedOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'asm15', 'setmusic', 'setcollisionradii', 'makeabuttonsensitive',
    'jumpifroomflagset', 'checkabutton', 'disableinput',
    'showtextlowindex', 'wait', 'jumpiftextoptioneq', 'setglobalflag',
    'writememory', 'enableinput', 'scriptjump')) {
    [void]$makuSavedOpcodes.Add($opcode)
}
$makuSavedParsed = @(Read-AssemblyCutsceneCommands `
    (Join-Path $Disassembly 'scripts\ages\scriptHelper.s') `
    'makuTree_subid02Script_body' $makuSavedOpcodes)
if ($makuSavedParsed.Count -ne 68) {
    throw "Expected 68 saved Maku Tree commands, parsed $($makuSavedParsed.Count)."
}
$makuSavedTargets = @{}
foreach ($command in $makuSavedParsed) {
    if (-not $makuSavedTargets.ContainsKey($command.Label)) {
        $makuSavedTargets[$command.Label] = $command.Index
    }
}
if ($makuSavedTargets['@explainAgain'] -ne 26 -or
    $makuSavedTargets['@npcLoop'] -ne 60) {
    throw 'Saved Maku Tree branch labels no longer begin at commands 26 and 60.'
}

$makuTreeMusicMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*MUS_MAKU_TREE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuSolveSoundMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_SOLVEPUZZLE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$makuLandingSoundMatch = [regex]::Match(
    $makuMusicSource,
    '(?m)^\s*SND_DROPESSENCE\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
$globalFlagSource = Read-ImportText (
    Join-Path $Disassembly 'constants\common\globalFlags.s')
$makuAdviceFlagMatch = [regex]::Match(
    $globalFlagSource,
    '(?m)^\s*GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP\s+db\s*;\s*\$(?<value>[0-9a-f]{2})')
if (-not $makuTreeMusicMatch.Success -or
    $makuTreeMusicMatch.Groups['value'].Value -ne '1e' -or
    -not $makuSolveSoundMatch.Success -or
    $makuSolveSoundMatch.Groups['value'].Value -ne '4d' -or
    -not $makuLandingSoundMatch.Success -or
    $makuLandingSoundMatch.Groups['value'].Value -ne '77' -or
    -not $makuAdviceFlagMatch.Success -or
    $makuAdviceFlagMatch.Groups['value'].Value -ne '3e') {
    throw 'Could not resolve saved Maku Tree music, Satchel sounds, or advice flag.'
}

$makuSavedCommandRows = [Collections.Generic.List[string]]::new()
$makuSavedCommandRows.Add(
    "# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64")
foreach ($command in $makuSavedParsed) {
    $opcode = $command.Opcode
    $actor = ''
    $arg0 = ''
    $arg1 = ''
    $payload = ''
    switch ($command.Opcode) {
        'asm15' {
            if ($command.Operands -match '^makuTree_setAnimation,\s*\$(?<animation>[0-4][0-9a-f]?)$') {
                $animation = [Convert]::ToInt32($Matches['animation'], 16)
                if ($animation -gt 4) { throw "Invalid Maku Tree animation at source line $($command.Line)." }
                $opcode = 'setanimationcontinue'
                $actor = 'MakuTree'
                $arg0 = $animation.ToString('x2')
                $payload = $makuAnimations[$animation]
            }
            elseif ($command.Operands -eq 'makuTree_checkSpawnSeedSatchel') {
                $opcode = 'native'
                $payload = 'makuTree_checkSpawnSeedSatchel'
            }
            elseif ($command.Operands -eq 'makuTree_dropSeedSatchel') {
                $opcode = 'native'
                $payload = 'makuTree_dropSeedSatchel'
            }
            else {
                throw "Unsupported saved Maku Tree asm15 '$($command.Operands)' at source line $($command.Line)."
            }
        }
        'setmusic' {
            if ($command.Operands -ne 'MUS_MAKU_TREE') {
                throw "Unexpected saved Maku Tree music '$($command.Operands)'."
            }
            $arg0 = $makuTreeMusicMatch.Groups['value'].Value
        }
        'setcollisionradii' {
            if ($command.Operands -notmatch '^\$(?<y>[0-9a-f]{2}),\s*\$(?<x>[0-9a-f]{2})$') {
                throw "Malformed saved Maku Tree collision radii at source line $($command.Line)."
            }
            $actor = 'MakuTree'
            $arg0 = $Matches['y']
            $arg1 = $Matches['x']
        }
        'makeabuttonsensitive' { $actor = 'MakuTree' }
        'checkabutton' { $actor = 'MakuTree' }
        'jumpifroomflagset' {
            if ($command.Operands -notmatch '^\$(?<flag>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed saved Maku Tree room-flag branch at source line $($command.Line)."
            }
            $arg0 = $Matches['flag']
            $arg1 = $makuSavedTargets[$Matches['target']].ToString()
        }
        'showtextlowindex' {
            if ($command.Operands -notmatch '^<TX_(?<id>[0-9a-f]{4})$') {
                throw "Malformed saved Maku Tree text at source line $($command.Line)."
            }
            $textId = [Convert]::ToInt32($Matches['id'], 16)
            if (-not $makuSavedTextIds.Contains($textId)) {
                throw "Unexpected saved Maku Tree text TX_$($Matches['id'])."
            }
            $opcode = 'showtext'
            $arg0 = $Matches['id']
            $payload = $allTexts[$textId]
        }
        'wait' { $arg0 = [int]$command.Operands }
        'jumpiftextoptioneq' {
            if ($command.Operands -notmatch '^\$(?<value>[0-9a-f]{2}),\s*(?<target>@[A-Za-z0-9_]+)$') {
                throw "Malformed saved Maku Tree text-option branch at source line $($command.Line)."
            }
            $arg0 = $Matches['value']
            $arg1 = $makuSavedTargets[$Matches['target']].ToString()
        }
        'setglobalflag' {
            if ($command.Operands -ne 'GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP') {
                throw "Unexpected saved Maku Tree global flag '$($command.Operands)'."
            }
            $arg0 = $makuAdviceFlagMatch.Groups['value'].Value
        }
        'writememory' {
            if ($command.Operands -ne 'wMakuMapTextPresent, <TX_054f') {
                throw "Unexpected saved Maku Tree WRAM write '$($command.Operands)'."
            }
            $arg0 = '4f'
            $payload = 'wMakuMapTextPresent'
        }
        'scriptjump' {
            if (-not $makuSavedTargets.ContainsKey($command.Operands)) {
                throw "Unknown saved Maku Tree branch target '$($command.Operands)'."
            }
            $arg0 = $makuSavedTargets[$command.Operands].ToString()
        }
    }
    $makuSavedCommandRows.Add((New-CutsceneCommandRow `
        $command.Script $command.Index $command.Label $command.Line `
        $opcode $actor "$arg0" "$arg1" $payload))
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_saved_commands.tsv'),
    $makuSavedCommandRows)

$makuHelpers = $makuScriptSource.Substring(
    $makuScriptSource.IndexOf('makuTree_dropSeedSatchel:'),
    $makuScriptSource.IndexOf('makuTree_spawnMakuSeed:') -
        $makuScriptSource.IndexOf('makuTree_dropSeedSatchel:'))
if ($makuHelpers -notmatch '(?ms)makuTree_dropSeedSatchel:.*?bit 7,a.*?set 7,\(hl\).*?TREASURE_SEED_SATCHEL.*?ld \(hl\),\$02.*?ld \(hl\),\$60.*?ld b,\$50.*?cp \$64.*?cp \$3c.*?ld b,\$40.*?cp \$50.*?ld b,\$60.*?wMakuTreeSeedSatchelXPosition' -or
    $makuHelpers -notmatch '(?ms)makuTree_checkSpawnSeedSatchel:.*?bit 5,a.*?bit 7,a.*?TREASURE_SEED_SATCHEL.*?ld \(hl\),\$03.*?ld a,\$58.*?wMakuTreeSeedSatchelXPosition') {
    throw 'Saved Maku Tree Seed Satchel drop/respawn helpers changed.'
}
$seedSatchel02 = $treasureObjectRecords['TREASURE_OBJECT_SEED_SATCHEL_02']
$seedSatchel03 = $treasureObjectRecords['TREASURE_OBJECT_SEED_SATCHEL_03']
if ($null -eq $seedSatchel02 -or $null -eq $seedSatchel03 -or
    $seedSatchel02.Treasure -ne 0x19 -or $seedSatchel03.Treasure -ne 0x19 -or
    $seedSatchel02.Graphic -ne 0x20 -or $seedSatchel03.Graphic -ne 0x20) {
    throw 'Could not resolve both Seed Satchel treasure-object records.'
}
$treasureObjectSourceText = $treasureObjectSource -join "`n"
if ($treasureObjectSourceText -notmatch 'm_TreasureSubid \$29, \$00, \$2d, \$20, TREASURE_OBJECT_SEED_SATCHEL_02' -or
    $treasureObjectSourceText -notmatch 'm_TreasureSubid \$09, \$00, \$2d, \$20, TREASURE_OBJECT_SEED_SATCHEL_03') {
    throw 'Seed Satchel falling/respawn treasure modes changed.'
}
$treasureInteractionSource = Read-ImportText (
    Join-Path $Disassembly 'object_code\common\interactions\treasure.s')
if ($treasureInteractionSource -notmatch '(?ms)^@spawnMode2:.*?@@substate0:.*?ld \(hl\),40.*?SND_SOLVEPUZZLE.*?@@substate1:.*?ld \(hl\),\$02\s+inc l\s+ld \(hl\),\$02.*?objectGetZAboveScreen.*?@@substate2:.*?ld c,\$10\s+call objectUpdateSpeedZ_paramC.*?SND_DROPESSENCE.*?interactionDecCounter1.*?ld bc,-\$aa' -or
    $treasureInteractionSource -notmatch '(?ms)^@grabMode1:\s*ldbc \$80,\$fc.*?ld b,\$f2\s+call objectTakePositionWithOffset') {
    throw 'INTERAC_TREASURE falling spawn mode $02 or one-hand grab mode $01 changed.'
}
$objectMathSource = Read-ImportText (Join-Path $Disassembly 'code\bank0.s')
if ($objectMathSource -notmatch '(?ms)^objectGetZAboveScreen:.*?ldh a,\(<hCameraY\)\s+sub b\s+sub \$08\s+cp \$80\s+ret nc\s+ld a,\$80\s+ret') {
    throw 'objectGetZAboveScreen no longer uses cameraY-Y-$08 clamped to -$80.'
}
# Room 0:38 is one screen tall, so hCameraY is zero. Y=$60 therefore starts
# at signed Z -$68, immediately above the screen as the native helper specifies.
$makuSatchelInitialZ = [Math]::Max(-0x80, -0x60 - 0x08)
$makuSavedEventRows = @(
    "# group`troom`tid`tsubid`tanimation0`tanimation1`tanimation2`tanimation3`tanimation4`textra-sprite`ttextbox-position`tmusic`tadvice-flag`tmap-text-low`tfalling-object`trespawn-object`tdrop-y`trespawn-y`tdefault-x`tlower-bound`tmiddle-bound`tupper-bound`tlower-band-x`tupper-band-x`tinitial-z`tdrop-delay`tbounce-count`tgravity`tbounce-speed`tspawn-sound`tlanding-sound",
    (@(
        '0', '38', '87', '00',
        $makuAnimations[0], $makuAnimations[1], $makuAnimations[2],
        $makuAnimations[3], $makuAnimations[4], $makuExtraSprite,
        '2', $makuTreeMusicMatch.Groups['value'].Value,
        $makuAdviceFlagMatch.Groups['value'].Value, '4f',
        'TREASURE_OBJECT_SEED_SATCHEL_02',
        'TREASURE_OBJECT_SEED_SATCHEL_03',
        '60', '58', '50', '3c', '50', '64', '60', '40',
        $makuSatchelInitialZ.ToString(), '40', '2', '10', '-170',
        $makuSolveSoundMatch.Groups['value'].Value,
        $makuLandingSoundMatch.Groups['value'].Value
    ) -join "`t")
)
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\maku_tree_saved_event.tsv'),
    $makuSavedEventRows)

$makuPaletteLabels = [Collections.Generic.List[string]]::new()
foreach ($symbol in $makuPaletteSymbols) {
    $headerMatch = [regex]::Match(
        $paletteHeaderSource,
        "(?ms)^m_PaletteHeaderStart\s+\`$[0-9a-f]{2},[ \t]*$([regex]::Escape($symbol))(?<body>.*?)(?=^m_PaletteHeaderStart|\z)")
    if (-not $headerMatch.Success) {
        throw "Maku Tree palette header not found: $symbol"
    }
    $background = [regex]::Match(
        $headerMatch.Groups['body'].Value,
        'm_PaletteHeaderBg\s+2,\s*(?<count>[46]),\s*(?<label>paletteData[0-9a-f]+)')
    $expectedPaletteCount = if ($symbol -eq 'PALH_8f') { 6 } else { 4 }
    if (-not $background.Success -or
        [int]$background.Groups['count'].Value -ne $expectedPaletteCount) {
        throw "$symbol did not load the expected $expectedPaletteCount Maku Tree BG palettes."
    }
    $makuPaletteLabels.Add($background.Groups['label'].Value)
}
$makuBasePaletteLabel = $makuPaletteLabels[$makuInitialPaletteIndex]
$makuPaletteColors = @{}
foreach ($label in $makuPaletteLabels) {
    $labelIndex = $paletteDataSource.IndexOf("${label}:", [StringComparison]::Ordinal)
    if ($labelIndex -lt 0) { throw "Maku Tree palette data not found: $label" }
    $nextLabel = $paletteDataSource.IndexOf(
        'paletteData', $labelIndex + $label.Length, [StringComparison]::Ordinal)
    if ($nextLabel -lt 0) { $nextLabel = $paletteDataSource.Length }
    $block = $paletteDataSource.Substring($labelIndex, $nextLabel - $labelIndex)
    $colors = [regex]::Matches(
        $block,
        'm_RGB16\s+\$(?<r>[0-9a-f]{2})\s+\$(?<g>[0-9a-f]{2})\s+\$(?<b>[0-9a-f]{2})')
    $expectedColors = if ($label -eq $makuBasePaletteLabel) { 24 } else { 16 }
    if ($colors.Count -lt $expectedColors) {
        throw "$label contains fewer than $expectedColors Maku Tree background colors."
    }
    $makuPaletteColors[$label] = $colors
}
$makuPaletteBytes = [Collections.Generic.List[byte]]::new()
foreach ($label in $makuPaletteLabels) {
    for ($color = 0; $color -lt 24; $color++) {
        # PALH_9a/PALH_c4/PALH_c5 replace BG palettes 2-5 only. Palettes
        # 6-7 retain the values installed by the initial PALH_8f load.
        $sourceLabel = if ($color -lt 16) { $label } else { $makuBasePaletteLabel }
        $sourceColor = $makuPaletteColors[$sourceLabel][$color]
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['r'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['g'].Value, 16))
        $makuPaletteBytes.Add([Convert]::ToByte($sourceColor.Groups['b'].Value, 16))
    }
}
if ($makuPaletteBytes.Count -ne 288) {
    throw "Expected 288 Maku Tree disappearance palette bytes, got $($makuPaletteBytes.Count)."
}
Write-GeneratedBytes(
    (Join-Path $destination 'metadata\maku_tree_disappear_palettes.bin'),
    $makuPaletteBytes.ToArray())
