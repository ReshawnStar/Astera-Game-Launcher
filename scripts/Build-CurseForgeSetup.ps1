#Requires -Version 7.2
param(
 [Parameter(Mandatory)][string]$PackZip,
 [string]$OutputDirectory=''
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$inputZip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackZip))
try{$reader=[IO.StreamReader]::new($inputZip.GetEntry('mc2-release.json').Open());try{$pack=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$inputZip.Dispose()}
if($pack.PackId -ne 'astera-minecraft-squared' -or $pack.Minecraft -notmatch '^[0-9.]+$' -or $pack.Loader -notmatch '^[0-9.]+$'){throw 'Choose the tested Astera Minecraft pack ZIP'}
if(!$OutputDirectory){$OutputDirectory=Join-Path (Split-Path ([IO.Path]::GetFullPath($PackZip)) -Parent) 'setup'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a fresh setup output directory'}
New-Item -ItemType Directory -Path $out|Out-Null
$manifest=[ordered]@{minecraft=[ordered]@{version=$pack.Minecraft;modLoaders=@([ordered]@{id=('fabric-'+$pack.Loader);primary=$true})};manifestType='minecraftModpack';manifestVersion=1;name='Minecraft Squared';version='Astera Setup';author='Star';files=@();overrides='overrides'}
$path=Join-Path $out 'Minecraft2-Setup.zip'
$zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
try{$entry=$zip.CreateEntry('manifest.json');$writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false));try{$writer.Write(($manifest|ConvertTo-Json -Depth 6))}finally{$writer.Dispose()};$zip.CreateEntry('overrides/')|Out-Null}finally{$zip.Dispose()}
$zip=[IO.Compression.ZipFile]::OpenRead($path)
try{$reader=[IO.StreamReader]::new($zip.GetEntry('manifest.json').Open());try{$read=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()};if($read.manifestType -ne 'minecraftModpack' -or $read.files.Count -ne 0 -or $read.minecraft.version -ne $pack.Minecraft -or $read.minecraft.modLoaders[0].id -ne ('fabric-'+$pack.Loader) -or $zip.Entries.Count -ne 2){throw 'Setup ZIP validation failed'}}finally{$zip.Dispose()}
$instructions=@"
Minecraft² first-time setup

1. In CurseForge, use Import / Import Profile ZIP and choose Minecraft2-Setup.zip.
2. Wait for CurseForge to create Minecraft Squared with Minecraft $($pack.Minecraft) and Fabric $($pack.Loader).
3. In Astera, link the newly created profile folder. Use Astera 0.1.18 or newer.
4. Astera checks the selected profile. Choose Update, let the download/verification finish, then confirm installation with Minecraft closed.
5. Once installation completes, launch the profile through CurseForge.

This small ZIP only creates the base profile. Astera supplies the mods, resource packs and shaders.
It contains no worlds, personal settings or mod JARs. Import is left for the tester to verify.
"@
[IO.File]::WriteAllText((Join-Path $out 'README.txt'),$instructions,[Text.UTF8Encoding]::new($false))
@{Passed=$true;Zip=$path;Sha256=(Get-FileHash -LiteralPath $path).Hash;Bytes=(Get-Item -LiteralPath $path).Length;Minecraft=$pack.Minecraft;Fabric=$pack.Loader;Files=0;CurseForgeImportVerified=$false;LiveProfilesChanged=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'receipt.json')
Write-Output $path
