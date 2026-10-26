#Requires -Version 7.2
param(
    [Parameter(Mandatory=$true)][string]$FixtureProfile,
    [Parameter(Mandatory=$true)][string]$TestDirectory,
    [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key'
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $TestDirectory){throw 'Use a new isolated publisher-test directory'}
New-Item -ItemType Directory -Path $TestDirectory|Out-Null
if(!(Test-Path -LiteralPath (Join-Path $FixtureProfile 'astera-test-fixture.txt')) -or (Get-Content -Raw -LiteralPath (Join-Path $FixtureProfile 'astera-test-fixture.txt')) -ne 'Synthetic self-test profile; never a live game.'){throw 'Publisher tests require an Astera synthetic fixture, never a live game profile'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$copy=Join-Path $TestDirectory 'profile';New-Item -ItemType Directory -Path (Join-Path $copy 'mods'),(Join-Path $copy 'config'),(Join-Path $copy 'resourcepacks') -Force|Out-Null
foreach($file in Get-ChildItem -LiteralPath (Join-Path $FixtureProfile 'mods') -File -Filter '*.jar'){if($file.Length -gt 1MB -or $file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unexpected large or redirected synthetic mod'};Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $copy 'mods')}
Copy-Item -LiteralPath (Join-Path $FixtureProfile 'minecraftinstance.json') -Destination $copy
Copy-Item -LiteralPath (Join-Path $FixtureProfile 'config/stars-general-fixes.json') -Destination (Join-Path $copy 'config')
$archive=[IO.Compression.ZipFile]::Open((Join-Path $copy 'resourcepacks/AsteraFixture.zip'),[IO.Compression.ZipArchiveMode]::Create)
try{$writer=[IO.StreamWriter]::new($archive.CreateEntry('pack.mcmeta').Open());try{$writer.Write('{"pack":{"pack_format":1,"description":"Synthetic updater fixture only"}}')}finally{$writer.Dispose()}}finally{$archive.Dispose()}
$rules=Join-Path $TestDirectory 'fixture-rules.json';@{Files=@(@{Path='config/stars-general-fixes.json'});ResourcePacks=@(@{Path='resourcepacks/AsteraFixture.zip';RedistributionApproved=$true})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $rules
if(Get-Variable -Name AsteraPublisherMock -Scope Global -ErrorAction SilentlyContinue){throw 'Publisher mock already exists'}
$global:AsteraPublisherMock=@{Remote=$null;FailRelease=$false;FailPut=$false;Calls=[Collections.Generic.List[object]]::new()}
function gh {
    $values=@($args|ForEach-Object{[string]$_});$m=$global:AsteraPublisherMock;$m.Calls.Add($values);$global:LASTEXITCODE=0
    if($values[0] -eq 'release' -and $values[1] -eq 'create'){
        if($m.FailRelease){$global:LASTEXITCODE=1;return}
        foreach($item in $values|Where-Object{$_ -like '*.bin'}){if(!(Test-Path -LiteralPath $item)){throw 'Publisher uploaded a nonexistent file'}}
        return 'mock content release'
    }
    if($values[0] -eq 'api'){
        if($values -contains 'PUT'){
            if($m.FailPut){$global:LASTEXITCODE=1;return}
            $index=[Array]::IndexOf($values,'--input');$body=Get-Content -Raw -LiteralPath $values[$index+1]|ConvertFrom-Json
            if($m.Remote -and $body.sha -ne $m.Remote.sha){throw 'Missing conditional channel SHA'}
            $m.Remote=@{type='file';encoding='base64';size=[Convert]::FromBase64String($body.content).Length;content=$body.content;sha=('a'*40)}
            return '{"content":{"sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}'
        }
        if(!$m.Remote){$global:LASTEXITCODE=1;return}
        return ($m.Remote|ConvertTo-Json -Compress)
    }
    throw 'Unexpected mocked GitHub command'
}
function Assert([bool]$value,[string]$message){if(!$value){throw $message}}
$checks=[Collections.Generic.List[string]]::new()
try{
    $publish=Join-Path $PSScriptRoot 'Publish-MinecraftUpdate.ps1'
    if((Get-Command gh).CommandType -ne 'Function'){throw 'Publisher mock must never call a real GitHub CLI'}
    $common=@{GitHubRepository='test-owner/Astera-Game-Launcher';ProfileDirectory=$copy;OutputRoot=$TestDirectory;PrivateKeyFile=$PrivateKeyFile;RulesFile=$rules;GitHubCliPath='gh';Upload=$true}
    & $publish @common -ReleaseName 'fixture-1'
    Assert ($null -ne $global:AsteraPublisherMock.Remote) 'Channel was not created'
    Assert (@($global:AsteraPublisherMock.Calls|Where-Object{$_[0] -eq 'api' -and $_ -contains 'PUT'}).Count -eq 1) 'Expected one final channel promotion'
    $checks.Add('Fresh publish uploads payload release, then promotes signed channel with conditional contents API')
    $firstEnvelope=Get-Content -Raw -LiteralPath (Join-Path $TestDirectory 'fixture-1/minecraft-squared.json')|ConvertFrom-Json
    $firstPayload=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($firstEnvelope.Payload))|ConvertFrom-Json
    Assert (@($firstPayload.Release.Files|Where-Object{$_.Path -eq 'resourcepacks/AsteraFixture.zip' -and $_.Kind -eq 'asset'}).Count -eq 1 -and $firstPayload.Release.Packs -contains 'file/AsteraFixture.zip') 'Explicit approved resource pack not represented in release/selection'
    $checks.Add('Explicitly approved resource pack receives verified asset metadata and resource-pack selection')
    $first=Get-Content -Raw -LiteralPath (Join-Path $TestDirectory 'fixture-1/receipt.json')|ConvertFrom-Json
    & $publish @common -ReleaseName 'fixture-2'
    $second=Get-Content -Raw -LiteralPath (Join-Path $TestDirectory 'fixture-2/receipt.json')|ConvertFrom-Json
    Assert ($second.UploadedPayloads -eq 0 -and $second.Sequence -gt $first.Sequence) 'Repeat publish did not reuse asset URLs or increment sequence'
    $checks.Add('Repeat publish reuses unchanged remote URLs, emits zero new binary assets and advances release sequence')
    $previous=$global:AsteraPublisherMock.Remote.content;$pointer=Get-Content -Raw -LiteralPath (Join-Path $TestDirectory 'latest-minecraft.json')
    $global:AsteraPublisherMock.FailRelease=$true;$failed=$false;try{& $publish @common -ReleaseName 'fixture-fail-assets'}catch{$failed=$true}
    Assert ($failed -and $global:AsteraPublisherMock.Remote.content -eq $previous) 'Failed asset upload changed the channel'
    $global:AsteraPublisherMock.FailRelease=$false;$checks.Add('Failed payload upload leaves the prior public channel intact')
    $global:AsteraPublisherMock.FailPut=$true;$failed=$false;try{& $publish @common -ReleaseName 'fixture-fail-promotion'}catch{$failed=$true}
    Assert ($failed -and $global:AsteraPublisherMock.Remote.content -eq $previous -and (Get-Content -Raw -LiteralPath (Join-Path $TestDirectory 'latest-minecraft.json')) -eq $pointer) 'Failed promotion changed public/local pointer'
    $global:AsteraPublisherMock.FailPut=$false;$checks.Add('Failed/conflicting channel commit preserves prior public notice and local publication pointer')
    $envelope=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($previous))|ConvertFrom-Json
    $bytes=[Convert]::FromBase64String($envelope.Payload);$bytes[0]=$bytes[0] -bxor 1;$envelope.Payload=[Convert]::ToBase64String($bytes)
    $global:AsteraPublisherMock.Remote.content=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($envelope|ConvertTo-Json -Compress)))
    $failed=$false;try{& $publish @common -ReleaseName 'fixture-bad-channel'}catch{$failed=$true}
    Assert $failed 'Invalid existing channel signature was accepted';$checks.Add('Invalid existing public-channel signature blocks promotion')
    Assert (@($global:AsteraPublisherMock.Calls|ForEach-Object{$_}|Where-Object{$_ -like '*.key' -or $_ -like '*private*'}).Count -eq 0) 'Signing key appeared in an upload argument'
    $checks.Add('Publishing uses the encrypted local key without passing it to GitHub or building a new application EXE')
    $unapproved=Join-Path $TestDirectory 'unapproved-rules.json';@{Files=@();ResourcePacks=@(@{Path='resourcepacks/AsteraFixture.zip';RedistributionApproved=$false})}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $unapproved
    $failed=$false;try{& $publish -ReleaseName 'unapproved' -BaseUrl 'https://example.invalid/unapproved/' -ProfileDirectory $copy -RulesFile $unapproved -PrivateKeyFile $PrivateKeyFile -Preview}catch{$failed=$true}
    Assert $failed 'Unapproved archive was swept into a publication';$checks.Add('Unapproved resource archive is refused before publication')
    @{Passed=$true;LiveGitHubContacted=$false;Checks=$checks.ToArray();MockedCommands=$global:AsteraPublisherMock.Calls.Count}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $TestDirectory 'result.json')
    Write-Output 'PUBLISHER_MOCK_TEST_PASSED: no real GitHub requests performed.'
}catch{$_|Out-String|Set-Content -LiteralPath (Join-Path $TestDirectory 'failure.txt');throw}finally{Remove-Variable -Name AsteraPublisherMock -Scope Global}
