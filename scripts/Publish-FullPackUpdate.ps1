#Requires -Version 7.2
param(
 [Parameter(Mandatory)][string]$Zip,
 [Parameter(Mandatory)][string]$NotesFile,
 [Parameter(Mandatory)][string]$TestReceipt,
 [string]$Repository='ReshawnStar/Astera-Game-Launcher',
 [string]$Executable='',
 [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key',
 [string]$GitHubCli='D:/vale/StarsAllies-Storage/tools/github-cli/v2.103.2/bin/gh.exe',
 [switch]$Publish
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim();$Executable="D:/vale/StarsAllies-Storage/builds/Astera/$version/Astera.exe"}
$zipItem=Get-Item -LiteralPath $Zip
$archive=[IO.Compression.ZipFile]::OpenRead($zipItem.FullName)
try{$reader=[IO.StreamReader]::new($archive.GetEntry('mc2-release.json').Open());try{$release=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$archive.Dispose()}
if($release.PackId -ne 'astera-minecraft-squared' -or $release.Name -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,70}$'){throw 'Not an Astera full-pack ZIP'}
if($Repository -notmatch '^[\w.-]+/[\w.-]+$'){throw 'Invalid repository'}
$tag=$release.Name
$notes=Get-Content -LiteralPath $NotesFile -Raw
$hash=(Get-FileHash -LiteralPath $Zip).Hash
$tested=Get-Content -LiteralPath $TestReceipt -Raw|ConvertFrom-Json
if(!$tested.Passed -or $tested.BundleSha256 -ne $hash){throw 'An isolated passing install receipt is required for this exact ZIP'}
$feed=@{Schema=2;GameId='minecraft-squared';ExpiresUtc=[DateTimeOffset]::UtcNow.AddYears(1).ToUnixTimeSeconds();Release=$release;Sources=@();RequiredMods=@();Notes=$notes;Bundle=@{Url="https://github.com/$Repository/releases/download/$tag/$($zipItem.Name)";Hash=$hash;Size=$zipItem.Length}}
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(3072,[Security.Cryptography.CspParameters]::new(24));$rsa.PersistKeyInCsp=$false
try{
 $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($PrivateKeyFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($raw))}finally{[Array]::Clear($raw,0,$raw.Length)}
 if($rsa.ToXmlString($false) -ne (Get-Content -LiteralPath (Join-Path $root 'channel.public.xml') -Raw).Trim()){throw 'Key mismatch'}
 $payload=[Text.Encoding]::UTF8.GetBytes(($feed|ConvertTo-Json -Depth 12 -Compress))
 $envelope=@{Payload=[Convert]::ToBase64String($payload);Signature=[Convert]::ToBase64String($rsa.SignData($payload,'SHA256'))}
 $notice=Join-Path $zipItem.DirectoryName 'minecraft-pack.json';$envelope|ConvertTo-Json -Compress|Set-Content -LiteralPath $notice -Encoding utf8
 $p=Start-Process -FilePath $Executable -ArgumentList @('--verify-feed',('"'+$notice+'"')) -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode -ne 0){throw 'Astera rejected full-pack notice'}
 if($Publish){
  $listing=& $GitHubCli api "repos/$Repository/contents/channels?ref=main";if($LASTEXITCODE -ne 0){throw 'Cannot inspect channels'}
  $entry=@($listing|ConvertFrom-Json|Where-Object name -eq 'minecraft-pack.json');$priorSha=$null
  if($entry.Count){$previous=& $GitHubCli api "repos/$Repository/contents/channels/minecraft-pack.json?ref=main";if($LASTEXITCODE -ne 0){throw 'Cannot inspect prior full pack'};$previous=$previous|ConvertFrom-Json;$priorSha=$previous.sha;$old=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($previous.content))|ConvertFrom-Json;$oldBytes=[Convert]::FromBase64String($old.Payload);if(!$rsa.VerifyData($oldBytes,'SHA256',[Convert]::FromBase64String($old.Signature))){throw 'Prior signature mismatch'};$prior=[Text.Encoding]::UTF8.GetString($oldBytes)|ConvertFrom-Json;if($prior.Release.Sequence -ge $release.Sequence){throw 'Full-pack downgrade refused'}}
  & $GitHubCli release create $tag $zipItem.FullName --repo $Repository --target main --title ('Minecraft² — '+$tag) --notes-file $NotesFile --latest=false
  if($LASTEXITCODE -ne 0){throw 'ZIP upload failed; channel unchanged'}
  $verify=Join-Path $zipItem.DirectoryName 'download-verification';New-Item -ItemType Directory -Path $verify -Force|Out-Null
  & $GitHubCli release download $tag --repo $Repository --pattern $zipItem.Name --dir $verify
  if($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath (Join-Path $verify $zipItem.Name)).Hash -ne $hash){throw 'Hosted ZIP verification failed; channel unchanged'}
  $request=@{message=('Publish Minecraft² full pack '+$tag);content=[Convert]::ToBase64String([IO.File]::ReadAllBytes($notice));branch='main'};if($priorSha){$request.sha=$priorSha}
  $requestPath=Join-Path $zipItem.DirectoryName 'channel-request.json';$request|ConvertTo-Json -Compress|Set-Content -LiteralPath $requestPath -Encoding utf8
  & $GitHubCli api --method PUT "repos/$Repository/contents/channels/minecraft-pack.json" --input $requestPath | Out-Null
  if($LASTEXITCODE -ne 0){throw 'Channel promotion failed; ZIP retained, channel unchanged'}
 }
 @{Passed=$true;Published=[bool]$Publish;Zip=$zipItem.FullName;Sha256=$hash;Release=$tag;Sequence=$release.Sequence;Files=$release.Files.Count}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $zipItem.DirectoryName 'publication-receipt.json')
}finally{$rsa.Dispose()}
Write-Output ('Full-pack feed prepared; published='+[bool]$Publish)
