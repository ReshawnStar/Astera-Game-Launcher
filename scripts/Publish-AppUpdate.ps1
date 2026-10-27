#Requires -Version 7.2
param(
 [string]$Executable='', [Parameter(Mandatory)][string]$TestReceipt,
 [Parameter(Mandatory)][string]$SelfUpdateReceipt,
 [string]$NotesFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'app-release-notes.md'),
 [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key',
 [string]$OutputRoot='D:/vale/StarsAllies-Storage/releases/Astera',
 [string]$Repository='ReshawnStar/Astera-Game-Launcher', [string]$Branch='main',
 [string]$GitHubCli='D:/vale/StarsAllies-Storage/tools/github-cli/v2.103.2/bin/gh.exe',
 [switch]$Publish
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$' -or $Repository -notmatch '^[\w.-]+/[\w.-]+$'){throw 'Invalid version or repository'}
if(!$Executable){$Executable='D:/vale/StarsAllies-Storage/builds/Astera/'+$version+'/Astera.exe'}
$file=Get-Item -LiteralPath $Executable
if($file.Attributes -band [IO.FileAttributes]::ReparsePoint -or $file.VersionInfo.ProductName -ne 'Astera Game Launcher' -or $file.VersionInfo.FileVersion -ne ($version+'.0')){throw 'Choose the exact Astera release build'}
$hash=(Get-FileHash -LiteralPath $Executable).Hash
$test=Get-Content -LiteralPath $TestReceipt -Raw|ConvertFrom-Json
$self=Get-Content -LiteralPath $SelfUpdateReceipt -Raw|ConvertFrom-Json
if(!$test.Passed -or !$self.Passed -or $test.AppSha256 -ne $hash -or $self.CandidateSha256 -ne $hash){throw 'Both test receipts must pass for this exact executable'}
$notes=Get-Content -LiteralPath $NotesFile -Raw;if($notes.Length -gt 16000){throw 'App release notes exceed limit'}
$tag='astera-v'+$version;$out=Join-Path $OutputRoot $tag
if(Test-Path -LiteralPath $out){throw 'Immutable app release staging folder already exists'}
New-Item -ItemType Directory -Path $out -Force|Out-Null
Copy-Item -LiteralPath $Executable -Destination (Join-Path $out 'Astera.exe')
if((Get-FileHash -LiteralPath (Join-Path $out 'Astera.exe')).Hash -ne $hash){throw 'App changed while staging'}
$sequence=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds();$remoteSha=$null
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(3072,[Security.Cryptography.CspParameters]::new(24));$rsa.PersistKeyInCsp=$false
try{
 $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($PrivateKeyFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($raw))}finally{[Array]::Clear($raw,0,$raw.Length)}
 if($rsa.ToXmlString($false) -ne (Get-Content -LiteralPath (Join-Path $root 'channel.public.xml') -Raw).Trim()){throw 'Publishing key differs from embedded public key'}
 $api='repos/'+$Repository+'/contents/channels/astera.json'
 if($Publish){
  # A missing path is allowed; connection/authentication errors are not.
  $listing=& $GitHubCli api ('repos/'+$Repository+'/contents/channels?ref='+[Uri]::EscapeDataString($Branch));if($LASTEXITCODE -ne 0){throw 'Cannot inspect hosted channels'}
  if(@($listing|ConvertFrom-Json|Where-Object name -eq 'astera.json').Count){
   $remote=& $GitHubCli api ($api+'?ref='+[Uri]::EscapeDataString($Branch));if($LASTEXITCODE -ne 0){throw 'Cannot read current app channel'};$remote=$remote|ConvertFrom-Json;$remoteSha=$remote.sha
   $signed=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($remote.content))|ConvertFrom-Json;$payload=[Convert]::FromBase64String($signed.Payload)
   if(!$rsa.VerifyData($payload,'SHA256',[Convert]::FromBase64String($signed.Signature))){throw 'Current app channel has a different signing key'}
   $previous=[Text.Encoding]::UTF8.GetString($payload)|ConvertFrom-Json
   if($previous.AppId -ne 'astera-launcher' -or [version]$previous.Version -ge [version]$version){throw 'App channel is equal or newer; no downgrade published'}
   $sequence=[Math]::Max($sequence,[long]$previous.Sequence+1)
  }
 }
 $release=@{Schema=1;AppId='astera-launcher';Version=$version;Url=('https://github.com/'+$Repository+'/releases/download/'+$tag+'/Astera.exe');Size=$file.Length;Sha256=$hash;Sequence=$sequence;ExpiresUtc=[DateTimeOffset]::UtcNow.AddYears(1).ToUnixTimeSeconds();Notes=$notes}
 $bytes=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress));$envelope=@{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($rsa.SignData($bytes,'SHA256'))}
 $notice=Join-Path $out 'astera.json';$envelope|ConvertTo-Json -Compress|Set-Content -LiteralPath $notice -Encoding utf8
 $verify=Start-Process -FilePath $Executable -ArgumentList @('--verify-app-candidate',('"'+$notice+'"'),('"'+(Join-Path $out 'Astera.exe')+'"')) -WindowStyle Hidden -PassThru -Wait;if($verify.ExitCode -ne 0){throw 'The actual app rejected its prepared release'}
 if($Publish){
  & $GitHubCli release create $tag (Join-Path $out 'Astera.exe') --repo $Repository --target $Branch --title ('Astera '+$version) --notes-file $NotesFile --latest
  if($LASTEXITCODE -ne 0){throw 'App upload failed; channel unchanged'}
  $verifyDir=Join-Path $out 'download-verification';New-Item -ItemType Directory -Path $verifyDir|Out-Null
  & $GitHubCli release download $tag --repo $Repository --pattern Astera.exe --dir $verifyDir
  if($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath (Join-Path $verifyDir 'Astera.exe')).Hash -ne $hash){throw 'Hosted app did not match tested build; channel unchanged'}
  Remove-Item -LiteralPath (Join-Path $verifyDir 'Astera.exe')
  $request=@{message=('Publish Astera '+$version+' update channel');branch=$Branch;content=[Convert]::ToBase64String([IO.File]::ReadAllBytes($notice))};if($remoteSha){$request.sha=$remoteSha}
  $requestFile=Join-Path $out 'channel-request.json';$request|ConvertTo-Json -Compress|Set-Content -LiteralPath $requestFile -Encoding utf8
  & $GitHubCli api $api --method PUT --input $requestFile > $null
  if($LASTEXITCODE -ne 0){throw 'App uploaded, but channel promotion conflicted or failed. Inspect before retrying.'}
 }
 @{Version=$version;Sha256=$hash;Sequence=$sequence;Published=[bool]$Publish;DownloadVerified=[bool]$Publish;TestReceipt=$TestReceipt;SelfUpdateReceipt=$SelfUpdateReceipt}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'receipt.json')
 Write-Output ("Prepared app release: "+$out+"; published="+[bool]$Publish)
}finally{$rsa.Dispose()}
