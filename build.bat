@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set PROJ_DIR=%~dp0Olden Era - Template Editor
set PROJ_FILE="%PROJ_DIR%\Olden Era - Template Editor.csproj"
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Release

echo ==============================================
echo  Сборка Olden Era Template Editor
echo  Конфигурация: %CONFIG%
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
dotnet build "%PROJ_FILE%" -c %CONFIG% -r win-x64 --no-restore
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ОШИБКА] build failed (код: %ERRORLEVEL%)
    exit /b %ERRORLEVEL%
)
echo OK.
echo.

:: Публикация (single-file self-contained)
echo [3/3] Публикация single-file exe...
dotnet publish "%PROJ_FILE%" -c %CONFIG% -r win-x64 --no-build ^
    -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -o "%~dp0build\%CONFIG%\"

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
echo  Исполняемые файлы:
echo    %~dp0build\%CONFIG%\OldenEraTemplateGenerator.exe
echo.
echo  Чтобы собрать в Debug (с отладкой):
echo    build Debug
echo.
echo  Чтобы собрать в Release (по умолчанию):
echo    build
echo ==============================================

endlocal
