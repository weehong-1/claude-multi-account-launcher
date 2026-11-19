# Builds the release files into artifacts/release:
#   claude-multi-account-launcher-<version>-x64.msi   per-user installer
#   claude-multi-account-launcher.exe                 portable single-file exe
# The version comes from Directory.Build.props.
#
#   pwsh tools/build-release.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $root 'artifacts\publish'
$release = Join-Path $root 'artifacts\release'

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" }
}

# installer/obj too: after a version bump WiX's incremental build otherwise skips linking the new MSI
Remove-Item $publish, $release, (Join-Path $root 'installer\obj'), (Join-Path $root 'installer\bin') -Recurse -Force -ErrorAction SilentlyContinue

Invoke-Checked 'tests' { dotnet test --project (Join-Path $root 'tests\ClaudeMultiAccountLauncher.Core.Tests') }
Invoke-Checked 'publish' { dotnet publish (Join-Path $root 'src\ClaudeMultiAccountLauncher') -c Release -o $publish }
Invoke-Checked 'installer' { dotnet build (Join-Path $root 'installer') -c Release -p:PublishDir=$publish\ -o $release }

Copy-Item (Join-Path $publish 'claude-multi-account-launcher.exe') $release
Get-ChildItem $release -File | Where-Object { $_.Extension -notin '.msi', '.exe' } | Remove-Item
Get-ChildItem $release -File | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
