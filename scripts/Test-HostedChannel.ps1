#Requires -Version 7.2
param(
    [string]$FeedUrl='https://raw.githubusercontent.com/ReshawnStar/Astera-Game-Launcher/main/channels/minecraft-squared.json',
    [Parameter(Mandatory=$true)][string]$SourceProfile,
    [string]$TestRoot=('D:/vale/StarsAllies-Storage/audits/astera-hosted-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$Executable='D:/vale/StarsAllies-Storage/builds/Astera/0.1.0/Astera.exe',
    [string]$ArchivePool='D:/vale/StarsAllies-Storage/immutable-test-archives'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($TestRoot)
if([IO.Path]::GetFileName($root) -notlike 'astera-hosted-*' -or (Test-Path -LiteralPath $root)){throw 'Use a new astera-hosted-* audit directory'}
foreach($path in @([IO.Path]::GetFullPath($SourceProfile),[IO.Path]::GetFullPath($ArchivePool),(Split-Path $root -Parent))){$at=$path;while($at){if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Redirected input/storage path refused'};$at=Split-Path $at -Parent}}
New-Item -ItemType Directory -Path $root|Out-Null
$profile=Join-Path $root 'profile';$cache=Join-Path $root 'cache'
foreach($path in @((Join-Path $profile 'mods'),(Join-Path $profile 'config'),(Join-Path $profile 'saves/protected'),$cache)){New-Item -ItemType Directory -Path $path -Force|Out-Null}
$meta=Get-Content -Raw -LiteralPath (Join-Path $SourceProfile 'minecraftinstance.json')|ConvertFrom-Json
@{name='Minecraft Squared';baseModLoader=@{name=$meta.baseModLoader.name;minecraftVersion=$meta.baseModLoader.minecraftVersion;forgeVersion=$meta.baseModLoader.forgeVersion}}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $profile 'minecraftinstance.json')
[IO.File]::WriteAllText((Join-Path $profile 'astera-test-fixture.txt'),'Synthetic self-test profile; never a live game.')
[IO.File]::WriteAllText((Join-Path $profile 'saves/protected/level.dat'),'Astera protected world sentinel')
[IO.File]::WriteAllText((Join-Path $profile 'config/stars-general-fixes.json'),'{"personal_setting":true}')
[IO.File]::WriteAllText((Join-Path $profile 'options.txt'),"music:0.4`nkey_key.forward:key.keyboard.up`n")
Add-Type -AssemblyName System.IO.Compression.FileSystem
$inputs=@();$before=@{C=(Get-PSDrive C).Free;D=(Get-PSDrive D).Free}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $SourceProfile 'mods') -File -Filter '*.jar'){
    if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Redirected mod refused'}
    $zip=[IO.Compression.ZipFile]::OpenRead($file.FullName);try{$reader=[IO.StreamReader]::new($zip.GetEntry('fabric.mod.json').Open());try{$mod=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$zip.Dispose()}
    if($mod.id -in @('stars_allies','stars_general_fixes','waystone_map_travel')){continue}
    $hash=(Get-FileHash -LiteralPath $file.FullName).Hash;$stored=Join-Path $ArchivePool ($hash.ToLowerInvariant()+'.jar')
    if(!(Test-Path -LiteralPath $stored)){Copy-Item -LiteralPath $file.FullName -Destination $stored}
    if((Get-FileHash -LiteralPath $stored).Hash -ne $hash){throw 'Archive-pool hash mismatch'}
    New-Item -ItemType HardLink -Path (Join-Path $profile ('mods/'+$file.Name)) -Target $stored|Out-Null
    $inputs+=@{Name=$file.Name;Sha256=$hash;Source=$file.FullName}
}
$inputs|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $root 'input-mods.json')
$process=Start-Process -FilePath $Executable -ArgumentList @('--audit-channel',('"'+$FeedUrl+'"'),('"'+$profile+'"'),('"'+$cache+'"')) -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(180000)){$process.Kill();throw 'Hosted audit timed out; preserve its evidence'}
$result=Get-Content -Raw -LiteralPath (Join-Path $root 'hosted-result.json')|ConvertFrom-Json
if($process.ExitCode -ne 0 -or !$result.Passed){$result|ConvertTo-Json -Depth 5;throw 'Hosted channel verification failed'}
foreach($file in $inputs){if((Get-FileHash -LiteralPath $file.Source).Hash -ne $file.Sha256){throw 'Live input changed during hosted test'}}
@{Passed=$true;Audit=$root;AppSha256=(Get-FileHash -LiteralPath $Executable).Hash;ForeignMods=$inputs.Count;FreePhysicalBefore=$before;FreePhysicalAfter=@{C=(Get-PSDrive C).Free;D=(Get-PSDrive D).Free};LiveSourceHashesUnchanged=$true;JarCodeExecuted=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $root 'receipt.json')
Write-Output ('HOSTED_CHANNEL_PASSED: '+$result.RealHttpsDownloads+' signed HTTPS payload downloads; installation, no-op repeat and restore passed in '+$root)
