Write-Host "Clearing Visual Studio Test Explorer cache..." -ForegroundColor Cyan

# Stop VS processes if needed
# $vsProcesses = Get-Process | Where-Object { $_.ProcessName -like "*devenv*" }
# if ($vsProcesses) {
#     Write-Host "Stopping Visual Studio instances..."
#     $vsProcesses | ForEach-Object { $_.Kill() }
# }

# Clear Test Explorer cache
$testCachePaths = @(
    "$env:TEMP\VisualStudioTestExplorerExtensions",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\TestExplorer"
)

foreach ($path in $testCachePaths) {
    if (Test-Path $path) {
        Write-Host "Removing $path..."
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Rebuild the solution
Write-Host "Rebuilding solution..." -ForegroundColor Cyan
dotnet clean
dotnet build

Write-Host "Done! Please restart Visual Studio for the changes to take effect." -ForegroundColor Green
Write-Host "After restart, go to Test > Test Explorer and click the 'Run All Tests' button." -ForegroundColor Green