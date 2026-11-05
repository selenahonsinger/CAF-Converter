[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repoRoot 'src\CafConverter\CafConverter.csproj'
$tests = Join-Path $repoRoot 'tests\CafConverter.Tests\CafConverter.Tests.csproj'
$toolDir = Join-Path $repoRoot 'src\CafConverter\Resources\Tools\win-x64'
$publishDir = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\CAFConverter-win-x64'))
$cliHome = Join-Path $repoRoot '.cli-home'
$nugetPackages = Join-Path $repoRoot '.nuget-packages'
$localDotnet = Join-Path $repoRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }

$env:DOTNET_CLI_HOME = $cliHome
$env:NUGET_PACKAGES = $nugetPackages
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host "Using .NET SDK: $(& $dotnet --version)"
if (-not (& $dotnet --list-sdks | Select-String '^10\.')) { throw '.NET 10 SDK was not found. Install .NET 10 SDK and try again.' }

foreach ($tool in @('ffmpeg.exe', 'ffprobe.exe')) {
    $path = Join-Path $toolDir $tool
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 1MB) {
        throw "A valid $tool is missing. Follow Resources\Tools\win-x64\README.txt before publishing."
    }
}

if (-not $publishDir.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish directory is outside the repository. Stopped.' }
if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host 'Restoring dependencies...'
& $dotnet restore $tests
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

Write-Host 'Running tests...'
& $dotnet test $tests -c Release --no-restore --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

Write-Host 'Publishing self-contained single-file win-x64 build...'
& $dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore --disable-build-servers -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $publishDir 'README.txt') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $publishDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'FFMPEG_LICENSE.txt') -Destination $publishDir -Force

$exe = Join-Path $publishDir 'CAFConverter.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'CAFConverter.exe was not found after publishing.' }
Write-Host "Publish completed: $publishDir"
