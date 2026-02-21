param(
    [Parameter(Mandatory = $false)]
    [string]$OutputRoot = "$(Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')) 'artifacts\templates')",

    [Parameter(Mandatory = $false)]
    [string]$TemplateName = "conversacore-blazor",

    [Parameter(Mandatory = $false)]
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$repoRoot = Resolve-Path (Join-Path $projectRoot '..\..')
$sourceProjectDir = $projectRoot
$templatesRoot = Join-Path $repoRoot 'templates'
$templateDir = Join-Path $templatesRoot $TemplateName
$outDir = Resolve-Path -LiteralPath $OutputRoot -ErrorAction SilentlyContinue
if (-not $outDir) {
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
    $outDir = Resolve-Path -LiteralPath $OutputRoot
}

if (-not (Test-Path $sourceProjectDir)) {
    throw "Source project folder not found: $sourceProjectDir"
}

# Clean template folder
if (Test-Path $templateDir) {
    Remove-Item -Recurse -Force $templateDir
}
New-Item -ItemType Directory -Force -Path $templateDir | Out-Null

# Copy project files
robocopy $sourceProjectDir $templateDir /E /XD bin obj .vs logs templates artifacts | Out-Null

# Copy template config (stored in the project under templates/<TemplateName>/.template.config)
$templateConfigSource = Join-Path $sourceProjectDir "templates\$TemplateName\.template.config"
$templateConfigTarget = Join-Path $templateDir '.template.config'
if (Test-Path $templateConfigSource) {
    New-Item -ItemType Directory -Force -Path $templateConfigTarget | Out-Null
    Copy-Item -Force -Recurse (Join-Path $templateConfigSource '*') $templateConfigTarget
}

# Ensure template.json exists (repo should contain it)
$templateJson = Join-Path $templateDir '.template.config\template.json'
if (-not (Test-Path $templateJson)) {
    throw "Missing template.json at $templateJson. Ensure templates/$TemplateName/.template.config/template.json exists in repo and is not nested."
}

# Create a .nupkg-like installable folder isn't required; dotnet new can install from folder.
# But produce a zip as a portable artifact.
$zipPath = Join-Path $outDir "$TemplateName.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($templateDir, $zipPath)

Write-Host "Template staged at: $templateDir"
Write-Host "Template zip created: $zipPath"

if ($Install) {
    Write-Host "Installing template via dotnet new install..."
    dotnet new install $templateDir
    Write-Host "Installed. Try: dotnet new conversacore-blazor -o C:\temp\MyApp"
}
