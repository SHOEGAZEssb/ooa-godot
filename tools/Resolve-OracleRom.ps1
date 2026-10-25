# Shared by import, import verification, validation and CI. Dot-source to use.
$oracleRomToolsRoot = $PSScriptRoot

function Get-OracleRomBuildPins {
    $pinsPath = Join-Path $oracleRomToolsRoot 'rom-build/ages.json'
    # Release rebuilds may install this resolver into a tag preceding the rename.
    if (-not [IO.File]::Exists($pinsPath)) {
        $pinsPath = Join-Path $oracleRomToolsRoot 'rom-build/pins.json'
    }
    return Get-Content -LiteralPath $pinsPath -Raw |
        ConvertFrom-Json
}

function Test-OracleRomImage([string]$Path, $Pins) {
    return [IO.File]::Exists($Path) -and
        ([IO.FileInfo]::new($Path).Length -eq $Pins.romLength) -and
        ((Get-FileHash -LiteralPath $Path -Algorithm MD5).Hash -eq $Pins.romMd5)
}

function Invoke-OracleRomBuild([string]$Stage, $Pins) {
    $bash = $env:OOA_ROM_BUILD_BASH
    if (-not $bash) {
        if ($env:OS -eq 'Windows_NT') {
            $candidates = @()
            if ($env:MSYS2_ROOT) { $candidates += Join-Path $env:MSYS2_ROOT 'usr/bin/bash.exe' }
            $candidates += @('C:/msys64/usr/bin/bash.exe', 'C:/msys32/usr/bin/bash.exe')
            $bash = $candidates | Where-Object { [IO.File]::Exists($_) } | Select-Object -First 1
        }
        else {
            $command = Get-Command bash -CommandType Application -ErrorAction SilentlyContinue
            if ($command) { $bash = $command.Source }
        }
    }
    if (-not $bash -or -not [IO.File]::Exists($bash)) {
        throw 'ROM build needs Bash. On Windows install MSYS2 with git, make, python, python-yaml, curl and unzip; set OOA_ROM_BUILD_BASH to its usr/bin/bash.exe for a custom installation. Alternatively provide -Rom PATH.'
    }
    $buildScript = (Join-Path $oracleRomToolsRoot 'rom-build/build.sh').Replace('\', '/')
    # Arguments are passed directly, never interpolated into shell code.
    $log = Join-Path $Stage 'build.log'
    Write-Host "ROM build log: $log"
    $writer = [IO.StreamWriter]::new($log, $false, [Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell wraps native stderr as nonterminating errors.
        # Keep it in the log and decide success from the process exit code.
        $ErrorActionPreference = 'Continue'
        & $bash --login $buildScript $Stage $Pins.repository $Pins.revision `
            $Pins.wlaVersion $Pins.windowsArchiveSha256 $Pins.wlaLinkVersion 2>&1 |
            ForEach-Object { $writer.WriteLine($_.ToString()) }
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
        $writer.Dispose()
    }
    if ($null -eq $buildExitCode -or $buildExitCode -ne 0) {
        throw "Oracle of Ages ROM build exited with code $buildExitCode. Diagnostics: $log"
    }
}

function Resolve-OracleRom {
    param(
        [string]$Rom,
        [string]$CacheDirectory = (Join-Path $oracleRomToolsRoot '../.cache/oracle-rom'),
        [ValidateRange(0, 86400)][int]$LockTimeoutSeconds = 600
    )
    $pins = Get-OracleRomBuildPins
    if ($Rom) {
        $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Rom)
        if (-not (Test-OracleRomImage $path $pins)) {
            throw "Unsupported or missing ROM '$path'; expected $($pins.romLength) bytes, MD5 $($pins.romMd5)."
        }
        return $path
    }

    $cacheRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($CacheDirectory)
    $pinBytes = [Text.Encoding]::UTF8.GetBytes(($pins | ConvertTo-Json -Compress))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $pinHash = [BitConverter]::ToString($sha256.ComputeHash($pinBytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha256.Dispose() }
    # Every pin participates; keep the path short for Windows source checkouts.
    $cache = Join-Path $cacheRoot ("pins-" + $pinHash.Substring(0, 24))
    [void][IO.Directory]::CreateDirectory($cache)
    # Separate pin caches may be initialized concurrently.
    $ignore = [IO.File]::Open((Join-Path $cacheRoot '.gdignore'),
        [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
    $ignore.Dispose()
    $path = Join-Path $cache 'ages.gbc'
    # Serialize builders, including verification workers and independent imports.
    # Check the ROM only under the lock so another process cannot replace it mid-read.
    $lock = $null
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        while ($null -eq $lock) {
            try {
                $lock = [IO.File]::Open((Join-Path $cache 'build.lock'),
                    [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            }
            catch [IO.IOException] {
                if ($LockTimeoutSeconds -gt 0 -and $timer.Elapsed.TotalSeconds -ge $LockTimeoutSeconds) {
                    throw "Timed out waiting for ROM build cache lock: $cache"
                }
                Start-Sleep -Milliseconds 200
            }
        }
        if (Test-OracleRomImage $path $pins) {
            Write-Host "Using verified cached US ROM: $path"
            return $path
        }
        $stage = Join-Path $cache "build-$([Guid]::NewGuid().ToString('N'))"
        [void][IO.Directory]::CreateDirectory($stage)
        Write-Host "Building clean US ROM from disassembly $($pins.revision)."
        Invoke-OracleRomBuild $stage $pins
        $built = Join-Path $stage 'disassembly/ages.gbc'
        if (-not (Test-OracleRomImage $built $pins)) {
            throw "Built ROM does not match clean US MD5 $($pins.romMd5). Build retained at $stage"
        }
        Move-Item -LiteralPath $built -Destination $path -Force
        # The stage is a fresh child of this exact cache, never a user source tree.
        $resolvedStage = [IO.Path]::GetFullPath($stage)
        $resolvedCache = [IO.Path]::GetFullPath($cache).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedStage.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove ROM build path outside cache: $resolvedStage"
        }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        Write-Host "Verified clean US ROM: $($pins.romMd5)"
        return $path
    }
    finally {
        if ($null -ne $lock) { $lock.Dispose() }
    }
}
