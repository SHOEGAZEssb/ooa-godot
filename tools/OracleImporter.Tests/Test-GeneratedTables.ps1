$ErrorActionPreference = 'Stop'
$initialize = Join-Path $PSScriptRoot '../import_oracles/Initialize-Import.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($initialize, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Could not parse the shared generated-table writer.' }
$writer = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Write-GeneratedTable'
}, $true)
if ($null -eq $writer) { throw 'Shared generated-table writer was not found.' }
# Exercise the real helper without starting a source host or requiring a ROM.
. ([ScriptBlock]::Create($writer.Extent.Text))
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ooa-table-test-' + [Guid]::NewGuid().ToString('N'))
try {
    $path = Join-Path $temporary 'nested/table.tsv'
    Write-GeneratedTable($path, @("# header`twide", "a`t`t$([char]0x03a9)", "b`t1"))
    $actual = [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
    # Literal independent UTF-8 fixture: tabs, an empty cell, Omega, LF and no BOM.
    $expected = 'IyBoZWFkZXIJd2lkZQphCQnOqQpiCTEK'
    if ($actual -cne $expected) { throw "Generated table encoding differs: $actual" }
    Write-GeneratedTable($path, @())
    if ([IO.File]::ReadAllBytes($path).Length -ne 0) { throw 'An empty table contains a newline or BOM.' }
    Write-Host 'Generated-table fixtures passed (UTF-8, LF, empty cells, empty tables and parent creation).'
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporary)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary table fixture path: $resolved"
    }
    if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved, $true) }
}
