param([string]$ProjectRoot, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (!$ProjectRoot) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Compilador do .NET Framework não encontrado.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'app' }
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter *.cs | Select-Object -ExpandProperty FullName)
$appIcon = Join-Path $projectRoot 'assets\cooler-panel.ico'
& $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/win32icon:$appIcon" "/out:$outputDirectory\CoolerPanel.exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o protótipo.' }
Write-Output "Criado: $outputDirectory\CoolerPanel.exe"
