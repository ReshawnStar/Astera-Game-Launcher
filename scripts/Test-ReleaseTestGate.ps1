$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseTestGate.ps1')
$hash = 'fixture-hash'
$full = @{ Passed = $true; CompleteRegressionSuite = $true; AppSha256 = $hash }
$updater = @{ Passed = $true; CandidateSha256 = $hash }
Assert-AsteraReleaseTests $full $updater $hash
$checks = @('Complete matching regression/updater receipts are accepted')
foreach ($case in @(
    @{ Name = 'focused receipt'; Regression = @{ Passed = $true; CompleteRegressionSuite = $false; AppSha256 = $hash }; Updater = $updater }
    @{ Name = 'missing full-suite marker'; Regression = @{ Passed = $true; AppSha256 = $hash }; Updater = $updater }
    @{ Name = 'failed regression'; Regression = @{ Passed = $false; CompleteRegressionSuite = $true; AppSha256 = $hash }; Updater = $updater }
    @{ Name = 'mismatched binary'; Regression = @{ Passed = $true; CompleteRegressionSuite = $true; AppSha256 = 'wrong' }; Updater = $updater }
    @{ Name = 'failed updater'; Regression = $full; Updater = @{ Passed = $false; CandidateSha256 = $hash } }
)) {
    $rejected = $false
    try { Assert-AsteraReleaseTests $case.Regression $case.Updater $hash } catch { $rejected = $true }
    if (!$rejected) { throw ('Gate accepted ' + $case.Name) }
    $checks += ('Publication rejects ' + $case.Name)
}
@{ Passed = $true; Checks = $checks }
