$ErrorActionPreference = "Stop"
$project = Split-Path $importRoot -Parent
$destination = Join-Path $project "assets\oracle"

if (-not (Test-Path -LiteralPath $Disassembly -PathType Container)) {
    throw "Disassembly root not found: $Disassembly"
}

$importerProject = Join-Path $project 'tools\OracleImporter\OracleImporter.csproj'
$importerBuildOutput = @(
    & dotnet build $importerProject --nologo --verbosity quiet 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Could not build the importer source host:`n$($importerBuildOutput -join "`n")"
}
$importerHost = Join-Path $project `
    'tools\OracleImporter\bin\Debug\net8.0\OracleOfAges.Importer.dll'
if (-not (Test-Path -LiteralPath $importerHost -PathType Leaf)) {
    throw "Importer source host build did not produce: $importerHost"
}

function Start-AssemblySourceHost {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'dotnet'
    $start.Arguments = "`"$importerHost`" serve"
    $start.WorkingDirectory = $project
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['OOA_DISASSEMBLY_ROOT'] = (
        Resolve-Path -LiteralPath $Disassembly).Path
    $start.EnvironmentVariables['OOA_ASSEMBLY_SYMBOLS'] = (
        'ROM_AGES;REGION_US;AGES_ENGINE;BUILD_VANILLA')
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) {
        throw 'Could not start the importer source host.'
    }
    $ready = $process.StandardOutput.ReadLine()
    if ($ready -ne "READY`t2") {
        $errorText = $process.StandardError.ReadToEnd()
        $process.Dispose()
        throw "Importer source host did not start: '$ready' $errorText"
    }
    return $process
}

function Invoke-AssemblySourceHost(
    [Diagnostics.Process]$HostProcess,
    [string]$Command,
    [string]$Payload = ''
) {
    if ($HostProcess.HasExited) {
        throw "Importer source host exited with code $($HostProcess.ExitCode): " +
            $HostProcess.StandardError.ReadToEnd()
    }
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $encoded = [Convert]::ToBase64String($encoding.GetBytes($Payload))
    $HostProcess.StandardInput.WriteLine("$Command`t$encoded")
    $HostProcess.StandardInput.Flush()
    $response = $HostProcess.StandardOutput.ReadLine()
    if ($null -eq $response) {
        throw 'Importer source host ended without a response: ' +
            $HostProcess.StandardError.ReadToEnd()
    }
    $parts = $response.Split("`t", 2)
    if ($parts.Count -ne 2) {
        throw "Malformed importer source host response: '$response'"
    }
    $value = $encoding.GetString([Convert]::FromBase64String($parts[1]))
    if ($parts[0] -eq 'ERR') {
        throw $value
    }
    if ($parts[0] -ne 'OK') {
        throw "Unknown importer source host response: '$response'"
    }
    return $value
}

$assemblySourceHost = Start-AssemblySourceHost
$assemblyTextCache = @{}
$assemblyNodeCache = @{}
$assemblyAnimationCache = @{}

function Resolve-ImportReadPath([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) {
        return [IO.Path]::GetFullPath($path)
    }
    return [IO.Path]::GetFullPath((Join-Path $Disassembly $path))
}

