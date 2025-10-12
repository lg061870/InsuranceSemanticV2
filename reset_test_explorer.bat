@echo off
echo Cleaning solution...
dotnet clean InsuranceSemantic.sln

echo Building solution...
dotnet build InsuranceSemantic.sln

echo Clearing VS test cache...
rmdir /s /q %TEMP%\VisualStudioTestExplorerExtensions 2>nul
rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\TestExplorer" 2>nul

echo Done! Please restart Visual Studio for changes to take effect.
pause