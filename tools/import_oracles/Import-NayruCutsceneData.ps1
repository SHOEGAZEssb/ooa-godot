# Parse the active Nayru/Ralph/Ghost script lanes with one source-aware reader.
# This is intentionally done before emitting the merged controller stream so a
# newly introduced opcode fails import at its exact source file, line and label.
$supportedNayruOpcodes = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($opcode in @(
    'setanimation', 'checkmemoryeq', 'wait', 'setspeed', 'moveup',
    'moveright', 'movedown', 'moveleft', 'showtext', 'writememory',
    'asm15', 'setangle', 'applyspeed', 'setcoords', 'writeobjectbyte',
    'playsound', 'orroomflag', 'scriptend', 'callscript',
    'jumpifmemoryeq', 'scriptjump')) {
    [void]$supportedNayruOpcodes.Add($opcode)
}
$nayruScriptPath = Join-Path $Disassembly 'scripts\ages\scripts.s'
$nayruLaneSpecs = @(
    'nayruScript00_part1',
    'nayruScript00_part2',
    'ralphSubid00Script'
)
foreach ($lane in $nayruLaneSpecs) {
    $parsedLane = @(Read-AssemblyCutsceneCommands `
        $nayruScriptPath $lane $supportedNayruOpcodes)
    if ($parsedLane[-1].Opcode -ne 'scriptend') {
        throw "$nayruScriptPath`:$($parsedLane[-1].Line): $lane does not terminate in scriptend."
    }
}

