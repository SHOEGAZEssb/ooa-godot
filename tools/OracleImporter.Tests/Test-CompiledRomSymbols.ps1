$ErrorActionPreference='Stop'
foreach($spec in @(
    @('../import_oracles.ps1','Read-CompiledRomSymbols'),
    @('../import_oracles/Shared/Import-InventoryPresentation.ps1','Read-MenuRomBytes')
)){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $spec[0]),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw $errors[0]}
    $definition=$ast.Find({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $spec[1]},$true)
    if($null -eq $definition){throw "Missing compiled-symbol fixture function $($spec[1])."}
    . ([scriptblock]::Create($definition.Extent.Text))
}
$temporary=[IO.Path]::GetTempFileName()
try{
    [IO.File]::WriteAllLines($temporary,@('01:4000 body','02:4000 interaction','01:4010 state1','02:4010 state1',
        '01:4010 alias','01:4010 alias','01:4020 State1','01:4030 duplicate','01:4031 duplicate'))
    $symbols=Read-CompiledRomSymbols $temporary
    $bytes=[byte[]]::new(0xc000);$bytes[0x4010]=0x11;$bytes[0x8010]=0x22;$bytes[0x4020]=0x33
    $pack=@{Name='fixture';Symbols=$symbols;Bytes=$bytes}
    if((Read-MenuRomBytes $pack 'state1' 1 'body')[0] -ne 0x11 -or
        (Read-MenuRomBytes $pack 'state1' 1 'interaction')[0] -ne 0x22 -or
        (Read-MenuRomBytes $pack 'State1' 1)[0] -ne 0x33 -or
        (Read-MenuRomBytes $pack 'bank:01:state1' 1)[0] -ne 0x11 -or
        (Read-MenuRomBytes $pack 'alias' 1)[0] -ne 0x11){throw 'Compiled native label bank/case/alias identity was lost.'}
    foreach($request in @(@('state1',''),@('duplicate','body'),@('state1','missing-owner'),@('missing-label','body'))){
        $rejected=$false
        try{$null=Read-MenuRomBytes $pack $request[0] 1 $request[1]}
        catch{$rejected=$_.Exception.Message.Contains('compiled symbol') -or $_.Exception.Message.Contains('bank owner')}
        if(-not $rejected){throw "Compiled-symbol fixture must reject ambiguous/missing $($request[0]) in '$($request[1])'."}
    }
    Write-Host 'Compiled-symbol fixtures passed (bank scope, aliases, case, ambiguity and missing owner/label).'
}finally{[IO.File]::Delete($temporary)}
