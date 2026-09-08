# Publishes MTM as a single .exe.
#
#   pwsh tools/publish.ps1              # framework-dependent  (~0.6 MB, needs .NET 9 Desktop Runtime)
#   pwsh tools/publish.ps1 -SelfContained   # standalone       (~60 MB, no runtime needed)
#
# Output goes to  dist/

param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$proj = Join-Path $root 'src\MultiTargetMonitor'
$out  = Join-Path $root 'dist'

$sc = $SelfContained.IsPresent
Write-Host "Publishing $(if ($sc) {'self-contained'} else {'framework-dependent'}) -> $out"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

$args = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '--self-contained', $sc, '-o', $out)
if ($sc) { $args += '-p:EnableCompressionInSingleFile=true' }   # compression needs self-contained
dotnet @args

$exe = Join-Path $out 'MultiTargetMonitor.exe'
"{0}  ({1:N1} MB)" -f $exe, ((Get-Item $exe).Length / 1MB)
