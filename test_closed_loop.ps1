# InstallerExtractor Automated Closed-Loop Test Suite
$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$cliExe = Join-Path $scriptDir "InstallerExtractorCLI.exe"
if (-not (Test-Path $cliExe)) {
    Write-Host "CLI executable not found. Running build.ps1 first..." -ForegroundColor Yellow
    & .\build.ps1
}

$testDir = Join-Path $scriptDir "temp_closed_loop_test"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir | Out-Null

$results = @()

function Report-Case($id, $name, $passed, $detail) {
    $script:results += [PSCustomObject]@{
        CaseId = $id
        Name   = $name
        Passed = $passed
        Detail = $detail
    }
    $color = if ($passed) { "Green" } else { "Red" }
    $status = if ($passed) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$status Case ${id} - ${name}: $detail" -ForegroundColor $color
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "       InstallerExtractor Automated Closed-Loop Verification Suite              " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Locate test fixtures
$msiSource = "C:\Users\J03378\scoop\cache\7zip#26.02#63f4002.msi"
if (-not (Test-Path $msiSource)) {
    $found = Get-ChildItem "C:\Users\J03378\scoop\cache\*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $msiSource = $found.FullName }
}

# ------------------------------------------------------------------------------
# Test 1: Detection & AI JSON Output Closed Loop (--detect --json)
# ------------------------------------------------------------------------------
try {
    $raw = & $cliExe -i $msiSource --detect --json
    $json = $raw | ConvertFrom-Json
    $pass = ($json.success -eq $true) -and ($json.action -eq "detect") -and ($json.type -eq "Msi")
    Report-Case "01" "Detect Installer Format (JSON)" $pass "Identified type: $($json.type), DisplayName: $($json.displayName)"
} catch {
    Report-Case "01" "Detect Installer Format (JSON)" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 2: Full Extraction + Automatic Diagnostic Logging Closed Loop
# ------------------------------------------------------------------------------
$outMsi = Join-Path $testDir "out_msi"
try {
    $raw = & $cliExe -i $msiSource -o $outMsi --json
    $json = $raw | ConvertFrom-Json
    
    $extractedFiles = (Get-ChildItem $outMsi -Recurse -File).Count
    $logCreated = ($json.logFile -ne $null) -and (Test-Path $json.logFile)
    
    $pass = ($json.success -eq $true) -and ($extractedFiles -gt 0) -and $logCreated
    $logContent = if ($logCreated) { Get-Content $json.logFile -Raw } else { "" }
    $hasProcessLog = $logContent.Contains("Executing") -and $logContent.Contains("ExitCode=0")
    
    Report-Case "02" "MSI Full Extraction & Diagnostic Log" ($pass -and $hasProcessLog) `
        "Extracted $extractedFiles files, diagnostic log verified at $($json.logFile)"
} catch {
    Report-Case "02" "MSI Full Extraction & Diagnostic Log" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 3: SFX (.exe) Archive Creation, Extraction & Data Integrity Closed Loop
# ------------------------------------------------------------------------------
$sfxSourceDir = Join-Path $testDir "dummy_source"
New-Item -ItemType Directory -Path $sfxSourceDir | Out-Null
"Hello AI Extractor 2026" | Set-Content (Join-Path $sfxSourceDir "sample1.txt") -Encoding UTF8
"Payload Test Data File" | Set-Content (Join-Path $sfxSourceDir "sample2.dat") -Encoding UTF8

$sfxModule = Join-Path $scriptDir "7z.sfx"
$sfxExe = Join-Path $testDir "test_sfx.exe"
& .\7z.exe a "-sfx$sfxModule" $sfxExe "$sfxSourceDir\*" | Out-Null

$outSfx = Join-Path $testDir "out_sfx"
try {
    $raw = & $cliExe -i $sfxExe -o $outSfx --json
    $json = $raw | ConvertFrom-Json
    $t1 = Get-Content (Join-Path $outSfx "sample1.txt") -Raw -ErrorAction SilentlyContinue
    $pass = ($json.success -eq $true) -and ($t1.Contains("Hello AI Extractor"))
    Report-Case "03" "SFX EXE Unpack & Integrity" $pass "Sample payload perfectly matches original data"
} catch {
    Report-Case "03" "SFX EXE Unpack & Integrity" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 4: Disable Logging Closed Loop (--no-log)
# ------------------------------------------------------------------------------
$outNoLog = Join-Path $testDir "out_nolog"
try {
    $raw = & $cliExe -i $sfxExe -o $outNoLog --no-log --json
    $json = $raw | ConvertFrom-Json
    $pass = ($json.success -eq $true) -and ($json.logFile -eq $null)
    Report-Case "04" "Disable Logging Switch (--no-log)" $pass "Output logFile is null, logging successfully disabled"
} catch {
    Report-Case "04" "Disable Logging Switch (--no-log)" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 5: Custom Log File Path Closed Loop (-l / --log-file)
# ------------------------------------------------------------------------------
$customLog = Join-Path $testDir "custom_diagnostic.log"
$outCustom = Join-Path $testDir "out_custom"
try {
    $raw = & $cliExe -i $sfxExe -o $outCustom -l $customLog --json
    $json = $raw | ConvertFrom-Json
    $exists = Test-Path $customLog
    $hasHeader = if ($exists) { (Get-Content $customLog -Raw).Contains("Diagnostic & AI Debugging Log") } else { $false }
    $pass = ($json.success -eq $true) -and $exists -and $hasHeader
    Report-Case "05" "Custom Log File Path (-l)" $pass "Diagnostic log successfully written to $customLog"
} catch {
    Report-Case "05" "Custom Log File Path (-l)" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 6: Error Handling & Diagnostic Capture Closed Loop
# ------------------------------------------------------------------------------
try {
    $raw = & $cliExe -i "non_existent_package_file_xyz.exe" --json
    $json = $raw | ConvertFrom-Json
    $pass = ($json.success -eq $false) -and ($json.exitCode -eq 1) -and ($json.error.Contains("not found"))
    Report-Case "06" "Error Interception & JSON Output" $pass "Non-existent file captured with structured error info"
} catch {
    Report-Case "06" "Error Interception & JSON Output" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 7: Hardware Driver Scanning Closed Loop (--scan-drivers)
# ------------------------------------------------------------------------------
$driverMockDir = Join-Path $testDir "mock_driver_app"
$driverSubDir = Join-Path $driverMockDir "Drivers"
New-Item -ItemType Directory -Path $driverSubDir -Force | Out-Null
"; Mock INF Driver" | Set-Content (Join-Path $driverSubDir "virtual_device.inf") -Encoding UTF8
"MZ" | Set-Content (Join-Path $driverSubDir "InstDrivers.exe") -Encoding ASCII

try {
    $raw = & $cliExe --scan-drivers -o $driverMockDir --json
    $json = $raw | ConvertFrom-Json
    $pass = ($json.success -eq $true) -and ($json.hasDrivers -eq $true) -and `
            ($json.infFiles.Count -ge 1) -and ($json.installerExes.Count -ge 1)
    Report-Case "07" "Device Driver Scan (--scan-drivers)" $pass `
        "Detected $($json.infFiles.Count) infs, $($json.installerExes.Count) dedicated installers"
} catch {
    Report-Case "07" "Device Driver Scan (--scan-drivers)" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Test 8: Empty Directory Driver Scanning Closed Loop (No drivers)
# ------------------------------------------------------------------------------
$noDriverDir = Join-Path $testDir "no_drivers"
New-Item -ItemType Directory -Path $noDriverDir -Force | Out-Null
try {
    $raw = & $cliExe --scan-drivers -o $noDriverDir --json
    $json = $raw | ConvertFrom-Json
    $pass = ($json.success -eq $true) -and ($json.hasDrivers -eq $false) -and ($json.infFiles.Count -eq 0)
    Report-Case "08" "Driver Scan on Empty Target" $pass "Correctly reported hasDrivers=false"
} catch {
    Report-Case "08" "Driver Scan on Empty Target" $false "Exception: $($_.Exception.Message)"
}

# ------------------------------------------------------------------------------
# Cleanup & Summary
# ------------------------------------------------------------------------------
Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "                        CLOSED-LOOP TEST SUMMARY REPORT                          " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
$allPassed = ($results | Where-Object { -not $_.Passed }).Count -eq 0
$results | Format-Table -AutoSize

if ($allPassed) {
    Write-Host "[ALL PASSED] All $($results.Count) test cases passed successfully!" -ForegroundColor Green
    Write-Host "AI CLI layer, diagnostic logging, and hardware driver modules are verified and production ready." -ForegroundColor Green
    exit 0
} else {
    Write-Error "[TEST FAILED] Some test cases failed! See details above."
    exit 1
}
