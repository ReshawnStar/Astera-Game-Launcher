# Shared, side-effect-free validation. Focused checks can never authorize publication.
function Assert-AsteraReleaseTests {
    param($Regression, $Updater, [string]$AppHash, [switch]$AllowUnavailableScreenCapture)
    if (!$Updater.Passed -or $Regression.AppSha256 -ne $AppHash -or $Updater.CandidateSha256 -ne $AppHash) {
        throw 'Test receipts must match the exact executable and updater checks must pass'
    }
    if ($AllowUnavailableScreenCapture) {
        $unresolved = @($Regression.UnresolvedChecks)
        if ($Regression.Passed -or !$Regression.NonScreenChecksPassed -or $Regression.CompleteRegressionSuite -or
            $unresolved.Count -ne 2 -or @($Regression.Tests).Count -lt 27 -or
            @($unresolved | Where-Object {
                $_ -notmatch '^(library-paint: native screen frames turn solid black|motion: native covered-page sample failed while capture was unavailable)$'
            }).Count) {
            throw 'Testing exception is restricted to the two previously approved unavailable capture groups'
        }
    } elseif (!$Regression.Passed -or $Regression.CompleteRegressionSuite -ne $true) {
        throw 'Publication requires a complete passing regression suite, not focused checks'
    }
}
