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
Write-Host "[1/3] Restoring packages..." -ForegroundColor Yellow
dotnet restore $ProjectFile -r win-x64
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] restore failed (code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

# Build
Write-Host "[2/3] Building project..." -ForegroundColor Yellow
dotnet build $ProjectFile -c Release -r win-x64 --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] build failed (code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

# Publish
Write-Host "[3/3] Publishing single-file exe..." -ForegroundColor Yellow
dotnet publish $ProjectFile -c Release -r win-x64 --no-build `
    -p:PublishSingleFile=true -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o $OutputDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] publish failed (code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "OK." -ForegroundColor Green
Write-Host ""

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host " Executable:" -ForegroundColor White
Write-Host "   $OutputDir\OldenEraTemplateGenerator.exe" -ForegroundColor Gray
Write-Host "==============================================" -ForegroundColor Cyan
