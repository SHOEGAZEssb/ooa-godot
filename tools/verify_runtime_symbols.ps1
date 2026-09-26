param(
    [string]$Disassembly = (Join-Path $PSScriptRoot '..\..\oracles-disasm')
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet run --project tools/OracleImporter.Tests -- --verify-runtime-symbols $Disassembly
    if ($LASTEXITCODE -ne 0) {
        throw 'Runtime source-symbol verification failed.'
    }
}
finally {
    Pop-Location
}
