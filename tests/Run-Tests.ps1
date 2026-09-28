$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildRoot = Join-Path $repoRoot '.build'
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework 4.x C# compiler was not found. Run on Windows with that component installed.'
}
New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
$sourcePath = Join-Path $repoRoot 'src\fiRenkoZoneReversal.cs'
$source = [IO.File]::ReadAllText($sourcePath)
$marker = '    // BEGIN TESTABLE ENGINE'
$start = $source.IndexOf($marker, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'The engine marker was not found in the source.' }
$engine = "using System;`nusing System.Collections.Generic;`nnamespace NinjaTrader.NinjaScript.Indicators.FreeIndicators`n{`n" + $source.Substring($start)
$enginePath = Join-Path $buildRoot 'ZoneEngine.cs'
[IO.File]::WriteAllText($enginePath, $engine, [Text.UTF8Encoding]::new($false))
$engineExe = Join-Path $buildRoot 'ZoneTests.exe'
& $compiler /nologo /target:exe "/out:$engineExe" $enginePath (Join-Path $PSScriptRoot 'ZoneTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Engine test compilation failed.' }
& $engineExe
if ($LASTEXITCODE -ne 0) { throw 'Engine tests failed.' }
$references = @('System.ComponentModel.DataAnnotations.dll','WPF\PresentationCore.dll',
    'WPF\PresentationFramework.dll','WPF\WindowsBase.dll','System.Xaml.dll')
$adapterExe = Join-Path $buildRoot 'ZoneAdapterTests.exe'
$compilerArgs = @('/nologo','/target:exe',"/out:$adapterExe")
foreach ($reference in $references) {
    $compilerArgs += '/reference:' + (Join-Path $frameworkRoot $reference)
}
$compilerArgs += @($sourcePath, (Join-Path $PSScriptRoot 'ZoneAdapterTests.cs'))
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Adapter test compilation failed.' }
& $adapterExe
if ($LASTEXITCODE -ne 0) { throw 'Adapter tests failed.' }
Write-Output 'All tests passed. No orders were submitted and no NT8 connection was opened.'
