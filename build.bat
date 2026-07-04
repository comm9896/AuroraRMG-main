@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set PROJ_DIR=%~dp0Olden Era - Template Editor
set PROJ_FILE="%PROJ_DIR%\Olden Era - Template Editor.csproj"
set OUT_DIR=%~dp0release

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

echo ==============================================
echo  Сборка CommFork Template Editor
echo  Платформа:    win-x64
echo ==============================================
echo.

:: Восстановление зависимостей
echo [1/3] Восстановление пакетов...
dotnet restore "%PROJ_FILE%" -r win-x64
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ОШИБКА] restore failed (код: %ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
echo OK.
echo.

:: Сборка проекта
echo [2/3] Сборка проекта...
dotnet build "%PROJ_FILE%" -c Release -r win-x64 --no-restore
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ОШИБКА] build failed (код: %ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
echo OK.
echo.

:: Публикация (single-file self-contained)
echo [3/3] Публикация single-file exe...
dotnet publish "%PROJ_FILE%" -c Release -r win-x64 --no-build ^
    -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -o "%OUT_DIR%\"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ОШИБКА] publish failed (код: %ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
echo OK.
echo.

echo ==============================================
echo  Сборка завершена успешно!
echo.
echo  Исполняемый файл:
echo    %OUT_DIR%\OldenEraTemplateGenerator.exe
echo ==============================================

endlocal
