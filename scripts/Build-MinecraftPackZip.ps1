[CmdletBinding()]
param(
    [string]$ProfileDirectory = 'C:\Users\PHANTOM GAMING\curseforge\minecraft\Instances\But Again',
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,70}$')][string]$ReleaseName,
    [string]$OutputRoot = 'D:\vale\StarsAllies-Storage\releases\Astera-packs',
    [string]$PreviousReleaseZip,
    [switch]$IncludeShaders,
    [switch]$Preview
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = [IO.Path]::GetFullPath($ProfileDirectory).TrimEnd('\')
$destination = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$code = Split-Path $PSScriptRoot -Parent
$rules = Get-Content -Raw -LiteralPath (Join-Path $code 'full-pack-rules.json') | ConvertFrom-Json
$utf8 = New-Object Text.UTF8Encoding($false)
function Assert-Plain([string]$Path) {
    if ((Test-Path -LiteralPath $Path) -and ((Get-Item -LiteralPath $Path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Redirected path refused: $Path" }
}
function Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Read-Entry($Zip,[string]$Name) {
    $entry=$Zip.GetEntry($Name)
    if (!$entry -or $entry.Length -gt 16777216) { throw "Missing/oversized metadata: $Name" }
    $reader=New-Object IO.StreamReader($entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function Read-Mod([string]$Path) {
    $zip=[IO.Compression.ZipFile]::OpenRead($Path)
    try { (Read-Entry $zip 'fabric.mod.json') | ConvertFrom-Json } finally { $zip.Dispose() }
}
Assert-Plain $source
if (!(Test-Path -LiteralPath $source -PathType Container)) { throw 'Profile directory not found.' }
$meta=Get-Content -Raw -LiteralPath (Join-Path $source 'minecraftinstance.json') | ConvertFrom-Json
if (!$meta.baseModLoader.minecraftVersion -or $meta.baseModLoader.name -notlike 'fabric-*') { throw 'Publisher requires a CurseForge Fabric profile.' }
$files=New-Object 'Collections.Generic.List[object]'
$ids=@{}
function Add-ReleaseFile([IO.FileInfo]$File,[string]$Kind,[string]$Policy,$Mod) {
    Assert-Plain $File.FullName
    $relative=$File.FullName.Substring($source.Length+1).Replace('\','/')
    if ($relative -match '[\x00-\x1f]') { throw 'Control character in filename.' }
    $row=[ordered]@{Path=$relative;Hash=(Sha $File.FullName);Size=$File.Length;Kind=$Kind;Policy=$Policy;Id=$null;Version=$null;Family=$null}
    if ($Mod) {
        if (!$Mod.id -or !$Mod.version -or $ids.ContainsKey([string]$Mod.id)) { throw "Missing or duplicate Fabric mod identity: $relative" }
        $ids[[string]$Mod.id]=$true;$row.Id=[string]$Mod.id;$row.Version=[string]$Mod.version
    }
    $files.Add([pscustomobject]$row)
}
$mods=Join-Path $source 'mods';Assert-Plain $mods
foreach ($file in Get-ChildItem -LiteralPath $mods -File -Filter '*.jar' | Sort-Object Name) { Add-ReleaseFile $file 'mod' 'replace' (Read-Mod $file.FullName) }
if (!$ids.ContainsKey('stars_allies') -or !$ids.ContainsKey('stars_general_fixes')) { throw 'Publish from the tested full profile containing both Stars components.' }
foreach ($folder in @('resourcepacks','shaderpacks')) {
    if ($folder -eq 'shaderpacks' -and !$IncludeShaders) { continue }
    $path=Join-Path $source $folder;Assert-Plain $path
    if (Test-Path -LiteralPath $path) { foreach($file in Get-ChildItem -LiteralPath $path -File -Filter '*.zip' | Sort-Object Name) { Add-ReleaseFile $file 'asset' $(if($folder -eq 'shaderpacks'){'seed'}else{'replace'}) $null } }
}
foreach ($folder in @('config','defaultconfigs')) {
    $path=Join-Path $source $folder;Assert-Plain $path
    if (!(Test-Path -LiteralPath $path)) { continue }
    # Enumerate only approved roots, never recurse through unknown/cache/private folders.
    $candidates=@(Get-ChildItem -LiteralPath $path -File | Where-Object { $_.Name -in $rules.ConfigNames -or $_.Name -match '-(common|server)\.(toml|json|json5)$' })
    if($folder -eq 'defaultconfigs') { $candidates=@(Get-ChildItem -LiteralPath $path -File) }
    foreach($relative in $rules.ConfigPaths){
        if($relative -notmatch '^[a-zA-Z0-9_.-]+(/[a-zA-Z0-9_.-]+)*\.(json|json5|toml|properties|cfg)$' -or $relative -match '(^|/)\.\.?(/|$)'){throw 'Unsafe explicit configuration path'}
        $candidate=$path;foreach($part in $relative.Split('/')){$candidate=Join-Path $candidate $part;Assert-Plain $candidate}
        if(Test-Path -LiteralPath $candidate -PathType Leaf){$candidates+=Get-Item -LiteralPath $candidate}
    }
    foreach($name in $rules.ConfigFolders) {
        $sub=Join-Path $path $name
        if(Test-Path -LiteralPath $sub) {
            $queue=New-Object 'Collections.Generic.Queue[string]';$queue.Enqueue($sub)
            while($queue.Count -gt 0) { $current=$queue.Dequeue();Assert-Plain $current;$candidates+=@(Get-ChildItem -LiteralPath $current -File);foreach($child in Get-ChildItem -LiteralPath $current -Directory){Assert-Plain $child.FullName;$queue.Enqueue($child.FullName)} }
        }
    }
    foreach($file in $candidates | Sort-Object FullName -Unique) {
        if($file.Extension -notin @('.json','.json5','.toml','.properties','.cfg') -or $file.Name -match '(?i)(before|\.bak|\.pre-|account|token|fingerprint|playlist)') { continue }
        $text=Get-Content -LiteralPath $file.FullName -Raw
        if($text -match '(?im)["'']?(access[_-]?token|refresh[_-]?token|api[_-]?key|password|client[_-]?secret)["'']?\s*[:=]\s*["'']?[^\s"'',}\r\n]{4,}') { throw "Potential private credential in config; review before publishing: $($file.Name)" }
        Add-ReleaseFile $file 'config' 'seed' $null
    }
}
$pointer=Join-Path $destination 'latest.json'
if(!$PreviousReleaseZip -and (Test-Path -LiteralPath $pointer)) {
    $latest=Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json
    $PreviousReleaseZip=Join-Path $destination $latest.Zip
    $prefix=$destination+'\'
    if(![IO.Path]::GetFullPath($PreviousReleaseZip).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Latest pointer escapes release storage.' }
    if((Sha $PreviousReleaseZip) -ne $latest.Sha256) { throw 'Prior release hash differs from latest pointer.' }
}
$retired=New-Object 'Collections.Generic.List[object]'
$sequence=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
if($PreviousReleaseZip) {
    $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PreviousReleaseZip))
    try { $previous=(Read-Entry $zip 'mc2-release.json') | ConvertFrom-Json } finally { $zip.Dispose() }
    if($previous.PackId -notin @($rules.PackId,'minecraft-squared') -or $previous.Schema -ne 2) { throw 'Prior release is not a full MC2 pack.' }
    $sequence=[Math]::Max($sequence,[long]$previous.Sequence+1)
    foreach($row in $previous.Retired) { $retired.Add($row) }
    foreach($row in $previous.Files | Where-Object { ($_.Kind -eq 'mod' -or $_.Path.StartsWith('resourcepacks/')) -and $_.Path -notin $files.Path }) { $retired.Add([pscustomobject]@{Path=$row.Path;Hash=$row.Hash}) }
}
$packOrder=New-Object 'Collections.Generic.List[string]'
$remaining=@($files | Where-Object {$_.Path.StartsWith('resourcepacks/')} | ForEach-Object {$_.Path.Substring(14)})
foreach($prefix in $rules.ResourceOrder) { foreach($name in @($remaining | Where-Object {$_.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)})) { $packOrder.Add('file/'+$name);$remaining=@($remaining | Where-Object {$_ -ne $name}) } }
# New packs not in the order list stay below the curated compatibility stack.
$ordered=@($remaining | ForEach-Object {'file/'+$_})+$packOrder.ToArray()+@('stars_general_fixes:stars_compat')
# Match the tested builtin support-pack priorities without copying host options.txt.
$insertion=[Array]::FindIndex([string[]]$ordered,[Predicate[string]]{param($p) $p.StartsWith('file/Actually 3D')})
if($insertion -lt 0){$insertion=$ordered.Count-1}
$builtins=@()
if($ids.ContainsKey('eatinganimationid')){
    $fixes=$files|Where-Object{$_.Id -eq 'stars_general_fixes'}
    $jar=[IO.Compression.ZipFile]::OpenRead((Join-Path $source $fixes.Path))
    try{$curated=$null -ne $jar.GetEntry('resourcepacks/eating_compat/pack.mcmeta')}finally{$jar.Dispose()}
    $builtins+=if($curated){'stars_general_fixes:eating_compat'}else{'eatinganimationid:supporteatinganimation'}
}
if($ids.ContainsKey('punchy')){$builtins+='punchy:punchy'}
$packList=New-Object 'Collections.Generic.List[string]';$packList.AddRange([string[]]$ordered);$packList.InsertRange($insertion,[string[]]$builtins);$ordered=$packList.ToArray()
# Preserve the author's tested order, reading only this single options key.
$options=Join-Path $source 'options.txt'
if(Test-Path -LiteralPath $options){
    $line=Get-Content -LiteralPath $options|Where-Object { $_.StartsWith('resourcePacks:') }|Select-Object -First 1
    if($line){$selected=$line.Substring(14)|ConvertFrom-Json;$curated=[Collections.Generic.List[string]]::new();foreach($pack in @($selected)+@($ordered)){if($pack -ne 'stars_general_fixes:stars_compat' -and $pack -in $ordered -and !$curated.Contains($pack)){$curated.Add($pack)}};$curated.Add('stars_general_fixes:stars_compat');$ordered=$curated.ToArray()}
}
$release=[ordered]@{Schema=2;Sequence=$sequence;PackId=$rules.PackId;Name=$ReleaseName;Minecraft=[string]$meta.baseModLoader.minecraftVersion;Loader=[string]$meta.baseModLoader.forgeVersion;Files=$files.ToArray();Retired=@($retired | Sort-Object Path,Hash -Unique);Packs=$ordered}
$logical=($files | Measure-Object Size -Sum).Sum
Write-Output ("Release {0}: {1} mods, {2} files, {3:N1} MiB. Minecraft {4} / Fabric {5}." -f $ReleaseName,$ids.Count,$files.Count,($logical/1MB),$release.Minecraft,$release.Loader)
Write-Output 'Worlds, launcher/account data, maps, options, caches and executables are excluded. Config defaults use preserve-local-settings policy.'
if($PreviousReleaseZip) { Write-Output "Retirement history retained from: $PreviousReleaseZip" }
if($Preview) { Write-Output 'Preview only; no files published.';return }
Assert-Plain $destination
$out=Join-Path $destination $ReleaseName
if(Test-Path -LiteralPath $out) { throw 'Release folder already exists. Use a new release name; published releases are immutable.' }
$drive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($destination))
$freeBefore=$drive.AvailableFreeSpace
if($freeBefore -lt 3*$logical+256MB) { throw 'Insufficient physical space for release ZIP, EXE and verification.' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$zipPath=Join-Path $out "MC2-$ReleaseName.zip"
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try {
    $entry=$archive.CreateEntry('mc2-release.json');$writer=New-Object IO.StreamWriter($entry.Open(),$utf8)
    try{$writer.Write(($release | ConvertTo-Json -Depth 12 -Compress))}finally{$writer.Dispose()}
    foreach($file in $files) {
        $original=Join-Path $source $file.Path.Replace('/','\');Assert-Plain $original
        if((Sha $original) -ne $file.Hash) { throw "Profile changed while publishing; retry with a new release name: $($file.Path)" }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$original,$file.Path,[IO.Compression.CompressionLevel]::NoCompression) | Out-Null
    }
} finally {$archive.Dispose()}
if(@(Get-ChildItem -LiteralPath $mods -File -Filter '*.jar').Count -ne $ids.Count) { throw 'Mod inventory changed while publishing. Release was not promoted.' }
# Independently hash the finished archive entries, including changes during copy.
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try { foreach($file in $files) { $input=$archive.GetEntry($file.Path).Open();$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($input)).Replace('-','');if($actual -ne $file.Hash){throw "Archived payload mismatch: $($file.Path)"}}finally{$input.Dispose();$sha.Dispose()} } } finally {$archive.Dispose()}
$notes=@"
Minecraft² — $ReleaseName
In Astera, link a CurseForge-created Minecraft $($release.Minecraft) / Fabric $($release.Loader) or newer profile.
Publish this ZIP with Publish-FullPackUpdate.ps1. Players receive it through Astera's Update button.
Active mods, supplied resource packs and approved config defaults are included.
Worlds, accounts, backups, disabled files, personal options and caches are excluded.
"@
[IO.File]::WriteAllText((Join-Path $out 'READ-ME.txt'),$notes,$utf8)
$receipt=[ordered]@{Release=$ReleaseName;Sequence=$sequence;Zip="$ReleaseName/MC2-$ReleaseName.zip";Sha256=(Sha $zipPath);Files=$files.Count;Mods=$ids.Count;Bytes=$logical;PhysicalFreeBefore=$freeBefore;PhysicalFreeAfter=$drive.AvailableFreeSpace;CreatedUtc=[DateTime]::UtcNow.ToString('o')}
[IO.File]::WriteAllText((Join-Path $out 'receipt.json'),($receipt | ConvertTo-Json -Depth 5),$utf8)
$tempPointer=Join-Path $destination 'latest-next.json'
[IO.File]::WriteAllText($tempPointer,($receipt | ConvertTo-Json -Depth 5),$utf8)
if(Test-Path -LiteralPath $pointer) {[IO.File]::Replace($tempPointer,$pointer,[NullString]::Value)}else{[IO.File]::Move($tempPointer,$pointer)}
Write-Output "ZIP: $zipPath"