[CmdletBinding()]
param([string]$Version='', [string]$EngineSourceRoot='', [string]$ClientSourceRoot='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $cfg=Get-Content release-config.json -Raw | ConvertFrom-Json
    function Invoke-DotNet([string[]]$Arguments) { & dotnet @Arguments; if ($LASTEXITCODE) { throw "dotnet failed: $($Arguments[0])" } }
    function Get-PinnedSource([string]$Repository,[string]$RefFile,[string]$Destination) {
        $sha=(Get-Content $RefFile -Raw).Trim()
        if ($sha -notmatch '^[a-f0-9]{40}$') { throw 'Compatibility source must be pinned to a full commit SHA' }
        if (-not (Test-Path "$Destination/.git")) { & git clone --no-checkout "https://github.com/$Repository.git" $Destination | Out-Host; if ($LASTEXITCODE) { throw 'Source clone failed' } }
        & git -C $Destination fetch origin $sha | Out-Host
        if ($LASTEXITCODE) { throw 'Pinned source unavailable' }
        & git -C $Destination checkout --detach $sha | Out-Host
        if ($LASTEXITCODE) { throw 'Pinned source checkout failed' }
        & git -C $Destination submodule update --init --recursive | Out-Host
        if ($LASTEXITCODE) { throw 'Pinned source dependencies unavailable' }
        return (Resolve-Path $Destination).Path
    }
    $buildArgs=@('build',$cfg.project,'-c','Release','-p:UseSharedCompilation=false','--nologo')
    if ($Version) { $buildArgs+="-p:Version=$Version" }
    Invoke-DotNet $buildArgs
    if ($Version) {
        $binary=Join-Path $root "$($cfg.build)/$($cfg.internal_name).dll"
        if ([Reflection.AssemblyName]::GetAssemblyName($binary).Version -ne [version]$Version) { throw 'Actual DLL version differs from the planned release' }
    }
    if ($cfg.internal_name -eq 'Aetherworks') { Invoke-DotNet @('run','--project','tests/Aetherworks.Tests.csproj','-c','Release') }
    elseif ($cfg.internal_name -eq 'Accord') {
        if (-not $EngineSourceRoot) { $EngineSourceRoot=Get-PinnedSource 'kaldigo/WrathCombo' 'engine-validation.ref' '.engine' }
        $engineProject=Join-Path $EngineSourceRoot 'WrathCombo/WrathCombo.csproj'
        Invoke-DotNet @('build',$engineProject,'-c','Release','-p:UseSharedCompilation=false','--nologo')
        $dll=Join-Path $EngineSourceRoot 'WrathCombo/bin/WrathCombo/Release/WrathCombo.dll'
        Invoke-DotNet @('run','--project','tests/Accord.Tests.csproj','-c','Release','-p:UseSharedCompilation=false',"-p:EngineSourceRoot=$EngineSourceRoot",'--',$dll)
    }
    elseif ($cfg.internal_name -eq 'WrathCombo') {
        if (-not $ClientSourceRoot) { $ClientSourceRoot=Get-PinnedSource 'kaldigo/FFXIVAccord' 'client-validation.ref' '.client-validation' }
        $tests=Join-Path $ClientSourceRoot 'tests/Accord.Tests.csproj'
        $dll=Join-Path $root 'WrathCombo/bin/WrathCombo/Release/WrathCombo.dll'
        Invoke-DotNet @('run','--project',$tests,'-c','Release','-p:UseSharedCompilation=false',"-p:EngineSourceRoot=$root",'--',$dll)
    }
} finally { Pop-Location }
