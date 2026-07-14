@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion

set PROJ_DIR=%~dp0Olden Era - Template Editor
set PROJ_FILE="%PROJ_DIR%\Olden Era - Template Editor.csproj"
set OUT_DIR=%~dp0release
set TEST_FILE="%~dp0tests\OldenEraTemplateEditor.Tests\OldenEraTemplateEditor.Tests.csproj"

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%" >nul 2>&1

echo ==============================================
echo  Sbor: CommFork Template Editor
echo  Platform:    win-x64
echo ==============================================
echo.

echo [0/5] Update game content catalog (parse map_templates)...
echo ^> dotnet run --project "%~dp0scripts\GenCatalog" --verbosity quiet
dotnet run --project "%~dp0scripts\GenCatalog" --verbosity quiet
if errorlevel 1 goto ERR_CATALOG
echo OK.
echo.

echo [1/5] Restore packages...
echo ^> dotnet restore %PROJ_FILE% -r win-x64 --verbosity normal
dotnet restore %PROJ_FILE% -r win-x64 --verbosity normal
if errorlevel 1 goto ERR_RESTORE
echo OK.
echo.

echo [2/4] Build project...
echo ^> dotnet build %PROJ_FILE% -c Release -r win-x64 --verbosity normal
dotnet build %PROJ_FILE% -c Release -r win-x64 --verbosity normal
if errorlevel 1 goto ERR_BUILD
echo OK.
echo.

echo [3/4] Publish single-file exe...
echo ^> dotnet publish %PROJ_FILE% -c Release -r win-x64 --verbosity normal -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT_DIR%"
dotnet publish %PROJ_FILE% -c Release -r win-x64 --verbosity normal -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT_DIR%"
if errorlevel 1 goto ERR_PUBLISH
echo OK.
echo.

echo [5/5] Run tests...
if exist %TEST_FILE% (
    echo ^> dotnet test %TEST_FILE% --verbosity normal
    dotnet test %TEST_FILE% --verbosity normal
    if errorlevel 1 goto ERR_TESTS
    echo OK.
) else (
    echo Tests folder not found - skipping.
)
echo.

echo ==============================================
echo  Build complete!
echo.
echo  Output exe:
echo    %OUT_DIR%\OldenEraTemplateGenerator.exe
echo ==============================================
goto END

:ERR_RESTORE
echo.
echo [ERROR] restore failed (code: %ERRORLEVEL%)
goto END

:ERR_CATALOG
echo.
echo [ERROR] catalog update failed (code: %ERRORLEVEL%)
goto END

:ERR_BUILD
echo.
echo [ERROR] build failed (code: %ERRORLEVEL%)
goto END

:ERR_PUBLISH
echo.
echo [ERROR] publish failed (code: %ERRORLEVEL%)
goto END

:ERR_TESTS
echo.
echo [ERROR] tests failed (code: %ERRORLEVEL%)
goto END

:END
echo.
echo Press any key to exit...
pause >nul
endlocal