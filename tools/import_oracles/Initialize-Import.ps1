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
    if ($ready -ne "READY`t1") {
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

function Read-AssemblyLabelBlock([string]$path, [string]$label) {
    $fullPath = Resolve-ImportReadPath $path
    if ([IO.Path]::GetExtension($fullPath) -ine '.s') {
        throw "Assembly label blocks require an .s source: $fullPath"
    }
    return Invoke-AssemblySourceHost `
        $assemblySourceHost 'LABEL' "$fullPath`0$label"
}

function Resolve-AssemblySourceTextPath([string]$source) {
    foreach ($entry in $assemblyTextCache.GetEnumerator()) {
        if ([object]::ReferenceEquals($entry.Value, $source)) {
            return $entry.Key
        }
    }
    return $null
}

# Remove cutscene outputs from their former menu/object categories. They are
# generated again below under cutscenes, which owns their runtime behavior.
foreach ($legacyCutsceneAsset in @(
    'menu\new_game_intro.tsv',
    'menu\new_game_intro_sprites.tsv',
    'objects\maku_tree_cutscene.tsv',
    'objects\ralph_portal_event.tsv'
)) {
    $legacyCutscenePath = Join-Path $destination $legacyCutsceneAsset
    if (Test-Path -LiteralPath $legacyCutscenePath) {
        Remove-Item -LiteralPath $legacyCutscenePath -Force
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
