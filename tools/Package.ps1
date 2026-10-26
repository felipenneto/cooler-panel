param([string]$ProjectRoot)
$ErrorActionPreference = 'Stop'
if (!$ProjectRoot) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$dist = Join-Path $ProjectRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$work = Join-Path $dist ('.work-' + [Guid]::NewGuid().ToString('N'))
$binary = Join-Path $work 'binary'
$source = Join-Path $work 'source'
New-Item -ItemType Directory -Path $binary,$source | Out-Null

& (Join-Path $ProjectRoot 'tools\Build.ps1') -ProjectRoot $ProjectRoot -OutputDirectory $binary
$executable = Join-Path $binary 'CoolerPanel.exe'
$test = Start-Process -FilePath $executable -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw "Testes falharam. Consulte $binary\resultado-teste.txt" }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versão do executável inválida.' }

# Explicit allowlist: never publish local logs, personal paths or old executables.
$publicFiles = @('README.md','LICENSE','.gitignore','.gitattributes','PUBLISHING.md','CHANGELOG.md')
$publicDirectories = @('src','assets','docs','examples','.github')
foreach ($name in $publicFiles) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination $source }
foreach ($name in $publicDirectories) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination $source -Recurse }
New-Item -ItemType Directory -Path (Join-Path $source 'tools') | Out-Null
foreach ($name in @('Build.ps1','Package.ps1','Build-Icon.py')) { Copy-Item -LiteralPath (Join-Path $ProjectRoot "tools\$name") -Destination (Join-Path $source 'tools') }

foreach ($name in @('README.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination $binary }
foreach ($name in @('docs','examples')) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination $binary -Recurse }
Remove-Item -LiteralPath (Join-Path $binary 'resultado-teste.txt')
$exeHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
"$exeHash  CoolerPanel.exe" | Set-Content -Encoding ASCII -LiteralPath (Join-Path $binary 'SHA256SUMS.txt')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(
    @{ Root=$binary; Name="CoolerPanel-$version-win-x64.zip" },
    @{ Root=$source; Name="CoolerPanel-$version-source.zip" }
)
$hashes = @()
foreach ($package in $packages) {
    $target = Join-Path $dist $package.Name
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    [IO.Compression.ZipFile]::CreateFromDirectory($package.Root,$target,[IO.Compression.CompressionLevel]::Optimal,$false)
    $hashes += (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $package.Name
    Write-Output $target
}
$hashes | Set-Content -Encoding ASCII -LiteralPath (Join-Path $dist 'SHA256SUMS.txt')

# Only remove this run's known staging directory, after validating its boundary.
$resolvedWork = (Resolve-Path -LiteralPath $work).Path
if (!$resolvedWork.StartsWith($dist + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Pasta temporária fora de dist.' }
Remove-Item -LiteralPath $resolvedWork -Recurse -Force
Write-Output 'Pacotes criados. Nenhum arquivo foi enviado ao GitHub.'