# Preserve the original substate-8 lane and its instruction locations. The
# native controller supplies $cfd2; this script writes the $cfd0=$1a handshake.
$nayruGhostCommands = @(Read-AssemblyCutsceneCommands `
    $nayruScriptPath 'ghostVeranSubid1Script_part2' $supportedNayruOpcodes)
$nayruGhostRows = ConvertTo-CutsceneCommandRows $nayruGhostCommands 'GhostVeran' -symbols @{
    SPEED_040 = (Resolve-ObjectSpeed '40')
    SPEED_080 = (Resolve-ObjectSpeed '80')
    'wTmpcfc0.genericCutscene.cfd2' = 'NayruPortalSignal'
    'wTmpcfc0.genericCutscene.cfd0' = 'NayruPhase'
}
Write-CutsceneGeneratedTable((Join-Path $destination 'cutscenes\nayru_ghost_commands.tsv'), $nayruGhostRows)

# The stone-child vignette has two concurrent scripts. Preserve their native
# movement counters and signal $03 instead of baking both into elapsed frames.
foreach ($lane in @(@('boySubid01Script', 'VignetteBoy'), @('oldLadySubid1Script', 'VignetteLady'))) {
    $endLabel = if ($lane[0] -eq 'boySubid01Script') { 'boySubid02Script_afterGotSeedSatchel' } else { '' }
    $parsed = @(Read-AssemblyCutsceneCommands $nayruScriptPath $lane[0] $supportedNayruOpcodes $endLabel)
    if ($parsed[-1].Opcode -ne 'scriptend') {
        throw "Initial Nayru: $($lane[0]) lost its scriptend (including boyStubScript fallthrough)."
    }
    $animations = @{ 0 = 'native'; 1 = 'native'; 2 = 'native'; 3 = 'native' }
    $rows = ConvertTo-CutsceneCommandRows $parsed $lane[1] -animations $animations -symbols @{
        SPEED_100 = (Resolve-ObjectSpeed '100')
        SPEED_040 = (Resolve-ObjectSpeed '40')
        SPEED_280 = (Resolve-ObjectSpeed '280')
        'wTmpcfc0.genericCutscene.cfd1' = 'VignetteSignal'
    } -bindings @{
        'asm15|scriptHelp.createExclamationMark, $3c' = @{ Opcode = 'native'; Payload = 'BoyExclamation' }
    }
    Write-CutsceneGeneratedTable((Join-Path $destination "cutscenes\nayru_$($lane[1].ToLowerInvariant())_commands.tsv"), $rows)
}

$nayruGhostNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\ghostVeran.s')
$nayruScriptHelperSource = Read-ImportText (Join-Path $Disassembly 'scripts\ages\scriptHelper.s')
if ($nayruScriptSource -notmatch '(?s)impaScript1:.*?showtextdifferentforlinked TX_0112, TX_0113.*?showtextdifferentforlinked TX_0115, TX_0116.*?jumpifmemoryeq wIsLinkedGame, \$01, @linked.*?giveitem TREASURE_SWORD, \$00.*?@linked:\s+giveitem TREASURE_SHIELD, \$00' -or
    $nayruGhostNative -notmatch '(?s)@subid0Init:\s+ld e,Interaction.counter1\s+ld a,Interaction.var38' -or
    $nayruScriptHelperSource -notmatch '(?s)ghostVeranApplySpeedUntilVar38Zero:\s+ld h,d\s+ld l,Interaction.var38\s+dec \(hl\)\s+ret z\s+call objectApplySpeed\s+jp objectApplySpeed') {
    throw 'Initial Nayru: Impa linked reward branch, ghost $38 appearance delay, or decrement-before-double-movement helper changed.'
}
$nayruGhostRise = [regex]::Match($nayruGhostNative,
    '(?s)@substate0:.*?ld \(hl\),\$(?<counter>[0-9a-f]{2})\s+ld l,Interaction.angle\s+ld \(hl\),\$00\s+ld l,Interaction.speed\s+ld \(hl\),\$(?<speed>[0-9a-f]{2})')
if (-not $nayruGhostRise.Success -or $nayruGhostNative -notmatch
    '(?ms)^@substate1:\s+call interactionDecCounter1\s+jp nz,objectApplySpeed') {
    throw 'ghostVeran.s: initial rise counter/speed or decrement-before-movement boundary changed.'
}
$nayruGhostRiseCounter = [Convert]::ToInt32($nayruGhostRise.Groups['counter'].Value, 16)
$nayruGhostRiseSpeed = $nayruGhostRise.Groups['speed'].Value
$nayruGhostRiseLine = Get-AssemblySourceLine $nayruGhostNative `
    '^\s*jp nz,objectApplySpeed' 'runVeranGhostSubid0/@substate1'

# The intro is a multi-object controller: independent interaction scripts,
# Link object code, and native palette/room handlers synchronize through cfd0.
# Export the already validated active-path orchestration as typed records while
# retaining native handlers only for the non-script object code.
$nayruControllerLine = Get-AssemblySourceLine `
    $nayruCutsceneSource '^nayruSingingCutsceneHandler:' 'nayruSingingCutsceneHandler'
$nayruPart1Line = Get-AssemblySourceLine `
    $nayruScriptSource '^nayruScript00_part1:' 'nayruScript00_part1'
$nayruPart2Line = Get-AssemblySourceLine `
    $nayruScriptSource '^nayruScript00_part2:' 'nayruScript00_part2'
$nayruRalphLine = Get-AssemblySourceLine `
    $nayruScriptSource '^ralphSubid00Script:' 'ralphSubid00Script'
$nayruGhostLine = Get-AssemblySourceLine `
    $nayruScriptSource '^ghostVeranSubid1Script_part2:' 'ghostVeranSubid1Script_part2'

$nayruCommandRows = [Collections.Generic.List[string]]::new()
$nayruCommandRows.Add(
    '# script`tlabel`tindex`tsource-line`topcode`tactor`targ0`targ1`tpayload-base64')
$addNayruCommand = {
    param(
        [string]$opcode,
        [string]$actor = '',
        [string]$arg0 = '',
        [string]$arg1 = '',
        [string]$payload = '',
        [string]$script = 'nayruSingingCutsceneHandler',
        [int]$line = $nayruControllerLine)
    $nayruCommandRows.Add((New-CutsceneCommandRow `
        $script ($nayruCommandRows.Count - 1) $script $line `
        $opcode $actor $arg0 $arg1 $payload))
}
$nayruWait = { param([int]$frames) & $addNayruCommand 'waitframes' '' $frames '' '' }
$nayruText = { param([string]$id, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    & $addNayruCommand 'dialogue' '' $id '' '' $script $line }
$nayruAnimation = { param([string]$actor, [int]$animation, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    & $addNayruCommand 'setanimation' $actor $animation.ToString('x2') '' '' $script $line }
$nayruMove = { param([string]$actor, [double]$dx, [double]$dy, [int]$frames, [int]$animation = -1, [bool]$setAnimation = $false, [string]$script = 'nayruSingingCutsceneHandler', [int]$line = $nayruControllerLine)
    if ($dx -ne 0 -and $dy -ne 0) { throw 'Initial Nayru cardinal movement received a diagonal.' }
    $pixelsPerUpdate = [Math]::Abs($dx + $dy) / $frames
    $speed = switch ($pixelsPerUpdate) { 0.25 { '40' } 0.5 { '80' } 1 { '100' } 1.5 { '180' } 2 { '200' } 3 { '300' } default { throw "Unsupported Nayru speed $pixelsPerUpdate" } }
    $angle = if ($dx -gt 0) { 8 } elseif ($dx -lt 0) { 24 } elseif ($dy -gt 0) { 16 } else { 0 }
    $initialDelay = if ($actor -eq 'Player') { 0 } else { 1 }
    $selectedAnimation = if ($setAnimation) { $animation } else { -1 }
    $raw = Resolve-ObjectSpeed $speed
    & $addNayruCommand 'nativeblock' $actor ($frames + $initialDelay) '' "ObjectMovement`0$raw,$angle,$selectedAnimation,1,1,$initialDelay" $script $line }
$nayruParallelMove = { param([string]$actor, [double]$dx, [double]$dy, [int]$frames, [string]$actor2, [double]$dx2, [double]$dy2, [int]$frames2, [int]$delay1, [int]$delay2)
    $motions = @()
    foreach ($motion in @(@($actor, $dx, $dy, $frames, $delay1), @($actor2, $dx2, $dy2, $frames2, $delay2))) {
        $pixels = [Math]::Abs($motion[1] + $motion[2]) / $motion[3]
        $speed = switch ($pixels) { 0.25 { '40' } 1.5 { '180' } 3 { '300' } default { throw "Unsupported parallel Nayru speed $pixels" } }
        $angle = if ($motion[1] -lt 0) { 24 } elseif ($motion[2] -lt 0) { 0 } else { 16 }
        $delay = $motion[4]
        $count = $motion[3] + $delay
        $raw = Resolve-ObjectSpeed $speed
        $motions += "$($motion[0]);$count;$raw,$angle,-1,1,1,$delay"
    }
    & $addNayruCommand 'nativeblock' '' ([Math]::Max($frames + $delay1, $frames2 + $delay2)) '' ("ParallelObjectMovement`0" + ($motions -join '|')) }
$nayruNative = { param([string]$handler)
    & $addNayruCommand 'nativeyield' '' '' '' $handler }
$nayruBlock = { param([string]$handler, [int]$frames, [string]$actor = '', [string]$arguments = '')
    $payload = if ([string]::IsNullOrEmpty($arguments)) { $handler } else { "$handler`0$arguments" }
    & $addNayruCommand 'nativeblock' $actor $frames '' $payload }
$nayruObjectMove = {
    param([string]$actor, [string]$speed, [int]$angle, [int]$frames,
        [int]$animation = -1, [int]$applications = 1, [int]$skipFinal = 1)
    $rawSpeed = Resolve-ObjectSpeed $speed
    $initialDelay = if ($actor -eq 'Player' -or $applications -eq 2) { 0 } else { 1 }
    $handler = if ($applications -eq 2) { 'GhostFlight' } else { 'ObjectMovement' }
    $duration = if ($applications -eq 2) { $frames * 2 + 1 } else { $frames + $initialDelay }
    & $nayruBlock $handler $duration $actor "$rawSpeed,$angle,$animation,$applications,$skipFinal,$initialDelay"
}
$nayruSound = { param([string]$sound)
    & $addNayruCommand 'playsound' '' $sound '' '' }

& $nayruNative 'SetupNayruPossessionScene'
& $nayruBlock 'Fade' 11 '' 'in'
& $nayruWait 30; & $nayruBlock 'Jump' 1 'Ralph'; & $nayruWait 30
& $nayruText '2a00' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 30
& $nayruNative 'FacePlayerUp'; & $nayruAnimation 'Nayru' 2 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 10
& $nayruMove 'Nayru' 0 8 32 2 $true 'nayruScript00_part1' $nayruPart1Line
& $nayruWait 30; & $nayruText '1d00' 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 30
& $nayruNative 'FacePlayerRight'; & $nayruBlock 'Jump' 1 'Ralph'; & $nayruWait 10
& $nayruText '2a22' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 30
& $nayruWait 40; & $nayruNative 'FacePlayerUp'; & $nayruText '1d22' 'nayruScript00_part1' $nayruPart1Line; & $nayruWait 30
& $nayruAnimation 'Impa' 2; & $nayruWait 30; & $nayruNative 'FastMusicFadeOut'; & $nayruWait 30
& $nayruMove 'Impa' 32 0 32 1 $true; & $nayruWait 8
& $nayruMove 'Impa' 0 -16 16 0 $true; & $nayruWait 30
& $nayruNative 'PlaySideviewMusic'; & $nayruAnimation 'Impa' 4; & $nayruWait 240
& $nayruText '5600'; & $nayruNative 'FacePlayerDown'; & $nayruNative 'AlarmNayruAudience'
& $nayruWait 60; & $nayruAnimation 'Impa' 0; & $nayruWait 60; & $nayruText '5606'; & $nayruWait 10
& $nayruAnimation 'Impa' 7
& $nayruObjectMove 'Impa' '80' 0x16 0x48
& $nayruNative 'SpawnGhostVeran'; & $nayruBlock 'RoomPalette' 32
& $nayruNative 'BeginNayruAudienceEscape'; & $nayruBlock 'WaitForGhostAppearance' 1
& $addNayruCommand 'nativeblock' 'GhostVeran' $nayruGhostRiseCounter '' "GhostInitialRise`0$nayruGhostRiseSpeed" 'runVeranGhostSubid0' $nayruGhostRiseLine
& $nayruWait 60
& $nayruBlock 'VeranReaction' 121
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x1c 0x11 -1 2 1; & $nayruWait 8
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x0b 0x25 -1 2 1; & $nayruWait 8
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x18 0x13 -1 2 1; & $nayruWait 8
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x02 0x19 -1 2 1; & $nayruWait 8
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x0a 0x0c -1 2 1; & $nayruWait 8
& $nayruSound '6b'; & $nayruObjectMove 'GhostVeran' '200' 0x14 0x11 -1 2 1; & $nayruWait 30
& $nayruNative 'SpawnHumanVeran'; & $nayruBlock 'Flicker' 120 'GhostVeran'; & $nayruWait 120
& $nayruAnimation 'HumanVeran' 1; & $nayruWait 30; & $nayruText '5601'; & $nayruWait 30
& $nayruAnimation 'HumanVeran' 0; & $nayruWait 60; & $nayruSound '8d'
& $nayruBlock 'Flicker' 120 'GhostVeran' 'PlaySwordObtained'
& $nayruNative 'HideHumanVeran'; & $nayruWait 30
& $nayruObjectMove 'GhostVeran' '80' 0x0b 0x50; & $nayruWait 30
& $nayruText '5602'; & $nayruWait 30; & $nayruNative 'BeginGhostRumble'; & $nayruWait 120
& $nayruMove 'GhostVeran' 0 10.25 41; & $nayruWait 60
# Ghost's signal-$13 write continues into applyspeed initialization. Nayru's
# earlier slot observes it next update, then yields through speed/angle/init.
& $nayruNative 'BeginGhostCharge'; & $nayruParallelMove 'GhostVeran' 0 -102 34 'Nayru' 0 -8 32 0 4
& $nayruNative 'FinishGhostCharge'; & $nayruBlock 'Fade' 32 '' 'out'
& $nayruWait 60; & $nayruNative 'HideGhostVeranAfterPossession'
& $nayruNative 'BeginNayruPossessionRecovery'; & $nayruBlock 'Fade' 97 '' 'in'
& $nayruBlock 'WaitForPossessionRecovery' 1; & $nayruWait 120
& $nayruMove 'Ralph' -16 0 16 3 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruNative 'SpawnRalphSword'; & $nayruMove 'Ralph' 0 -24 24 0 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruAnimation 'Ralph' 4 'ralphSubid00Script' $nayruRalphLine
& $nayruSound '74'; & $nayruWait 60; & $nayruText '2a01' 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruText '5603' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruAnimation 'Ralph' 0 'ralphSubid00Script' $nayruRalphLine
& $nayruObjectMove 'Ralph' '20' 0x10 0x81
& $nayruWait 30; & $nayruText '5604' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruNative 'SpawnPortalLightning'; & $nayruWait 2; & $nayruNative 'ActivateNayruPortal'
& $nayruBlock 'WaitForGhostDeparture' 1; & $nayruNative 'BeginNayruPortalWait'; & $nayruWait 60
& $nayruBlock 'PortalFlight' 1 'Nayru'; & $nayruWait 20
& $nayruMove 'Ralph' 0 -48 48 0 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruMove 'Ralph' -49 0 49 3 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 40; & $nayruText '5605' 'nayruScript00_part2' $nayruPart2Line; & $nayruWait 60
& $nayruMove 'Nayru' 0 -17 17 0 $true 'nayruScript00_part2' $nayruPart2Line
& $nayruSound '95'; & $nayruBlock 'Flicker' 120 'Nayru'; & $nayruNative 'HideNayru'; & $nayruWait 120
& $nayruNative 'MediumMusicFadeOut'; & $nayruWait 90; & $nayruText '5607'; & $nayruWait 90
& $nayruBlock 'Fade' 32 '' 'out'; & $nayruNative 'BeginNayruVignette0'; & $nayruBlock 'Fade' 32 '' 'in'; & $nayruBlock 'WaitForVignetteCompletion' 1
& $nayruBlock 'Fade' 32 '' 'out'; & $nayruNative 'BeginNayruVignette1'; & $nayruBlock 'Fade' 32 '' 'in'; & $nayruBlock 'WaitForVignetteCompletion' 1
& $nayruBlock 'Fade' 32 '' 'out'; & $nayruNative 'BeginNayruVignette2'; & $nayruBlock 'Fade' 32 '' 'in'; & $nayruBlock 'WaitForVignetteCompletion' 1
& $nayruBlock 'Fade' 32 '' 'out'; & $nayruNative 'BeginNayruAftermath'; & $nayruBlock 'Fade' 32 '' 'in'
& $nayruWait 120; & $nayruText '2a02'; & $nayruWait 30
& $nayruObjectMove 'AftermathRalph' '20' 0x08 0x81; & $nayruAnimation 'AftermathRalph' 8
& $nayruWait 120; & $nayruText '2a03'; & $nayruWait 120; & $nayruAnimation 'AftermathRalph' 9
& $nayruWait 10; & $nayruAnimation 'AftermathRalph' 10; & $nayruWait 60
& $nayruObjectMove 'AftermathRalph' '20' 0x18 0x41
& $nayruObjectMove 'AftermathRalph' '40' 0x18 0x25; & $nayruWait 30
& $nayruText '2a04'; & $nayruWait 120; & $nayruNative 'RestoreAftermathPalette'; & $nayruWait 60; & $nayruAnimation 'AftermathRalph' 2
& $nayruText '2a05'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 50 0 25 1 $true
& $nayruAnimation 'AftermathRalph' 2; & $nayruSound '78'; & $nayruWait 120
& $nayruText '2a06'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 0 120 40 2 $true
& $nayruWait 60; & $nayruNative 'FinishAftermathRalphDeparture'
& $nayruWait 80; & $nayruObjectMove 'Player' '100' 0x10 0x30 -1 1 1; & $nayruWait 8
& $nayruObjectMove 'Player' '100' 0x18 0x10 -1 1 1
& $nayruBlock 'WaitForImpaRecovery' 1
& $nayruNative 'RestoreAftermathImpa'; & $nayruWait 60; & $nayruAnimation 'AftermathImpa' 3
& $nayruWait 50; & $nayruAnimation 'AftermathImpa' 1; & $nayruWait 30
& $nayruAnimation 'AftermathImpa' 3; & $nayruWait 10; & $nayruAnimation 'AftermathImpa' 1
& $nayruWait 60; & $nayruText '0110'; & $nayruWait 30; & $nayruAnimation 'AftermathImpa' 3
& $nayruWait 30; & $nayruText '0112'; & $nayruWait 30; & $nayruAnimation 'AftermathImpa' 1
& $nayruText '0115'; & $nayruWait 30; & $nayruNative 'BeginNayruSwordGift'
& $nayruNative 'GrantNayruSword'; & $nayruText '001c'; & $nayruNative 'RemoveNayruSwordEffect'
& $nayruWait 30; & $nayruNative 'FacePlayerLeft'; & $nayruWait 30; & $nayruText '0117'; & $nayruWait 30
& $nayruMove 'AftermathImpa' 65 0 65 1 $true; & $nayruWait 8
& $nayruMove 'AftermathImpa' 0 33 33 2 $true; & $nayruWait 30
& $nayruNative 'RestoreRoomMusic'; & $nayruWait 30
& $addNayruCommand 'scriptend' '' '' '' ''

if ($nayruCommandRows.Count -lt 200) {
    throw "Initial Nayru typed command stream is unexpectedly short ($($nayruCommandRows.Count - 1) records)."
}
Write-CutsceneGeneratedTable(
    (Join-Path $destination 'cutscenes\nayru_intro_commands.tsv'),
    $nayruCommandRows)
