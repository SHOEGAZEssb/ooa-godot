param(
    [string]$Disassembly = (Join-Path $PSScriptRoot '..\external\oracles-disasm')
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet build tools/OracleImporter.Tests -t:Rebuild -p:Optimize=true --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Runtime source-symbol test build failed.'
    }
    dotnet run --project tools/OracleImporter.Tests --no-build -- --verify-runtime-symbols $Disassembly
    if ($LASTEXITCODE -ne 0) {
        throw 'Runtime source-symbol verification failed.'
    }
}
finally {
    Pop-Location
}
