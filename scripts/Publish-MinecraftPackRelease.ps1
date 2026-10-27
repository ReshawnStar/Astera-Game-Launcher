#Requires -Version 7.2
param(
 [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,70}$')][string]$ReleaseName,
 [Parameter(Mandatory)][string]$NotesFile,
 [string]$ProfileDirectory='',
 [string]$OutputRoot='D:/vale/StarsAllies-Storage/releases/Astera-packs',
 [switch]$Preview,
 [switch]$Publish
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$exe="D:/vale/StarsAllies-Storage/builds/Astera/$version/Astera.exe"
if(!(Test-Path -LiteralPath $NotesFile)){throw 'Player-facing release notes are required'}
$arguments=@{ReleaseName=$ReleaseName;OutputRoot=$OutputRoot;IncludeShaders=$true;Preview=[bool]$Preview}
if($ProfileDirectory){$arguments.ProfileDirectory=$ProfileDirectory}
& (Join-Path $PSScriptRoot 'Build-MinecraftPackZip.ps1') @arguments
if($Preview){return}
$zip=Join-Path $OutputRoot "$ReleaseName/MC2-$ReleaseName.zip"
$audit="D:/vale/StarsAllies-Storage/audits/astera-pack-publish-$ReleaseName"
if(Test-Path -LiteralPath $audit){throw 'A previous audit already exists for this release; use a fresh release name'}
$p=Start-Process -FilePath $exe -ArgumentList @('--audit-full-pack-release',('"'+$zip+'"'),('"'+$audit+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw "Isolated release installation failed; inspect $audit"}
& (Join-Path $PSScriptRoot 'Publish-FullPackUpdate.ps1') -Zip $zip -NotesFile $NotesFile -Executable $exe -TestReceipt (Join-Path $audit 'audit-result.json') -Publish:$Publish
