@echo off
:: ============================================================
:: BlockPC - Script di Build e Pubblicazione
:: ============================================================

echo.
echo  BlockPC - Build e Pubblicazione
echo ============================================================
echo.

set ROOT_DIR=%~dp0

echo [1/3] Pulizia build precedenti...
dotnet clean "%ROOT_DIR%BlockPC.sln" -c Release >nul 2>&1

echo.
echo [2/3] Pubblicazione BlockerService...
dotnet publish "%ROOT_DIR%src\BlockerService\BlockerService.csproj" -c Release -o "%ROOT_DIR%publish\BlockerService" --self-contained false

echo.
echo [3/3] Pubblicazione WatchdogService...
dotnet publish "%ROOT_DIR%src\WatchdogService\WatchdogService.csproj" -c Release -o "%ROOT_DIR%publish\WatchdogService" --self-contained false

echo.
echo ============================================================
echo  BUILD COMPLETATA
echo.
echo  Output:
echo    - publish\BlockerService\
echo    - publish\WatchdogService\
echo.
echo  Eseguire install.bat come Amministratore per installare.
echo ============================================================
echo.
pause
