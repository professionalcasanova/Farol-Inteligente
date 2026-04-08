$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot '..\NuGet.Config'
$resolvedPath = [System.IO.Path]::GetFullPath($configPath)

if (-not (Test-Path $resolvedPath)) {
    throw "NuGet.Config nao encontrado em $resolvedPath"
}

[xml]$config = Get-Content $resolvedPath
$packageSources = @($config.configuration.packageSources.add)

foreach ($source in $packageSources) {
    $key = [string]$source.key
    $value = [string]$source.value

    $hasUserProfileSource = $value -like '*%USERPROFILE%*'
    $hasNuGetCacheSource = $value -like '*.nuget\packages*'
    $hasDriveLetterSource = $value -match '^[A-Za-z]:\\'
    $hasUncSource = $value -match '^\\\\'

    if ($hasUserProfileSource -or $hasNuGetCacheSource -or $hasDriveLetterSource -or $hasUncSource) {
        throw "NuGet.Config contem source local invalido ('$key' => '$value'). Use apenas feeds remotos no arquivo versionado."
    }
}

Write-Output "NuGet.Config validado com sucesso."
