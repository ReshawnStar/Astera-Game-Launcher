#Requires -Version 7.2
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,70}$')][string]$ReleaseName,
    [string]$BaseUrl='',
    [ValidatePattern('^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$')][string]$GitHubRepository,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._/-]*$')][string]$GitHubBranch='main',
    [string]$ProfileDirectory='',
    [string]$NotesFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'release-notes.md'),
    [string]$RulesFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'managed-files.json'),
    [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key',
    [string]$GitHubCliPath='',
    [string]$PreviousFeed='',
    [string]$OutputRoot='D:/vale/StarsAllies-Storage/releases/Astera',
    [switch]$Preview,
    [switch]$Upload
)
$ErrorActionPreference='Stop'
$channelSettings=Join-Path (Split-Path $PSScriptRoot -Parent) 'channel-settings.json'
if(!$GitHubRepository -and (Test-Path -LiteralPath $channelSettings)){$GitHubRepository=(Get-Content -Raw -LiteralPath $channelSettings|ConvertFrom-Json).GitHubRepository}
$localSettings=Join-Path (Split-Path $PSScriptRoot -Parent) 'publish-settings.local.json'
if(!$GitHubCliPath -and (Test-Path -LiteralPath $localSettings)){$GitHubCliPath=(Get-Content -Raw -LiteralPath $localSettings|ConvertFrom-Json).GitHubCliPath}
$ghCommand=if($GitHubCliPath){$GitHubCliPath}else{'gh'}
if($GitHubRepository -and !$BaseUrl){$BaseUrl='https://github.com/'+$GitHubRepository+'/releases/download/'+$ReleaseName+'/'}
if(!$BaseUrl){throw 'Supply -BaseUrl, or -GitHubRepository owner/repository'}
if($Upload -and $BaseUrl.TrimEnd('/') -ne ('https://github.com/'+$GitHubRepository+'/releases/download/'+$ReleaseName)){throw 'Automatic GitHub upload requires the matching GitHub release asset URL'}
if($Upload -and (!$GitHubRepository -or !(Get-Command $ghCommand -ErrorAction SilentlyContinue))){throw 'Upload needs -GitHubRepository and an installed, signed-in GitHub CLI. Local preparation does not.'}
if(!$PreviousFeed){$latest=Join-Path $OutputRoot 'latest-minecraft.json';if(Test-Path -LiteralPath $latest){$PreviousFeed=$latest}}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Security
$uri=[Uri]$BaseUrl
if(!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment -or $uri.Query){throw 'BaseUrl must be an HTTPS release-asset directory without credentials/query/fragment'}
if(!$ProfileDirectory){$instances=Join-Path $env:USERPROFILE 'curseforge/minecraft/Instances';$matches=@(foreach($folder in Get-ChildItem -LiteralPath $instances -Directory){$file=Join-Path $folder.FullName 'minecraftinstance.json';if(Test-Path -LiteralPath $file){$m=Get-Content -Raw -LiteralPath $file|ConvertFrom-Json;if($m.name -in @('Minecraft Squared','Minecraft²','Minecraft2')){$folder.FullName}}});if($matches.Count -ne 1){throw 'Choose the source with -ProfileDirectory; no unique Minecraft Squared profile found'};$ProfileDirectory=$matches[0]}
$profile=[IO.Path]::GetFullPath($ProfileDirectory)
function Plain([string]$path){$at=$path;while($at){if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Redirected input refused: '+$at)};$at=Split-Path $at -Parent}}
Plain $profile
$meta=Get-Content -Raw -LiteralPath (Join-Path $profile 'minecraftinstance.json')|ConvertFrom-Json
if($meta.baseModLoader.name -notlike 'fabric-*'){throw 'Publish from the tested CurseForge Fabric profile'}
function Mod([string]$path){$zip=[IO.Compression.ZipFile]::OpenRead($path);try{$entry=$zip.GetEntry('fabric.mod.json');if(!$entry){throw 'Root mod has no Fabric metadata'};$reader=[IO.StreamReader]::new($entry.Open());try{return $reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$zip.Dispose()}}
$files=[Collections.Generic.List[object]]::new();$required=[Collections.Generic.List[object]]::new();$ids=@{};$local=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $profile 'mods') -File -Filter '*.jar'|Sort-Object Name){Plain $file.FullName;$mod=Mod $file.FullName;if(!$mod.id -or !$mod.version -or $ids.ContainsKey([string]$mod.id)){throw 'Missing/duplicate mod identity'};$ids[[string]$mod.id]=$true
    if($mod.id -notin @('stars_allies','stars_general_fixes','waystone_map_travel')){$required.Add(@{Id=[string]$mod.id;Version=[string]$mod.version});continue}
    $path='mods/'+$file.Name;$files.Add(@{Path=$path;Hash=(Get-FileHash -LiteralPath $file.FullName).Hash;Size=$file.Length;Kind='mod';Policy='replace';Id=[string]$mod.id;Version=[string]$mod.version});$local[$path]=$file.FullName
}
if(!$ids.ContainsKey('stars_allies') -or !$ids.ContainsKey('stars_general_fixes')){throw 'Both tested Stars components are required'}
$rules=Get-Content -Raw -LiteralPath $RulesFile|ConvertFrom-Json
foreach($item in $rules.Files){$relative=[string]$item.Path;if($relative -notmatch '^(config|defaultconfigs)/[a-zA-Z0-9_./-]+\.(json|json5|toml|properties|cfg)$' -or $relative -match '(^|/)\.\.?(/|$)'){throw 'Only explicitly listed configuration defaults are accepted by this publisher'};$path=Join-Path $profile $relative;if(!(Test-Path -LiteralPath $path)){continue};Plain $path;$text=Get-Content -Raw -LiteralPath $path;if($text -match '(?im)["'']?(access[_-]?token|refresh[_-]?token|api[_-]?key|password|client[_-]?secret)["'']?\s*[:=]\s*["'']?[^\s"'',}\r\n]{4,}'){throw ('Review possible private data in '+$relative)};$f=Get-Item -LiteralPath $path;$files.Add(@{Path=$relative;Hash=(Get-FileHash -LiteralPath $path).Hash;Size=$f.Length;Kind='config';Policy='seed'});$local[$relative]=$path}
$packs=[Collections.Generic.List[string]]::new()
foreach($item in $rules.ResourcePacks){
    $relative=[string]$item.Path
    if($item.RedistributionApproved -ne $true -or $relative -notmatch '^resourcepacks/[^\\/:\x00-\x1f]+\.zip$'){throw 'Resource archives need an explicit root ZIP path and RedistributionApproved=true in the publishing rules'}
    $path=Join-Path $profile $relative;Plain $path
    if(!(Test-Path -LiteralPath $path)){throw ('Approved resource pack is missing: '+$relative)}
    $zip=[IO.Compression.ZipFile]::OpenRead($path);try{if(!$zip.GetEntry('pack.mcmeta')){throw 'Resource pack has no pack.mcmeta'}}finally{$zip.Dispose()}
    $file=Get-Item -LiteralPath $path;$files.Add(@{Path=$relative;Hash=(Get-FileHash -LiteralPath $path).Hash;Size=$file.Length;Kind='asset';Policy='replace'});$local[$relative]=$path;$packs.Add('file/'+[IO.Path]::GetFileName($path))
}
if(@($files|Group-Object Path|Where-Object Count -gt 1).Count){throw 'Duplicate managed path in publishing rules'}
$packs.Add('stars_general_fixes:stars_compat')
$sequence=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds();$retired=@();$previous=$null
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(3072,[Security.Cryptography.CspParameters]::new(24));$rsa.PersistKeyInCsp=$false
try{
    $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($PrivateKeyFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($raw))}finally{[Array]::Clear($raw,0,$raw.Length)}
    if($PreviousFeed){$signed=Get-Content -Raw -LiteralPath $PreviousFeed|ConvertFrom-Json;$bytes=[Convert]::FromBase64String($signed.Payload);if(!$rsa.VerifyData($bytes,'SHA256',[Convert]::FromBase64String($signed.Signature))){throw 'Previous feed signature is invalid'};$previous=[Text.Encoding]::UTF8.GetString($bytes)|ConvertFrom-Json;if($previous.GameId -ne 'minecraft-squared'){throw 'Previous feed belongs to another game'};$sequence=[Math]::Max($sequence,[long]$previous.Release.Sequence+1);$retired=@($previous.Release.Retired)+@($previous.Release.Files|Where-Object{($_.Kind -eq 'mod' -or $_.Path.StartsWith('resourcepacks/')) -and $_.Path -notin $files.Path}|ForEach-Object{@{Path=$_.Path;Hash=$_.Hash}})}
    $notes=Get-Content -Raw -LiteralPath $NotesFile;if($notes.Length -gt 32000){throw 'Release notes too long'}
    $sources=@();$uploads=@{}
    foreach($file in $files){$old=if($previous){@($previous.Release.Files|Where-Object{$_.Path -eq $file.Path -and $_.Hash -eq $file.Hash -and $_.Size -eq $file.Size})}else{@()};if($old.Count -eq 1){$url=($previous.Sources|Where-Object Path -eq $file.Path).Url}else{$url=$BaseUrl.TrimEnd('/')+'/'+$file.Hash+'.bin';$uploads[$file.Hash]=$local[$file.Path]};$sources+=@{Path=$file.Path;Url=$url}}
    $release=@{Schema=2;Sequence=$sequence;PackId='astera-minecraft-squared';Name=$ReleaseName;Minecraft=[string]$meta.baseModLoader.minecraftVersion;Loader=[string]$meta.baseModLoader.forgeVersion;Files=$files.ToArray();Retired=$retired;Packs=$packs.ToArray()}
    $payload=@{Schema=1;GameId='minecraft-squared';ExpiresUtc=([DateTimeOffset]::UtcNow.AddYears(1).ToUnixTimeSeconds());Notes=$notes;Release=$release;Sources=$sources;RequiredMods=$required.ToArray()}
    $bytes=[Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Depth 16 -Compress));$signature=$rsa.SignData($bytes,'SHA256')
    Write-Output ('Release '+$ReleaseName+': '+$files.Count+' managed files; '+$uploads.Count+' new payloads; '+$required.Count+' CurseForge prerequisites. No third-party mod binaries included.')
    if($Preview){Write-Output 'Preview only; nothing published.';return}
    $out=Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $ReleaseName;Plain $out;if(Test-Path -LiteralPath $out){throw 'Release output exists; use a new immutable release name'};New-Item -ItemType Directory -Path $out|Out-Null
    foreach($hash in $uploads.Keys){$target=Join-Path $out ($hash+'.bin');Copy-Item -LiteralPath $uploads[$hash] -Destination $target;if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw 'Source changed while publishing'}}
    @{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($signature)}|ConvertTo-Json -Compress|Set-Content -LiteralPath (Join-Path $out 'minecraft-squared.json') -Encoding utf8
    @{Release=$ReleaseName;Sequence=$sequence;ManagedFiles=$files.Count;UploadedPayloads=$uploads.Count;ThirdPartyBinaries=0;PublicKeyFingerprint=([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($false)))))}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'receipt.json')
    if($Upload){
        $channelPath='repos/'+$GitHubRepository+'/contents/channels/minecraft-squared.json'
        $remoteText=& $ghCommand api ($channelPath+'?ref='+[Uri]::EscapeDataString($GitHubBranch)) 2>$null
        $remoteSha=$null
        if($LASTEXITCODE -eq 0){
            $remote=$remoteText|ConvertFrom-Json
            if($remote.type -ne 'file' -or $remote.encoding -ne 'base64' -or $remote.size -gt 8MB){throw 'Remote channel is not a supported signed notice'}
            $remoteEnvelope=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($remote.content))|ConvertFrom-Json
            $remotePayload=[Convert]::FromBase64String($remoteEnvelope.Payload)
            if(!$rsa.VerifyData($remotePayload,'SHA256',[Convert]::FromBase64String($remoteEnvelope.Signature))){throw 'Existing public channel has a different/invalid signing key; channel was not changed'}
            $remoteFeed=[Text.Encoding]::UTF8.GetString($remotePayload)|ConvertFrom-Json
            if($remoteFeed.GameId -ne 'minecraft-squared' -or [long]$remoteFeed.Release.Sequence -ge $sequence){throw 'Public channel already has an equal/newer release; retrieve its signed notice as -PreviousFeed and prepare a new release'}
            $remoteSha=$remote.sha
        }
        $assets=@(Get-ChildItem -LiteralPath $out -File -Filter '*.bin'|ForEach-Object FullName)
        # Content assets are immutable. Updating the small signed channel uses
        # GitHub's conditional contents commit, so a failed promotion leaves the
        # old channel intact instead of deleting/re-uploading a release asset.
        & $ghCommand release create $ReleaseName @assets --repo $GitHubRepository --target $GitHubBranch --title ('Minecraft² — '+$ReleaseName) --notes-file $NotesFile --latest=false
        if($LASTEXITCODE -ne 0){throw 'Content release upload failed; update channel was not promoted'}
        $request=@{message=('Publish Minecraft² '+$ReleaseName);branch=$GitHubBranch;content=[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $out 'minecraft-squared.json')))}
        if($remoteSha){$request.sha=$remoteSha}
        $requestPath=Join-Path $out 'channel-request.json';$request|ConvertTo-Json -Compress|Set-Content -LiteralPath $requestPath -Encoding utf8
        & $ghCommand api $channelPath --method PUT --input $requestPath > $null
        if($LASTEXITCODE -ne 0){throw 'Content uploaded, but channel promotion failed or conflicted. The existing public notice was not replaced; inspect before retrying.'}
        $pointer=Join-Path $OutputRoot 'latest-minecraft.json';$temp=Join-Path $OutputRoot 'latest-minecraft.next';Copy-Item -LiteralPath (Join-Path $out 'minecraft-squared.json') -Destination $temp
        if(Test-Path -LiteralPath $pointer){[IO.File]::Replace($temp,$pointer,[NullString]::Value)}else{[IO.File]::Move($temp,$pointer)}
        Write-Output ('Published. Channel: https://raw.githubusercontent.com/'+$GitHubRepository+'/'+$GitHubBranch+'/channels/minecraft-squared.json')
    }else{Write-Output ('Prepared '+$out+'. Upload the .bin files to the configured release address and promote minecraft-squared.json only after they are available. No network upload was performed.')}
    Write-Output 'No application EXE was rebuilt.'
}finally{$rsa.Dispose()}
