#!/usr/bin/env pwsh
<#
.SYNOPSIS
Build script for Cross-Platform Input Sharing Software

.DESCRIPTION
This script builds, tests, and packages the CrossInputShare solution.
It supports different build configurations and target platforms.

.PARAMETER Configuration
Build configuration (Debug, Release, Testing)

.PARAMETER Platform
Target platform (Windows, Linux, Android - planned)

.PARAMETER Clean
Clean build outputs before building

.PARAMETER Test
Run tests after building

.PARAMETER Package
Create deployment packages

.PARAMETER CodeAnalysis
Run code analysis and security checks

.EXAMPLE
./build.ps1 -Configuration Release -Test -Package
Builds release version, runs tests, and creates packages

.EXAMPLE
./build.ps1 -Clean -Configuration Debug
Cleans and builds debug version
#>

param(
    [ValidateSet("Debug", "Release", "Testing")]
    [string]$Configuration = "Debug",
    
    [ValidateSet("Windows", "Linux", "Android")]
    [string]$Platform = "Windows",
    
    [switch]$Clean,
    [switch]$Test,
    [switch]$Package,
    [switch]$CodeAnalysis,
    [switch]$Help
)

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

# Set error handling
$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0

# Configuration
$SolutionPath = "src/CrossInputShare.sln"
$TestProjectPath = "src/CrossInputShare.Tests/CrossInputShare.Tests.csproj"
$OutputDir = "dist/$Configuration"
$TestResultsDir = "TestResults"
$PackagesDir = "packages"

# Dotnet version check
function Test-DotnetVersion {
    Write-Host "Checking .NET SDK version..." -ForegroundColor Cyan
    $dotnetVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK is not installed or not in PATH"
    }
    Write-Host "Using .NET SDK $dotnetVersion" -ForegroundColor Green
    
    # Check for required workloads
    $workloads = dotnet workload list
    if ($Platform -eq "Windows" -and $workloads -notmatch "microsoft-windowsdesktop") {
        Write-Warning "Windows desktop workload may not be installed. Run: dotnet workload install windowsdesktop"
    }
}

# Clean build outputs
function Invoke-Clean {
    Write-Host "Cleaning build outputs..." -ForegroundColor Cyan
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force
    }
    if (Test-Path $TestResultsDir) {
        Remove-Item -Path $TestResultsDir -Recurse -Force
    }
    if (Test-Path $PackagesDir) {
        Remove-Item -Path $PackagesDir -Recurse -Force
    }
    
    # Clean obj and bin directories
    Get-ChildItem -Path "src" -Recurse -Directory | 
        Where-Object { $_.Name -in ("obj", "bin") } |
        ForEach-Object {
            Write-Host "Cleaning $($_.FullName)" -ForegroundColor Yellow
            Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    
    Write-Host "Clean completed" -ForegroundColor Green
}

# Restore dependencies
function Invoke-Restore {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    dotnet restore $SolutionPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore packages"
    }
    Write-Host "Restore completed" -ForegroundColor Green
}

