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

$nayruGhostNative = Read-ImportText (Join-Path $Disassembly 'object_code\ages\interactions\ghostVeran.s')
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
    $payload = @(
        $dx.ToString([Globalization.CultureInfo]::InvariantCulture),
        $dy.ToString([Globalization.CultureInfo]::InvariantCulture),
        $(if ($setAnimation) { '1' } else { '0' })) -join ','
    & $addNayruCommand 'translate' $actor $frames $animation $payload $script $line }
$nayruParallelMove = { param([string]$actor, [double]$dx, [double]$dy, [int]$frames, [string]$actor2, [double]$dx2, [double]$dy2, [int]$frames2)
    $first = $dx.ToString([Globalization.CultureInfo]::InvariantCulture) + ',' +
        $dy.ToString([Globalization.CultureInfo]::InvariantCulture)
    $second = $dx2.ToString([Globalization.CultureInfo]::InvariantCulture) + ',' +
        $dy2.ToString([Globalization.CultureInfo]::InvariantCulture)
    & $addNayruCommand 'paralleltranslate' $actor $frames $frames2 "$first|$actor2|$second" }
$nayruNative = { param([string]$handler)
    & $addNayruCommand 'nativeyield' '' '' '' $handler }
$nayruBlock = { param([string]$handler, [int]$frames, [string]$actor = '', [string]$arguments = '')
    $payload = if ([string]::IsNullOrEmpty($arguments)) { $handler } else { "$handler`0$arguments" }
    & $addNayruCommand 'nativeblock' $actor $frames '' $payload }
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
& $nayruMove 'Impa' -33.259663 13.776604 72 7 $false
& $nayruNative 'SpawnGhostVeran'; & $nayruBlock 'RoomPalette' 32
& $nayruNative 'BeginNayruAudienceEscape'; & $nayruWait 58
& $addNayruCommand 'nativeblock' 'GhostVeran' $nayruGhostRiseCounter '' "GhostInitialRise`0$nayruGhostRiseSpeed" 'runVeranGhostSubid0' $nayruGhostRiseLine
& $nayruWait 60
& $nayruAnimation 'Ralph' 2 'ralphSubid00Script' $nayruRalphLine
& $nayruNative 'BeginVeranReaction'
& $nayruParallelMove 'Player' -33 0 22 'Ralph' 0 33 22
& $nayruSound '75'; & $nayruWait 6; & $nayruMove 'Player' 0 12 8
& $nayruSound '75'; & $nayruWait 84
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -48.08326 -48.08326 17; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 123.0575 82.224396 37; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -76 0 19; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 38.26834 -92.38795 25; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' 44.346214 18.368805 12; & $nayruWait 8
& $nayruSound '6b'; & $nayruMove 'GhostVeran' -48.08326 48.08326 17; & $nayruWait 30
& $nayruNative 'SpawnHumanVeran'; & $nayruBlock 'Flicker' 120 'GhostVeran'; & $nayruWait 120
& $nayruAnimation 'HumanVeran' 1; & $nayruWait 30; & $nayruText '5601'; & $nayruWait 30
& $nayruAnimation 'HumanVeran' 0; & $nayruWait 60; & $nayruSound '8d'
& $nayruBlock 'Flicker' 120 'GhostVeran' 'PlaySwordObtained'
& $nayruNative 'HideHumanVeran'; & $nayruWait 30
& $nayruMove 'GhostVeran' 33.258785 22.222809 80; & $nayruWait 30
& $nayruText '5602'; & $nayruWait 30; & $nayruNative 'BeginGhostRumble'; & $nayruWait 120
& $nayruMove 'GhostVeran' 0 10.25 41; & $nayruWait 60
& $nayruNative 'BeginGhostCharge'; & $nayruParallelMove 'GhostVeran' 0 -102 34 'Nayru' 0 -8 32
& $nayruNative 'FinishGhostCharge'; & $nayruBlock 'Fade' 32 '' 'out'
& $nayruWait 60; & $nayruNative 'HideGhostVeranAfterPossession'
& $nayruNative 'BeginNayruPossessionRecovery'; & $nayruBlock 'Fade' 97 '' 'in'
& $nayruWait 452; & $nayruWait 120
& $nayruMove 'Ralph' -16 0 16 3 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruNative 'SpawnRalphSword'; & $nayruMove 'Ralph' 0 -24 24 0 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruAnimation 'Ralph' 4 'ralphSubid00Script' $nayruRalphLine
& $nayruSound '74'; & $nayruWait 60; & $nayruText '2a01' 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruText '5603' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruAnimation 'Ralph' 0 'ralphSubid00Script' $nayruRalphLine
& $nayruMove 'Ralph' 0 16 129 0 $false 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 30; & $nayruText '5604' 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 60
& $nayruNative 'SpawnPortalLightning'; & $nayruWait 2; & $nayruNative 'ActivateNayruPortal'
& $nayruBlock 'WaitForGhostDeparture' 1; & $nayruWait 60
& $nayruBlock 'PortalFlight' 1 'Nayru'; & $nayruWait 20
& $nayruMove 'Ralph' 0 -48 48 0 $true 'ralphSubid00Script' $nayruRalphLine; & $nayruWait 6
& $nayruMove 'Ralph' -49 0 49 3 $true 'ralphSubid00Script' $nayruRalphLine
& $nayruWait 40; & $nayruText '5605' 'nayruScript00_part2' $nayruPart2Line; & $nayruWait 60
& $nayruMove 'Nayru' 0 -17 17 0 $true 'nayruScript00_part2' $nayruPart2Line
& $nayruSound '95'; & $nayruBlock 'Flicker' 120 'Nayru'; & $nayruNative 'HideNayru'; & $nayruWait 120
& $nayruNative 'MediumMusicFadeOut'; & $nayruWait 90; & $nayruText '5607'; & $nayruWait 90
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette0'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 926
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette1'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 589
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruVignette2'; & $nayruBlock 'Fade' 11 '' 'in'; & $nayruWait 634
& $nayruBlock 'Fade' 11 '' 'out'; & $nayruNative 'BeginNayruAftermath'; & $nayruBlock 'Fade' 11 '' 'in'
& $nayruWait 120; & $nayruText '2a02'; & $nayruWait 30
& $nayruMove 'AftermathRalph' 16 0 129 9 $false; & $nayruAnimation 'AftermathRalph' 8
& $nayruWait 120; & $nayruText '2a03'; & $nayruWait 120; & $nayruAnimation 'AftermathRalph' 9
& $nayruWait 10; & $nayruAnimation 'AftermathRalph' 10; & $nayruWait 60
& $nayruMove 'AftermathRalph' -17 0 102 10 $false; & $nayruWait 30
& $nayruText '2a04'; & $nayruWait 120; & $nayruWait 60; & $nayruAnimation 'AftermathRalph' 2
& $nayruText '2a05'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 50 0 25 1 $true
& $nayruAnimation 'AftermathRalph' 2; & $nayruSound '78'; & $nayruWait 120
& $nayruText '2a06'; & $nayruWait 30; & $nayruMove 'AftermathRalph' 0 120 40 2 $true
& $nayruWait 60; & $nayruNative 'FinishAftermathRalphDeparture'
& $nayruWait 80; & $nayruMove 'Player' 0 48 48; & $nayruWait 8
& $nayruMove 'Player' -16 0 16; & $nayruWait 60; & $nayruWait 120
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
