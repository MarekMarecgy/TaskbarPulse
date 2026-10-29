param([string]$OutputPath = "TaskbarPulse.exe")
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Wymagany .NET Framework 4.8 (Windows x64).' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\TaskbarPulse.ico "/out:$OutputPath" /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll src\TaskbarPulse.cs
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodla sie.' }
Write-Host "Gotowe: $OutputPath"
