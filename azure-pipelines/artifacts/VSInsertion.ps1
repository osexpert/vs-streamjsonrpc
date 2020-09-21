# This artifact captures everything needed to insert into VS (NuGet packages, insertion metadata, etc.)

if ($IsMacOS -or $IsLinux) {
    # We only package up for insertions on Windows agents since they are where optprof can happen.
    Write-Verbose "Skipping VSInsertion artifact since we're not on Windows"
    return @{}
}

$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$config = 'Debug'
if ($env:BUILDCONFIGURATION) { $config = $env:BUILDCONFIGURATION }
$NuGetPackages = "$RepoRoot\bin\Packages\$config\NuGet"
$CoreXTPackages = "$RepoRoot\bin\Packages\$config\CoreXT"
if (-not (Test-Path $NuGetPackages)) { Write-Error "No NuGet packages found. Has a build been run?"; return @{} }
$ArtifactBasePath = "$RepoRoot\obj\_artifacts"
$ArtifactPath = "$ArtifactBasePath\VSInsertion"
if (-not (Test-Path $ArtifactPath)) { New-Item -ItemType Directory -Path $ArtifactPath | Out-Null }

$profilingInputs = [xml](Get-Content -Path "$PSScriptRoot\..\ProfilingInputs.props")
$profilingInputs.Project.ItemGroup.TestStore.Include = "vstsdrop:" + (& "$PSScriptRoot\..\variables\ProfilingInputsDropName.ps1")
$profilingInputs.Save("$ArtifactPath\ProfilingInputs.props")

$nbgv = & "$PSScriptRoot\..\Get-nbgv.ps1"
$version = $(& $nbgv get-version -p "$RepoRoot\src" -f json | ConvertFrom-Json).NuGetPackageVersion
& (& "$PSScriptRoot\..\Get-NuGetTool.ps1") pack "$PSScriptRoot\..\InsertionMetadataPackage.nuspec" -OutputDirectory $CoreXTPackages -BasePath $ArtifactPath -Version $version | Out-Null
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

@{
    "$NuGetPackages" = (Get-ChildItem "$NuGetPackages\*.nupkg");
    "$CoreXTPackages" = (Get-ChildItem "$CoreXTPackages\StreamJsonRpc.VSInsertionMetadata.$version.nupkg");
}