# Build solution
function Invoke-Build {
    Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
    
    $buildArgs = @(
        $SolutionPath,
        "-c", $Configuration,
        "--no-restore"
    )
    
    if ($Platform -eq "Windows") {
        # Windows-specific build
        dotnet build @buildArgs
    } elseif ($Platform -eq "Linux") {
        # Linux build (cross-platform)
        dotnet build @buildArgs -r linux-x64
    } elseif ($Platform -eq "Android") {
        # Android build (future)
        Write-Warning "Android build not yet implemented"
        return
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    
    Write-Host "Build completed" -ForegroundColor Green
}

# Run tests
function Invoke-Test {
    Write-Host "Running tests..." -ForegroundColor Cyan
    
    # Create test results directory
    New-Item -ItemType Directory -Path $TestResultsDir -Force | Out-Null
    
    $testArgs = @(
        "test",
        $TestProjectPath,
        "-c", $Configuration,
        "--no-build",
        "--verbosity", "normal",
        "--logger", "trx",
        "--results-directory", $TestResultsDir,
        "--collect", "XPlat Code Coverage",
        "--settings", "coverlet.runsettings"
    )
    
    if ($env:CI -eq "true") {
        $testArgs += "--logger", "GitHubActions"
    }
    
    dotnet @testArgs
    
    if ($LASTEXITCODE -ne 0) {
        # Tests failed, but don't throw - just report
        Write-Warning "Some tests failed. Check $TestResultsDir for details."
    } else {
        Write-Host "All tests passed" -ForegroundColor Green
    }
    
    # Generate test report
    if (Test-Path "$TestResultsDir/coverage.cobertura.xml") {
        Write-Host "Generating coverage report..." -ForegroundColor Cyan
        dotnet tool run reportgenerator `
            "-reports:$TestResultsDir/coverage.cobertura.xml" `
            "-targetdir:$TestResultsDir/coverage-report" `
            "-reporttypes:Html"
    }
}

# Run code analysis
function Invoke-CodeAnalysis {
    Write-Host "Running code analysis..." -ForegroundColor Cyan
    
    # Security analysis
    Write-Host "Running security analysis..." -ForegroundColor Yellow
    dotnet list package --vulnerable --include-transitive
    
    # Code style analysis
    Write-Host "Checking code style..." -ForegroundColor Yellow
    dotnet format --verify-no-changes --severity warn
    
    # Roslyn analyzers
    Write-Host "Running Roslyn analyzers..." -ForegroundColor Yellow
    dotnet build $SolutionPath -c $Configuration -p:RunAnalyzers=true -p:AnalysisLevel=recommended
    
    Write-Host "Code analysis completed" -ForegroundColor Green
}

# Create packages
function Invoke-Package {
    Write-Host "Creating deployment packages..." -ForegroundColor Cyan
    
    New-Item -ItemType Directory -Path $PackagesDir -Force | Out-Null
    
    # Package UI application
    if ($Platform -eq "Windows") {
        Write-Host "Packaging Windows application..." -ForegroundColor Yellow
        
        $publishArgs = @(
            "publish",
            "src/CrossInputShare.UI/CrossInputShare.UI.csproj",
            "-c", $Configuration,
            "-r", "win-x64",
            "--self-contained",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:DebugType=embedded",
            "-p:DebugSymbols=false",
            "-o", "$PackagesDir/CrossInputShare-Windows-x64"
        )
        
        dotnet @publishArgs
        
        # Create ZIP archive
        Compress-Archive -Path "$PackagesDir/CrossInputShare-Windows-x64/*" `
            -DestinationPath "$PackagesDir/CrossInputShare-Windows-x64.zip" -Force
    }
    
    # Package libraries
    Write-Host "Creating NuGet packages..." -ForegroundColor Yellow
    
    $projects = @(
        "CrossInputShare.Core",
        "CrossInputShare.Security",
        "CrossInputShare.Network",
        "CrossInputShare.Platform"
    )
    
    foreach ($project in $projects) {
        Write-Host "  Packaging $project..." -ForegroundColor Gray
        dotnet pack "src/$project/$project.csproj" `
            -c $Configuration `
            -o $PackagesDir `
            --no-build `
            --include-symbols `
            -p:SymbolPackageFormat=snupkg
    }
    
    Write-Host "Packaging completed" -ForegroundColor Green
    Write-Host "Packages available in: $PackagesDir" -ForegroundColor Cyan
}

# Main execution
try {
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "Cross-Platform Input Sharing Build Script" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "Configuration: $Configuration" -ForegroundColor White
    Write-Host "Platform: $Platform" -ForegroundColor White
    Write-Host "=========================================" -ForegroundColor Cyan
    
    # Check prerequisites
    Test-DotnetVersion
    
    # Clean if requested
    if ($Clean) {
        Invoke-Clean
    }
    
    # Restore dependencies
    Invoke-Restore
    
    # Build solution
    Invoke-Build
    
    # Run tests if requested
    if ($Test) {
        Invoke-Test
    }
    
    # Run code analysis if requested
    if ($CodeAnalysis) {
        Invoke-CodeAnalysis
    }
    
    # Create packages if requested
    if ($Package) {
        Invoke-Package
    }
    
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "Build process completed successfully" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    
} catch {
    Write-Host "=========================================" -ForegroundColor Red
    Write-Host "Build failed with error:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "=========================================" -ForegroundColor Red
    exit 1
}