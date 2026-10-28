param([string]$AuditRoot=('D:/vale/StarsAllies-Storage/audits/astera-self-update-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),[string]$Executable='', [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key')
if(!$Executable){$Executable='D:/vale/StarsAllies-Storage/builds/Astera/'+(Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'VERSION') -Raw).Trim()+'/Astera.exe'}
$releaseVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).FileVersion;$releaseVersion=([version]$releaseVersion).ToString(3);$nextVersion=[version]$releaseVersion;$nextVersion=('{0}.{1}.{2}' -f $nextVersion.Major,$nextVersion.Minor,($nextVersion.Build+1))
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $AuditRoot){throw 'Preserve previous test evidence; use a new audit root'}
New-Item -ItemType Directory -Path $AuditRoot|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
function Stub([string]$folder,[string]$version,[int]$wait,[int]$code){New-Item -ItemType Directory -Path $folder -Force|Out-Null;$source=Join-Path $folder 'fixture.cs';$text='[assembly:System.Reflection.AssemblyProduct("Astera Game Launcher")][assembly:System.Reflection.AssemblyVersion("'+$version+'")] [assembly:System.Reflection.AssemblyFileVersion("'+$version+'")] class Fixture { [System.STAThread] static int Main(string[] args){System.Threading.Thread.Sleep('+$wait+');return '+$code+';}}';[IO.File]::WriteAllText($source,$text);$exe=Join-Path $folder 'Astera.exe';& $compiler /nologo /target:winexe /platform:x64 "/out:$exe" $source;if($LASTEXITCODE -ne 0){throw 'Stub compilation failed'};return $exe}
$helperBuild=Join-Path (Split-Path $Executable -Parent) 'Astera.Updater.exe';if(!(Test-Path -LiteralPath $helperBuild)){throw 'Dedicated updater build missing'};$helperHash=(Get-FileHash -LiteralPath $helperBuild).Hash
$old=Stub (Join-Path $AuditRoot 'old-fixture') '0.0.0.0' 1800 0
$bad=Stub (Join-Path $AuditRoot 'bad-fixture') ($nextVersion+'.0') 0 42
$checks=[Collections.Generic.List[string]]::new()
Add-Type -AssemblyName System.Security
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(3072,[Security.Cryptography.CspParameters]::new(24));$rsa.PersistKeyInCsp=$false
try{
    $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($PrivateKeyFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($raw))}finally{[Array]::Clear($raw,0,$raw.Length)}
    function Sign([string]$candidate,[string]$version,[long]$expires){$release=@{Schema=1;AppId='astera-launcher';Version=$version;Url='https://github.com/test-owner/Astera/releases/download/fixture/Astera.exe';Size=(Get-Item -LiteralPath $candidate).Length;Sha256=(Get-FileHash -LiteralPath $candidate).Hash;Sequence=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds();ExpiresUtc=$expires;Notes='Fixture release'};$bytes=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress));return @{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($rsa.SignData($bytes,'SHA256'))}}
    $valid=Sign $Executable $releaseVersion ([DateTimeOffset]::UtcNow.AddDays(1).ToUnixTimeSeconds())
    $manifest=Join-Path $AuditRoot 'valid.json';$valid|ConvertTo-Json|Set-Content -LiteralPath $manifest
    $probe=Start-Process -FilePath $Executable -ArgumentList @('--verify-app-candidate',('"'+$manifest+'"'),('"'+$Executable+'"')) -WindowStyle Hidden -PassThru -Wait;if($probe.ExitCode -ne 0){throw 'Valid signed candidate rejected'};$checks.Add('Signed notice and exact candidate hash/version accepted')
    $tampered=@{Payload=$valid.Payload;Signature=[Convert]::ToBase64String([byte[]]::new(384))};$path=Join-Path $AuditRoot 'tampered.json';$tampered|ConvertTo-Json|Set-Content -LiteralPath $path;$probe=Start-Process -FilePath $Executable -ArgumentList @('--verify-app-feed',('"'+$path+'"')) -WindowStyle Hidden -PassThru -Wait;if($probe.ExitCode -eq 0){throw 'Tampered signature accepted'};$checks.Add('Tampered launcher signature refused')
    $path=Join-Path $AuditRoot 'expired.json';Sign $Executable $releaseVersion 1|ConvertTo-Json|Set-Content -LiteralPath $path;$probe=Start-Process -FilePath $Executable -ArgumentList @('--verify-app-feed',('"'+$path+'"')) -WindowStyle Hidden -PassThru -Wait;if($probe.ExitCode -eq 0){throw 'Expired release accepted'};$checks.Add('Expired launcher notice refused')
    $probe=Start-Process -FilePath $Executable -ArgumentList @('--verify-app-candidate',('"'+$manifest+'"'),('"'+$old+'"')) -WindowStyle Hidden -PassThru -Wait;if($probe.ExitCode -eq 0){throw 'Wrong executable accepted'};$checks.Add('Wrong executable/hash rejected before replacement')
    foreach($case in @(@{Name='success';Candidate=$Executable;Version=$releaseVersion;Expected='installed'},@{Name='startup-failure';Candidate=$bad;Version=$nextVersion;Expected='rolled-back'})){
        $folder=Join-Path $AuditRoot $case.Name;$data=Join-Path $folder 'data';$install=Join-Path $folder 'installation';$jobRoot=Join-Path $data ('self-update/jobs/'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $install,$jobRoot|Out-Null;$target=Join-Path $install 'Astera.exe';Copy-Item -LiteralPath $old -Destination $target;$before=(Get-FileHash -LiteralPath $target).Hash
        $prefs=@{Profile='C:/Astera-fixture-unavailable';StarDashExecutable='C:/Astera-fixture-unavailable/StarDash.exe';FeedUrl='https://example.invalid/fixture';CheckOnStart=$false;CheckAppOnStart=$false;KeepInTray=$false;UiSounds=$false;UiVolume=.35};$prefs|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $data 'preferences.json');$prefsHash=(Get-FileHash -LiteralPath (Join-Path $data 'preferences.json')).Hash
        [IO.File]::WriteAllText((Join-Path $folder 'personal-game-save.txt'),'unchanged fixture save');$saveHash=(Get-FileHash -LiteralPath (Join-Path $folder 'personal-game-save.txt')).Hash
        $candidate=Join-Path $jobRoot 'candidate.exe';Copy-Item -LiteralPath $case.Candidate -Destination $candidate;$notice=Join-Path $jobRoot 'release.json';Sign $candidate $case.Version ([DateTimeOffset]::UtcNow.AddDays(1).ToUnixTimeSeconds())|ConvertTo-Json|Set-Content -LiteralPath $notice
        $parent=Start-Process -FilePath $target -WindowStyle Hidden -PassThru
        $job=@{DataRoot=$data;Target=$target;Candidate=$candidate;Envelope=$notice;ExpectedCurrentHash=$before;HelperHash=$helperHash;OwnerPid=$parent.Id;OwnerStartTicks=$parent.StartTime.ToUniversalTime().Ticks;Nonce=([Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N'))};$jobFile=Join-Path $jobRoot 'job.json';$job|ConvertTo-Json|Set-Content -LiteralPath $jobFile
        $stage=Start-Process -FilePath $Executable -ArgumentList @('--start-self-update-helper',('"'+$jobFile+'"')) -WindowStyle Hidden -PassThru
        if(!$stage.WaitForExit(10000)){throw 'Launcher helper handoff timed out'}
        if($stage.ExitCode -ne 0){throw ('Dedicated helper failed to start: '+(Get-Content -Raw -LiteralPath (Join-Path $jobRoot 'helper-failure.txt')))}
        $helper=Join-Path $jobRoot 'Astera.Updater.exe';if((Get-FileHash -LiteralPath $helper).Hash -ne $helperHash -or (Get-FileHash -LiteralPath $helper).Hash -eq (Get-FileHash -LiteralPath $Executable).Hash){throw 'Helper was not the dedicated verified build'}
        $started=Get-Content -Raw -LiteralPath (Join-Path $jobRoot 'helper-started.json')|ConvertFrom-Json
        $worker=Get-Process -Id $started.Pid -ErrorAction SilentlyContinue
        if($worker -and $worker.Path -ne $helper){throw 'Helper process identity changed'}
        if($worker -and !$worker.WaitForExit(50000)){throw 'Dedicated updater timed out; preserve evidence'}
        $result=Get-Content -LiteralPath (Join-Path $jobRoot 'result.json') -Raw|ConvertFrom-Json;if($result.Status -ne $case.Expected){throw ('Unexpected self-update result: '+($result|ConvertTo-Json -Compress))}
        if($case.Expected -eq 'installed'){
            if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $Executable).Hash){throw 'New executable differs'}
            if((Get-FileHash -LiteralPath ($target+'.astera-rollback.exe')).Hash -ne $before){throw 'Previous executable backup lost'}
            $child=Get-Process -Id $result.NewPid -ErrorAction SilentlyContinue;if($child -and $child.Path -eq $target){Stop-Process -Id $child.Id;$child.WaitForExit()};$checks.Add('Dedicated verified helper waits for parent, atomically installs and restarts real Astera with startup health handshake')
        }else{if((Get-FileHash -LiteralPath $target).Hash -ne $before){throw 'Startup failure did not restore original executable'};$checks.Add('A signed build that exits during startup automatically rolls back to the prior executable')}
        if((Get-FileHash -LiteralPath (Join-Path $data 'preferences.json')).Hash -ne $prefsHash -or (Get-FileHash -LiteralPath (Join-Path $folder 'personal-game-save.txt')).Hash -ne $saveHash){throw 'Preferences or game save changed'}
    }
    $checks.Add('The extracted helper matches its pinned build hash and is not a copy of the launcher')
    $checks.Add('Settings and unrelated game-save fixture remain byte-for-byte unchanged')
    $healthRoot=Join-Path $AuditRoot 'confirmation-lock';$health=Start-Process -FilePath $Executable -ArgumentList @('--self-update-health-audit',('"'+$healthRoot+'"')) -WindowStyle Hidden -PassThru -Wait;if($health.ExitCode -ne 0){throw 'Startup confirmation lock regression failed'};$healthReport=Get-Content -LiteralPath (Join-Path $healthRoot 'result.json') -Raw|ConvertFrom-Json;if(!$healthReport.Passed){throw 'Startup confirmation lock regression did not pass'};foreach($check in $healthReport.Checks){$checks.Add($check)}
    @{Passed=$true;Checks=@($checks);LiveGamesChanged=$false;LiveAppReplaced=$false;GitHubContacted=$false;CandidateSha256=(Get-FileHash -LiteralPath $Executable).Hash}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $AuditRoot 'result.json')
    Write-Output ('SELF_UPDATE_TESTS_PASSED: '+$checks.Count+' checks')
}finally{$rsa.Dispose()}