function Read-ImportText([string]$path) {
    $fullPath = Resolve-ImportReadPath $path
    if ([IO.Path]::GetExtension($fullPath) -ieq '.s') {
        if (-not $assemblyTextCache.ContainsKey($fullPath)) {
            $assemblyTextCache[$fullPath] = Invoke-AssemblySourceHost `
                $assemblySourceHost 'TEXT' $fullPath
        }
        return $assemblyTextCache[$fullPath]
    }
    return [IO.File]::ReadAllText($fullPath)
}

function Read-ImportLines([string]$path) {
    $fullPath = Resolve-ImportReadPath $path
    if ([IO.Path]::GetExtension($fullPath) -ine '.s') {
        return [IO.File]::ReadAllLines($fullPath)
    }
    $text = Read-ImportText $fullPath
    $lines = [regex]::Split($text, "`r`n|`n|`r")
    if ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -eq '') {
        return @($lines[0..($lines.Count - 2)])
    }
    return $lines
}

# The supported ROM is the non-Japanese US build. Assembly source still keeps
# both REGION_JP branches, so consumers which parse object rows directly must
# select the same branch rgbasm does before interpreting those rows.
function Select-CleanUsAssemblyLines([string[]]$lines) {
    $selected = [Collections.Generic.List[string]]::new()
    $conditionals = [Collections.Generic.List[object]]::new()
    $includeLine = $true

    foreach ($line in $lines) {
        if ($line -match '^\s*\.(?<directive>ifdef|ifndef)\s+(?<symbol>[A-Za-z0-9_]+)\s*$') {
            if ($Matches['symbol'] -ne 'REGION_JP') {
                throw "Unsupported clean-US assembly conditional: $line"
            }
            $conditionTrue = $Matches['directive'] -eq 'ifndef'
            $conditionals.Add([pscustomobject]@{
                ParentIncluded = $includeLine
                ConditionTrue = $conditionTrue
                SawElse = $false
            })
            $includeLine = $includeLine -and $conditionTrue
            continue
        }
        if ($line -match '^\s*\.else\s*$') {
            if ($conditionals.Count -eq 0) {
                throw "Unexpected clean-US assembly .else: $line"
            }
            $conditional = $conditionals[$conditionals.Count - 1]
            if ($conditional.SawElse) {
                throw "Duplicate clean-US assembly .else: $line"
            }
            $conditional.SawElse = $true
            $includeLine = $conditional.ParentIncluded -and
                -not $conditional.ConditionTrue
            continue
        }
        if ($line -match '^\s*\.endif\s*$') {
            if ($conditionals.Count -eq 0) {
                throw "Unexpected clean-US assembly .endif: $line"
            }
            $conditional = $conditionals[$conditionals.Count - 1]
            $conditionals.RemoveAt($conditionals.Count - 1)
            $includeLine = $conditional.ParentIncluded
            continue
        }
        if ($includeLine) {
            $selected.Add($line)
        }
    }

    if ($conditionals.Count -ne 0) {
        throw 'Clean-US assembly conditional was not closed.'
    }
    return $selected.ToArray()
}

function Read-AssemblyLabelBlock([string]$path, [string]$label) {
    $fullPath = Resolve-ImportReadPath $path
    if ([IO.Path]::GetExtension($fullPath) -ine '.s') {
        throw "Assembly label blocks require an .s source: $fullPath"
    }
    return Invoke-AssemblySourceHost `
        $assemblySourceHost 'LABEL' "$fullPath`0$label"
}

function Read-AssemblyNodeQuery(
    [string]$path,
    [string]$command,
    [string]$label = '',
    [string]$name = ''
) {
    $fullPath = Resolve-ImportReadPath $path
    if ([IO.Path]::GetExtension($fullPath) -ine '.s') {
        throw "Assembly node queries require an .s source: $fullPath"
    }
    $key = "$command`0$fullPath`0$label`0$name"
    if (-not $assemblyNodeCache.ContainsKey($key)) {
        $json = Invoke-AssemblySourceHost `
            $assemblySourceHost $command "$fullPath`0$label`0$name"
        $parsed = $json | ConvertFrom-Json
        $assemblyNodeCache[$key] = [object[]]$parsed
    }
    return @(@($assemblyNodeCache[$key]) | Where-Object IsActive)
}

function Read-AssemblyNodes([string]$path) {
    return Read-AssemblyNodeQuery $path 'NODES'
}

function Read-AssemblyLabelNodes([string]$path, [string]$label) {
    return Read-AssemblyNodeQuery $path 'LABEL_NODES' $label
}

function Read-AssemblyLabels([string]$path, [string]$name = '') {
    return Read-AssemblyNodeQuery $path 'LABELS' '' $name
}

function Read-AssemblyDataDirectives(
    [string]$path,
    [string]$label = '',
    [string]$name = ''
) {
    return Read-AssemblyNodeQuery $path 'DATA_DIRECTIVES' $label $name
}

function Read-AssemblyMacroInvocations(
    [string]$path,
    [string]$label = '',
    [string]$name = ''
) {
    return Read-AssemblyNodeQuery $path 'MACRO_INVOCATIONS' $label $name
}

function Read-AssemblyInstructions(
    [string]$path,
    [string]$label = '',
    [string]$name = ''
) {
    return Read-AssemblyNodeQuery $path 'INSTRUCTIONS' $label $name
}

function Read-AssemblyConstants(
    [string]$path,
    [string]$label = '',
    [string]$name = ''
) {
    return Read-AssemblyNodeQuery $path 'CONSTANTS' $label $name
}

function Resolve-AssemblySourceTextPath([string]$source) {
    foreach ($entry in $assemblyTextCache.GetEnumerator()) {
        if ([object]::ReferenceEquals($entry.Value, $source)) {
            return $entry.Key
        }
    }
    return $null
}

function Get-AssemblyLabelBody([string]$source, [string]$label) {
    $sourcePath = Resolve-AssemblySourceTextPath $source
    if ($null -eq $sourcePath) {
        throw "Assembly label '$label' was requested from untracked source text."
    }
    return Read-AssemblyLabelBlock $sourcePath $label
}

function Convert-AssemblyInteger([string]$value) {
    $trimmed = $value.Trim()
    if ($trimmed -match '^(?<sign>-?)\$(?<value>[0-9a-f]+)$') {
        $number = [Convert]::ToInt32($Matches['value'], 16)
        return $(if ($Matches['sign']) { -$number } else { $number })
    }
    if ($trimmed -match '^%(?<value>[01]+)$') {
        return [Convert]::ToInt32($Matches['value'], 2)
    }
    if ($trimmed -match '^-?[0-9]+$') {
        return [Convert]::ToInt32($trimmed, 10)
    }
    throw "Assembly operand is not an integer literal: '$value'."
}

function Read-AssemblyLiteralValues(
    [string]$path,
    [string]$label,
    [string]$directive = '.db'
) {
    $values = [Collections.Generic.List[int]]::new()
    foreach ($node in Read-AssemblyDataDirectives $path $label $directive) {
        foreach ($operand in $node.Operands) {
            $values.Add((Convert-AssemblyInteger $operand))
        }
    }
    return @($values)
}

function Convert-AssemblyAnimationFrame($node) {
    return @{
        Duration = Convert-AssemblyInteger $node.Operands[0]
        PointerOffset = Convert-AssemblyInteger $node.Operands[1]
        Parameter = Convert-AssemblyInteger $node.Operands[2]
    }
}

function Read-AssemblyAnimationDefinitions(
    [string]$path,
    [string]$labelPattern,
    [bool]$stopAtNextAnimation = $false
) {
    $cacheKey =
        "$(Resolve-ImportReadPath $path)`0$labelPattern`0$stopAtNextAnimation"
    if ($assemblyAnimationCache.ContainsKey($cacheKey)) {
        return $assemblyAnimationCache[$cacheKey]
    }
    $nodes = @(Read-AssemblyNodes $path)
    $labels = @($nodes | Where-Object {
        $_.Kind -eq 'Label' -and $_.Name -match "^$labelPattern`$"
    })
    $frameNodes = @($nodes | Where-Object {
        $_.Kind -eq 'Data' -and $_.Name -ieq '.db' -and
        $_.Operands.Count -ge 3
    })
    $loopNodes = @($nodes | Where-Object {
        $_.Kind -eq 'MacroInvocation' -and $_.Name -eq 'm_AnimationLoop'
    })
    $terminalNodes = @($frameNodes | Where-Object { $_.Operands[2] -ieq '$ff' })
    $labelsByName = @{}
    $frameIndexByLabel = @{}
    $frameIndex = 0
    foreach ($label in $labels) {
        $labelsByName[$label.Name] = $label
        while ($frameIndex -lt $frameNodes.Count -and
            $frameNodes[$frameIndex].Offset -le $label.Offset) {
            $frameIndex++
        }
        $frameIndexByLabel[$label.Name] = $frameIndex
    }
    $result = @{}
    $loopIndex = 0
    $terminalIndex = 0
    for ($labelIndex = 0; $labelIndex -lt $labels.Count; $labelIndex++) {
        $label = $labels[$labelIndex]
        while ($loopIndex -lt $loopNodes.Count -and
            $loopNodes[$loopIndex].Offset -le $label.Offset) { $loopIndex++ }
        while ($terminalIndex -lt $terminalNodes.Count -and
            $terminalNodes[$terminalIndex].Offset -le $label.Offset) {
            $terminalIndex++
        }
        $loop = if ($loopIndex -lt $loopNodes.Count) {
            $loopNodes[$loopIndex]
        } else { $null }
        $terminal = if ($terminalIndex -lt $terminalNodes.Count) {
            $terminalNodes[$terminalIndex]
        } else { $null }
        $nextLabelIndex = $labelIndex + 1
        while ($nextLabelIndex -lt $labels.Count -and
            $labels[$nextLabelIndex].Name.EndsWith('Loop')) {
            $nextLabelIndex++
        }
        $nextLabelOffset = if ($nextLabelIndex -lt $labels.Count) {
            $labels[$nextLabelIndex].Offset
        } else { [int]::MaxValue }
        if ($stopAtNextAnimation) {
            $usesLoop = $null -ne $loop -and $loop.Offset -lt $nextLabelOffset
            $end = if ($usesLoop) { $loop.Offset } else { $nextLabelOffset }
        } else {
            $usesLoop = $null -ne $loop -and
                ($null -eq $terminal -or $loop.Offset -lt $terminal.Offset)
            $end = if ($usesLoop) {
                $loop.Offset
            } elseif ($null -ne $terminal) {
                $terminal.Offset + $terminal.Length
            } else {
                $nextLabelOffset
            }
        }
        $startFrame = $frameIndexByLabel[$label.Name]
        $endFrame = $startFrame
        while ($endFrame -lt $frameNodes.Count -and
            $frameNodes[$endFrame].Offset -lt $end) { $endFrame++ }
        $frames = [Collections.Generic.List[object]]::new()
        for ($index = $startFrame; $index -lt $endFrame; $index++) {
            $frames.Add((Convert-AssemblyAnimationFrame $frameNodes[$index]))
        }
        if ($frames.Count -eq 0) { continue }
        $loopStart = 0
        if ($usesLoop) {
            $target = $loop.Operands[0]
            if (-not $labelsByName.ContainsKey($target)) {
                throw "$($label.Name) loops to missing animation label $target."
            }
            $targetStart = $labelsByName[$target].Offset
            if ($targetStart -ge $label.Offset -and $targetStart -le $end) {
                $loopStart = $frameIndexByLabel[$target] - $startFrame
            } elseif ($targetStart -lt $label.Offset) {
                $loopStart = $frames.Count
                for ($index = $frameIndexByLabel[$target];
                    $index -lt $endFrame; $index++) {
                    $frames.Add((Convert-AssemblyAnimationFrame $frameNodes[$index]))
                }
            }
        }
        $result[$label.Name] = @{ Frames = @($frames); LoopStart = $loopStart }
    }
    $assemblyAnimationCache[$cacheKey] = $result
    return $result
}

function Write-GeneratedTable([object[]]$arguments) {
    if ($arguments.Count -lt 2) {
        throw "Write-GeneratedTable requires a destination path and rows; got " +
            "$($arguments.Count) argument(s) from " +
            "$($MyInvocation.ScriptName):$($MyInvocation.ScriptLineNumber)."
    }
    $path = [string]$arguments[0]
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($value in $arguments[1..($arguments.Count - 1)]) {
        foreach ($row in $value) {
            $rows.Add([string]$row)
        }
    }
    $parent = Split-Path $path -Parent
    if (-not [string]::IsNullOrEmpty($parent)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [IO.File]::WriteAllLines(
        $path,
        $rows,
        [Text.UTF8Encoding]::new($false))
}

function Write-GeneratedBytes([object[]]$arguments) {
    if ($arguments.Count -lt 2) {
        throw "Write-GeneratedBytes requires a destination path and bytes; got " +
            "$($arguments.Count) argument(s) from " +
            "$($MyInvocation.ScriptName):$($MyInvocation.ScriptLineNumber)."
    }
    $path = [string]$arguments[0]
    $bytes = [Collections.Generic.List[byte]]::new()
    foreach ($value in $arguments[1..($arguments.Count - 1)]) {
        foreach ($byte in $value) {
            $bytes.Add([byte]$byte)
        }
    }
    $parent = Split-Path $path -Parent
    if (-not [string]::IsNullOrEmpty($parent)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [IO.File]::WriteAllBytes($path, $bytes)
}

function Read-AssemblyDwTables(
    [string]$path,
    [string]$tableLabelPattern,
    [string]$entryPattern
) {
    $tables = @{}
    $aliases = [Collections.Generic.List[string]]::new()
    foreach ($node in Read-AssemblyNodes $path) {
        if ($node.Kind -eq 'Label') {
            if ($node.Name -match "^$tableLabelPattern`$") {
                if ($aliases.Count -gt 0 -and
                    $tables.ContainsKey($aliases[0])) {
                    $aliases.Clear()
                }
                $aliases.Add($node.Name)
            } else {
                $aliases.Clear()
            }
            continue
        }
        if ($aliases.Count -gt 0 -and $node.Kind -eq 'Data' -and
            $node.Name -ieq '.dw' -and
            $node.Operands.Count -gt 0 -and
            $node.Operands[0] -match "^$entryPattern") {
            foreach ($alias in $aliases) {
                if (-not $tables.ContainsKey($alias)) {
                    $tables[$alias] = [Collections.Generic.List[string]]::new()
                }
                $tables[$alias].Add($Matches[0])
            }
        }
    }
    return $tables
}

# Remove outputs that moved to another owner or were replaced by a broader
# generated table. Their obsolete paths must not survive a local re-import.
foreach ($legacyGeneratedAsset in @(
    'menu\new_game_intro.tsv',
    'menu\new_game_intro_sprites.tsv',
    'objects\maku_tree_cutscene.tsv',
    'objects\ralph_portal_event.tsv',
    'objects\linked_game_ghini.tsv',
    'objects\tokay_island_constants.tsv',
    'objects\tokay_island_texts.tsv',
    'objects\tokay_island_animations.tsv'
)) {
    $legacyGeneratedPath = Join-Path $destination $legacyGeneratedAsset
    if (Test-Path -LiteralPath $legacyGeneratedPath) {
        Remove-Item -LiteralPath $legacyGeneratedPath -Force
    }
}

# Remove the eight flat files produced by the original four-room prototype.
# All generated data now lives in purpose-specific subdirectories.
foreach ($legacyName in @(
    'gfx_tileset08.png', 'spr_link.png',
    'tilesetMappings06.bin', 'tilesetCollisions06.bin',
    'room0000.bin', 'room0001.bin', 'room0010.bin', 'room0011.bin'
)) {
    $legacyPath = Join-Path $destination $legacyName
    if (Test-Path -LiteralPath $legacyPath) {
        Remove-Item -LiteralPath $legacyPath -Force
    }
    if (Test-Path -LiteralPath "${legacyPath}.import") {
        Remove-Item -LiteralPath "${legacyPath}.import" -Force
    }
}

if (-not (Test-Path -LiteralPath $Rom)) {
    throw "ROM not found: $Rom"
}

$romBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Rom))
if ($romBytes.Length -ne 1048576) {
    throw "Expected the 1 MiB US Oracle of Ages ROM, got $($romBytes.Length) bytes."
}

$hash = (Get-FileHash -LiteralPath $Rom -Algorithm MD5).Hash
$cleanUsHash = "C4639CC61C049E5A085526BB6CAC03BB"
if ($hash -ne $cleanUsHash) {
    throw "ROM hash $hash is not the supported clean US Oracle of Ages hash $cleanUsHash."
}

function Copy-GeneratedFile([string]$relativeSource, [string]$relativeDestination) {
    $source = Join-Path $Disassembly $relativeSource
    $target = Join-Path $destination $relativeDestination
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Disassembly asset not found: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}
