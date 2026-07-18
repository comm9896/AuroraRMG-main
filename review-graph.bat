@echo off
chcp 65001 >nul 2>&1
cd /d "%~dp0"

echo ==============================================
echo  /code-review-graph build
echo ==============================================
echo.
code-review-graph build
if errorlevel 1 goto ERR_BUILD
echo OK.
echo.

echo ==============================================
echo  /code-review-graph watch
echo ==============================================
echo.
code-review-graph watch
if errorlevel 1 goto ERR_WATCH
echo OK.
echo.

goto END

:ERR_BUILD
echo.
echo [ERROR] build step failed (code: %ERRORLEVEL%)
goto END

:ERR_WATCH
echo.
echo [ERROR] watch step failed (code: %ERRORLEVEL%)
goto END

:END
echo.
echo Press any key to close...
pause >nul
