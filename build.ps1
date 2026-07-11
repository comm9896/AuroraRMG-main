$ProjectDir = Join-Path $PSScriptRoot "Olden Era - Template Editor"
$ProjectFile = Join-Path $ProjectDir "Olden Era - Template Editor.csproj"
$OutputDir = Join-Path $PSScriptRoot "release"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Building CommFork Template Editor" -ForegroundColor Cyan
Write-Host " Platform:      win-x64" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Restore
Write-Host "[1/4] Restoring packages..." -ForegroundColor Yellow
Write-Host "> dotnet restore $ProjectFile -r win-x64 --verbosity normal" -ForegroundColor Gray
dotnet restore $ProjectFile -r win-x64 --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] restore failed (code: $LASTEXITCODE)" -ForegroundColor Red
    Read-Host -Prompt "Press Enter to exit"
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

# Build
Write-Host "[2/4] Building project..." -ForegroundColor Yellow
Write-Host "> dotnet build $ProjectFile -c Release -r win-x64 --verbosity normal" -ForegroundColor Gray
dotnet build $ProjectFile -c Release -r win-x64 --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] build failed (code: $LASTEXITCODE)" -ForegroundColor Red
    Read-Host -Prompt "Press Enter to exit"
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

# Publish
Write-Host "[3/4] Publishing single-file exe..." -ForegroundColor Yellow
Write-Host "> dotnet publish $ProjectFile -c Release -r win-x64 --verbosity normal -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $OutputDir" -ForegroundColor Gray
dotnet publish $ProjectFile -c Release -r win-x64 --verbosity normal `
    -p:PublishSingleFile=true -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $OutputDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] publish failed (code: $LASTEXITCODE)" -ForegroundColor Red
    Read-Host -Prompt "Press Enter to exit"
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

# Run tests
$TestFile = Join-Path $PSScriptRoot "tests\OldenEraTemplateEditor.Tests\OldenEraTemplateEditor.Tests.csproj"
Write-Host "[4/4] Running tests..." -ForegroundColor Yellow
if (Test-Path $TestFile) {
    Write-Host "> dotnet test $TestFile --verbosity normal" -ForegroundColor Gray
    dotnet test $TestFile --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] tests failed (code: $LASTEXITCODE)" -ForegroundColor Red
        Read-Host -Prompt "Press Enter to exit"
        exit $LASTEXITCODE
    }
    Write-Host "OK." -ForegroundColor Green
} else {
    Write-Host "Test project not found — skipping." -ForegroundColor Yellow
}
Write-Host ""

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host " Executable:" -ForegroundColor White
Write-Host "   $OutputDir\OldenEraTemplateGenerator.exe" -ForegroundColor Gray
Write-Host "==============================================" -ForegroundColor Cyan

Read-Host -Prompt "Press Enter to exit"
