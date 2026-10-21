$ErrorActionPreference = 'Stop'

$workspacePath = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $workspacePath
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (!$vsPath) {
    throw 'Visual Studio with MSBuild is required.'
}

Import-Module (Join-Path $vsPath 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64'
& msbuild -restore src/Files.App/Files.App.csproj -p:Configuration=Debug -p:Platform=x64 -p:PublishAot=false -v:quiet -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$outputPath = Join-Path $workspacePath 'src/Files.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64'
$layoutPath = Join-Path $outputPath 'VSCodeAppX'
[xml]$recipe = Get-Content -LiteralPath (Join-Path $outputPath 'Files.App.build.appxrecipe') -Raw
$files = $recipe.SelectNodes("//*[local-name()='AppXManifest' or local-name()='AppxPackagedFile']")
if (!$files.Count) {
    throw 'The build did not produce a package deployment recipe.'
}

# Stage the same payload and resource paths used by Visual Studio deployment.
$layoutPrefix = [IO.Path]::GetFullPath($layoutPath) + [IO.Path]::DirectorySeparatorChar
$payload = foreach ($file in $files) {
    $source = [Uri]::UnescapeDataString($file.Include)
    $destination = [IO.Path]::GetFullPath((Join-Path $layoutPath ([Uri]::UnescapeDataString($file.PackagePath))))
    if (!$destination.StartsWith($layoutPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package path is outside the debug layout: $destination"
    }
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Package file is missing: $source"
    }
    [pscustomobject]@{ Source = $source; Destination = $destination }
}

if (Test-Path -LiteralPath $layoutPath) {
    Remove-Item -LiteralPath $layoutPath -Recurse -Force
}
foreach ($file in $payload) {
    $null = [IO.Directory]::CreateDirectory((Split-Path $file.Destination -Parent))
    Copy-Item -LiteralPath $file.Source -Destination $file.Destination -Force
}
Write-Output "Debug package ready: $layoutPath"
