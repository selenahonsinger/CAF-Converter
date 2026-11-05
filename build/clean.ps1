[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$targets = @(
    (Join-Path $repoRoot 'src\CafConverter\bin'),
    (Join-Path $repoRoot 'src\CafConverter\obj'),
    (Join-Path $repoRoot 'tests\CafConverter.Tests\bin'),
    (Join-Path $repoRoot 'tests\CafConverter.Tests\obj'),
    (Join-Path $repoRoot 'artifacts\CAFConverter-win-x64')
)
foreach ($target in $targets) {
    $resolved = [IO.Path]::GetFullPath($target)
    if (-not $resolved.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing to clean a path outside the repository: $resolved" }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
Write-Host 'Build and publish output cleaned.'
