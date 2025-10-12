Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build InsuranceSemantic.sln

Write-Host "Running tests with diagnostic logging..." -ForegroundColor Cyan
dotnet test ConversaCore.Tests/ConversaCore.Tests.csproj --no-build --verbosity diagnostic --logger "console;verbosity=detailed"