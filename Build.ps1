param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Starship EVO',
    [string]$LoaderDir,
    [string]$DotnetPath = 'dotnet',
    [ValidateSet('netstandard2.0','netstandard2.1')][string]$Framework = 'netstandard2.1'
)
$ErrorActionPreference = 'Stop'
if (!$LoaderDir) { $LoaderDir = Join-Path $GameDir 'BepInEx/core' }
$root = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $root '.build-home'
$env:NUGET_PACKAGES = Join-Path $root '.packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location $root
try {
    & $DotnetPath run --project tests/PolicyTests.csproj -c Release
    if ($LASTEXITCODE) { throw 'Offline policy tests failed.' }
    & $DotnetPath build src/InfiniteRotator.csproj -c Release "-p:GameDir=$GameDir" "-p:LoaderDir=$LoaderDir" "-p:TargetFramework=$Framework"
    if ($LASTEXITCODE) { throw 'Plugin build failed.' }
    $manifest = Get-Content -LiteralPath 'sevo.mod.json' -Raw | ConvertFrom-Json
    $managed = $Framework -eq 'netstandard2.1'
    $archiveName = if ($managed) { $manifest.assetName } else { "InfiniteRotator-v$($manifest.version)-legacy.zip" }
    $package = Join-Path $root ('dist/' + [IO.Path]::GetFileNameWithoutExtension($archiveName))
    $pluginPath = if ($managed) { $manifest.payloadRoot } else { 'BepInEx/plugins/InfiniteRotator' }
    $plugin = Join-Path $package $pluginPath
    New-Item -ItemType Directory -Force -Path $plugin | Out-Null
    Copy-Item -LiteralPath "src/bin/Release/$Framework/InfiniteRotator.dll" -Destination $plugin
    Copy-Item -LiteralPath 'README.md','LICENSE','COMPATIBILITY.md' -Destination $package
    if ($managed) { Copy-Item -LiteralPath 'sevo.mod.json' -Destination $package }
    $hash = (Get-FileHash -LiteralPath (Join-Path $plugin 'InfiniteRotator.dll')).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $package 'SHA256SUMS.txt'), "$hash  $pluginPath/InfiniteRotator.dll`n", [Text.UTF8Encoding]::new($false))
    $zip = Join-Path $root "dist/$archiveName"
    Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
    Write-Output "Package: $zip"
} finally { Pop-Location }
