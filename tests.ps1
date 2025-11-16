param(
    [string]$ProjectPath = ".\ConversaCore.Tests",
    [switch]$OpenReports = $true
)

Write-Host "`n🔍 Scanning test files in $ProjectPath..."
$testFiles = Get-ChildItem -Path $ProjectPath -Filter "*Tests.cs" -Recurse
if ($testFiles.Count -eq 0) {
    Write-Host "⚠️  No test files found under $ProjectPath" -ForegroundColor Yellow
    exit 1
}

Write-Host ("   Found {0} test files:`n" -f $testFiles.Count)
$testFiles | ForEach-Object { Write-Host "   • $($_.Name)" }

# ----------------------------------------------------------------------
# Wait for file to exist and stabilize
# ----------------------------------------------------------------------
function Wait-ForFile {
    param([string]$Path)
    $attempts = 0
    while (-not (Test-Path $Path) -and $attempts -lt 20) {
        Start-Sleep -Milliseconds 250
        $attempts++
    }
    # wait until file size stops growing
    $lastSize = -1
    for ($i=0; $i -lt 10; $i++) {
        if (Test-Path $Path) {
            $size = (Get-Item $Path).Length
            if ($size -eq $lastSize) { return }
            $lastSize = $size
        }
        Start-Sleep -Milliseconds 250
    }
}

# ----------------------------------------------------------------------
# Parse results from TRX or HTML
# ----------------------------------------------------------------------
function Parse-TestResults {
    param([string]$trxPath, [string]$htmlPath)

    $stats = @{ Total=0; Passed=0; Failed=0; Skipped=0; FailedTests=@() }

    # Prefer TRX
    if (Test-Path $trxPath) {
        try {
            [xml]$trxXml = Get-Content $trxPath -ErrorAction Stop
            $stats.Total  = ($trxXml.TestRun.Results.UnitTestResult).Count
            $stats.Failed = ($trxXml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "Failed" }).Count
            $stats.Passed = ($trxXml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "Passed" }).Count
            $stats.Skipped= ($trxXml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "NotExecuted" }).Count
            return $stats
        }
        catch {
            Write-Host "⚠️  TRX parse failed, will read HTML instead..." -ForegroundColor Yellow
        }
    }

    # Parse HTML if available
    if (Test-Path $htmlPath) {
        Wait-ForFile $htmlPath
        $html = Get-Content $htmlPath -Raw

        # Extract stats
        $stats.Total  = if ($html -match '<div class="total-tests">(\d+)</div>') { [int]$matches[1] } else { 0 }
        $stats.Passed = if ($html -match '<span class="passedTests">(\d+)</span>') { [int]$matches[1] } else { 0 }
        $stats.Failed = if ($html -match '<span class="failedTests">(\d+)</span>') { [int]$matches[1] } else { 0 }
        $stats.Skipped= if ($html -match '<span class="skippedTests">(\d+)</span>') { [int]$matches[1] } else { 0 }

        # Failed test messages
        $pattern = '✘\s*([A-Za-z0-9_.]+)\s*.*?Error:\s*<span class="error-message"><pre>(.*?)</pre>'
        $matchesAll = [regex]::Matches($html, $pattern, 'Singleline')
        foreach ($m in $matchesAll) {
            $stats.FailedTests += @{
                Name = $m.Groups[1].Value
                Message = ($m.Groups[2].Value -replace '\s+', ' ').Trim()
            }
        }
    }

    return $stats
}

# ----------------------------------------------------------------------
# Run a single test group
# ----------------------------------------------------------------------
function Run-TestGroup {
    param([string]$Filter, [string]$Name)

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $resultsDir = Join-Path $ProjectPath "TestResults"
    if (!(Test-Path $resultsDir)) { New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null }

    $htmlLog = Join-Path $resultsDir "$($Name)_$timestamp.html"
    $trxLog  = Join-Path $resultsDir "$($Name)_$timestamp.trx"

    Write-Host "`n🔹 Running test group: $Name"
    Write-Host "   Filter: $Filter"
    Write-Host "   Logging: $htmlLog`n"

    dotnet test $ProjectPath `
        --filter $Filter `
        --logger "html;LogFileName=$htmlLog" `
        --logger "trx;LogFileName=$trxLog" `
        --nologo | Out-Null

    Wait-ForFile $htmlLog
    $parsed = Parse-TestResults -trxPath $trxLog -htmlPath $htmlLog

    [PSCustomObject]@{
        Name       = $Name
        Total      = $parsed.Total
        Passed     = $parsed.Passed
        Failed     = $parsed.Failed
        Skipped    = $parsed.Skipped
        FailedTests= $parsed.FailedTests
        HtmlReport = $htmlLog
    }
}

# ----------------------------------------------------------------------
# Execute all test files
# ----------------------------------------------------------------------
$summary = @()
foreach ($file in $testFiles) {
    $className = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $summary += Run-TestGroup -Filter "FullyQualifiedName~$className" -Name $className
}

# ----------------------------------------------------------------------
# Print results summary
# ----------------------------------------------------------------------
Write-Host "`n==================== TEST SUMMARY ===================="
foreach ($r in $summary) {
    $color = if ($r.Failed -gt 0) { "Red" } elseif ($r.Total -eq 0) { "DarkGray" } else { "Green" }
    Write-Host ("{0,-35} Passed: {1,-3}  Failed: {2,-3}  Total: {3,-3}" -f $r.Name, $r.Passed, $r.Failed, $r.Total) -ForegroundColor $color

    if ($r.FailedTests.Count -gt 0) {
        foreach ($f in $r.FailedTests) {
            Write-Host ("   ✘ {0}" -f $f.Name) -ForegroundColor Yellow
            Write-Host ("     → {0}" -f $f.Message.Substring(0, [Math]::Min($f.Message.Length, 100))) -ForegroundColor DarkGray
        }
    }
}

# ----------------------------------------------------------------------
# Open reports (optional)
# ----------------------------------------------------------------------
if ($OpenReports) {
    foreach ($report in $summary.HtmlReport) {
        if (Test-Path $report) { Start-Process $report }
    }
}

Write-Host "`n✅ All test groups completed. Reports saved under: $(Join-Path $ProjectPath 'TestResults')"
