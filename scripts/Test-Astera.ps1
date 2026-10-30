#Requires -Version 7.2
[CmdletBinding()]
param(
    [string]$TestRoot = ('D:/vale/StarsAllies-Storage/audits/astera-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$BuildDirectory = '',
    [switch]$UseExistingBuild,
    [string[]]$Only = @(),
    [switch]$ListChecks,
    [switch]$ContinueOnFailure,
    [string]$PrivateKeyFile = 'D:/vale/StarsAllies-Storage/keys/Astera/publisher.key'
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$providers = @(
    @{ Name = 'network'; Flag = '--self-test' }
    @{ Name = 'full-pack'; Flag = '--full-pack-self-test' }
    @{ Name = 'install-locks'; Flag = '--install-lock-audit' }
    @{ Name = 'engine'; Flag = '--engine-self-test' }
    @{ Name = 'layout'; Flag = '--layout-audit' }
    @{ Name = 'library-features'; Flag = '--library-features-audit' }
    @{ Name = 'animated-gif'; Flag = '--gif-audit' }
    @{ Name = 'library-paint'; Flag = '--library-paint-audit' }
    @{ Name = 'buffer-offset'; Flag = '--buffer-offset-audit' }
    @{ Name = 'library-icons'; Flag = '--library-icons-audit' }
    @{ Name = 'library-wheel'; Flag = '--library-wheel-audit' }
    @{ Name = 'library-organization'; Flag = '--library-organization-audit' }
    @{ Name = 'itch-launch'; Flag = '--itch-launch-audit' }
    @{ Name = 'activity'; Flag = '--activity-audit' }
    @{ Name = 'app-update-flow'; Flag = '--app-update-flow-audit' }
    @{ Name = 'managed-games'; Flag = '--managed-game-audit' }
    @{ Name = 'primary-transition'; Flag = '--primary-transition-audit' }
    @{ Name = 'discover'; Flag = '--discover-audit' }
    @{ Name = 'thunderstore'; Flag = '--thunderstore-audit' }
    @{ Name = 'startup'; Flag = '--startup-audit' }
    @{ Name = 'motion'; Flag = '--motion-audit' }
    @{ Name = 'display-polish'; Flag = '--display-polish-audit' }
    @{ Name = 'youtube'; Flag = '--youtube-audit' }
    @{ Name = 'roblox'; Flag = '--roblox-audit' }
    @{ Name = 'shutdown'; Flag = '--shutdown-audit' }
    @{ Name = 'folders'; Flag = '--folder-picker-audit' }
    @{ Name = 'multiverse'; Flag = '--multiverse-self-test' }
    @{ Name = 'ftl-progress'; Flag = '--ftl-progress-audit' }
    @{ Name = 'prism'; Flag = '--prism-audit' }
    @{ Name = 'minecraft-profiles'; Flag = '--minecraft-profiles-audit' }
    @{ Name = 'minecraft-launch'; Flag = '--minecraft-launch-audit' }
    @{ Name = 'managed-library'; Flag = '--managed-library-audit' }
    @{ Name = 'runtime-health'; Flag = '--runtime-health-audit' }
)
$knownNames = @($providers.Name) + @('publisher', 'release-gate')
if ($ListChecks) { $knownNames; return }
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$unknown = @($Only | Where-Object { $_ -notin $knownNames })
if ($unknown.Count) { throw ('Unknown checks: ' + ($unknown -join ', ')) }
$focused = $Only.Count -gt 0
$selected = if ($focused) { @($providers | Where-Object Name -in $Only) } else { $providers }
$runPublisher = !$focused -or 'publisher' -in $Only
if ($runPublisher -and 'network' -notin $selected.Name) {
    # Publisher checks operate only on the synthetic profile made by network checks.
    $selected = @($providers | Where-Object Name -eq 'network') + $selected
}
if (!$BuildDirectory) {
    $BuildDirectory = 'D:/vale/StarsAllies-Storage/builds/Astera/' + (Get-Content (Join-Path $sourceRoot 'VERSION') -Raw).Trim()
}
if (Test-Path -LiteralPath $TestRoot) { throw 'Use a fresh test directory; preserve previous evidence' }
New-Item -ItemType Directory -Path $TestRoot | Out-Null
$exe = Join-Path $BuildDirectory 'Astera.exe'
if ($UseExistingBuild) {
    $build = Get-Content (Join-Path $BuildDirectory 'build-receipt.json') -Raw | ConvertFrom-Json
    if ($build.Sha256 -ne (Get-FileHash $exe).Hash -or $build.Version -ne (Get-Content (Join-Path $sourceRoot 'VERSION') -Raw).Trim()) {
        throw 'Existing build does not match its receipt/version'
    }
    $inputs = @(Get-ChildItem (Join-Path $sourceRoot 'src') -File -Filter '*.cs') +
        @(Get-ChildItem (Join-Path $sourceRoot 'updater') -File -Filter '*.cs') +
        @(Get-ChildItem (Join-Path $sourceRoot 'assets') -File -Recurse) +
        @(Get-Item (Join-Path $sourceRoot 'app-release-notes.md'))
    if ($inputs | Where-Object LastWriteTimeUtc -gt (Get-Item $exe).LastWriteTimeUtc) {
        throw 'Runtime inputs changed after this build; rebuild before testing'
    }
} else {
    & (Join-Path $PSScriptRoot 'Build-Astera.ps1') -OutputDirectory $BuildDirectory
}
$reports = @()
$failures = @()
foreach ($provider in $selected) {
    try {
    $folder = Join-Path $TestRoot $provider.Name
    Write-Host ('Checking ' + $provider.Name + '...')
    $process = Start-Process -FilePath $exe -ArgumentList @($provider.Flag, ('"' + $folder + '"')) -WindowStyle Hidden -PassThru
    try {
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw ($provider.Name + ' timed out') }
        if ($process.ExitCode -ne 0) {
            Get-Content (Join-Path $folder 'failure.txt') -ErrorAction SilentlyContinue
            throw ($provider.Name + ' failed')
        }
    } finally { $process.Dispose() }
    $report = Get-Content (Join-Path $folder 'result.json') -Raw | ConvertFrom-Json
    if (!$report.Passed) { throw ($provider.Name + ' did not certify success') }
    $reports += @{ Name = $provider.Name; Checks = $report.Checks; Directory = $folder }
    } catch {
        if (!$ContinueOnFailure) { throw }
        $failures += @{ Name = $provider.Name; Error = $_.Exception.Message }
        Write-Warning ($provider.Name + ': ' + $_.Exception.Message)
    }

}
if ($runPublisher) {
    & (Join-Path $PSScriptRoot 'Test-Publisher.ps1') -FixtureProfile (Join-Path $TestRoot 'network/profile') -TestDirectory (Join-Path $TestRoot 'publisher') -PrivateKeyFile $PrivateKeyFile
    $publisher = Get-Content (Join-Path $TestRoot 'publisher/result.json') -Raw | ConvertFrom-Json
    if (!$publisher.Passed -or $publisher.LiveGitHubContacted) { throw 'Publisher mock did not certify offline success' }
    foreach ($name in @('fixture-1', 'fixture-2')) {
        $feed = Join-Path $TestRoot ('publisher/' + $name + '/minecraft-squared.json')
        $verify = Start-Process -FilePath $exe -ArgumentList @('--verify-feed', ('"' + $feed + '"')) -WindowStyle Hidden -PassThru -Wait
        try { if ($verify.ExitCode -ne 0) { throw 'App rejected published fixture' } }
        finally { $verify.Dispose() }
    }
    $reports += @{ Name = 'publisher'; Checks = $publisher.Checks; Directory = (Join-Path $TestRoot 'publisher') }
}
if (!$focused -or 'release-gate' -in $Only) {
    $gate = & (Join-Path $PSScriptRoot 'Test-ReleaseTestGate.ps1')
    $reports += @{ Name = 'release-gate'; Checks = $gate.Checks; Directory = $TestRoot }
}
$receipt = @{
    Passed = $failures.Count -eq 0
    FailedChecks = $failures
    CompleteRegressionSuite = !$focused
    Scope = $(if ($focused) { 'focused' } else { 'full' })
    AppSha256 = (Get-FileHash $exe).Hash
    Tests = $reports
    GroupedChecks = ($reports | ForEach-Object { $_.Checks.Count } | Measure-Object -Sum).Sum
    LiveProfileChanged = $false
    LiveGitHubContacted = $false
    CreatedUtc = [DateTime]::UtcNow.ToString('o')
}
$receipt | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $TestRoot 'result.json') -Encoding utf8
if ($failures.Count) { throw ('Checks failed; see result.json: ' + ($failures.Name -join ', ')) }
Write-Host ('ASTERA_TESTS_PASSED: ' + $receipt.GroupedChecks + ' groups; scope=' + $receipt.Scope)
