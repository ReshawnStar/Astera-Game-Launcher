#Requires -Version 7.2
param(
    [string]$TestRoot=('D:/vale/StarsAllies-Storage/audits/astera-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$BuildDirectory='D:/vale/StarsAllies-Storage/builds/Astera/0.1.3',
    [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key'
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $TestRoot){throw 'Use a new test directory; preserve previous test evidence'}
New-Item -ItemType Directory -Path $TestRoot|Out-Null
& (Join-Path $PSScriptRoot 'Build-Astera.ps1') -OutputDirectory $BuildDirectory
$exe=Join-Path $BuildDirectory 'Astera.exe'
$reports=@()
foreach($item in @(@{Name='network';Flag='--self-test'},@{Name='engine';Flag='--engine-self-test'},@{Name='layout';Flag='--layout-audit'})){
    $folder=Join-Path $TestRoot $item.Name
    $process=Start-Process -FilePath $exe -ArgumentList @($item.Flag,('"'+$folder+'"')) -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(60000)){$process.Kill();throw ($item.Name+' test timed out')}
    if($process.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $folder 'failure.txt');throw ($item.Name+' test failed')}
    $report=Get-Content -Raw -LiteralPath (Join-Path $folder 'result.json')|ConvertFrom-Json
    if(!$report.Passed){throw 'Test did not certify success'}
    $reports+=@{Name=$item.Name;Checks=$report.Checks;Directory=$folder}
}
& (Join-Path $PSScriptRoot 'Test-Publisher.ps1') -FixtureProfile (Join-Path $TestRoot 'network/profile') -TestDirectory (Join-Path $TestRoot 'publisher') -PrivateKeyFile $PrivateKeyFile
$publisher=Get-Content -Raw -LiteralPath (Join-Path $TestRoot 'publisher/result.json')|ConvertFrom-Json
if(!$publisher.Passed -or $publisher.LiveGitHubContacted){throw 'Publisher mock did not certify offline success'}
foreach($name in @('fixture-1','fixture-2')){$feed=Join-Path $TestRoot ('publisher/'+$name+'/minecraft-squared.json');$verify=Start-Process -FilePath $exe -ArgumentList @('--verify-feed',('"'+$feed+'"')) -WindowStyle Hidden -PassThru -Wait;if($verify.ExitCode -ne 0){Get-Content -LiteralPath ($feed+'.failure.txt');throw 'Published fixture was rejected by the actual app'}}
$reports+=@{Name='publisher';Checks=$publisher.Checks;Directory=(Join-Path $TestRoot 'publisher')}
$receipt=@{Passed=$true;AppSha256=(Get-FileHash -LiteralPath $exe).Hash;Tests=$reports;GroupedChecks=($reports|ForEach-Object{$_.Checks.Count}|Measure-Object -Sum).Sum;LiveProfileChanged=$false;LiveGitHubContacted=$false;CreatedUtc=[DateTime]::UtcNow.ToString('o')}
$receipt|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $TestRoot 'result.json')
Write-Output ('ASTERA_TESTS_PASSED: '+$receipt.GroupedChecks+' grouped checks. Receipt: '+(Join-Path $TestRoot 'result.json'))
