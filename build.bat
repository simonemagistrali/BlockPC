@echo off
:: ============================================================
:: BlockPC - Script di Build e Pubblicazione
:: ============================================================

echo.
echo  BlockPC - Build e Pubblicazione
echo ============================================================
echo.

set ROOT_DIR=%~dp0

echo [1/4] Pulizia build precedenti...
dotnet clean "%ROOT_DIR%BlockPC.sln" -c Release >nul 2>&1

echo.
echo [2/4] Pubblicazione BlockerService...
dotnet publish "%ROOT_DIR%src\BlockerService\BlockerService.csproj" -c Release -o "%ROOT_DIR%publish\BlockerService" --self-contained false

echo.
echo [3/4] Pubblicazione WatchdogService...
dotnet publish "%ROOT_DIR%src\WatchdogService\WatchdogService.csproj" -c Release -o "%ROOT_DIR%publish\WatchdogService" --self-contained false

echo.
echo [4/4] Pubblicazione BlockPC.UI...
dotnet publish "%ROOT_DIR%src\BlockPC.UI\BlockPC.UI.csproj" -c Release -o "%ROOT_DIR%publish\BlockPC.UI" --self-contained false

echo.
echo ============================================================
echo  BUILD COMPLETATA
echo.
echo  Output:
echo    - publish\BlockerService\
echo    - publish\WatchdogService\
echo    - publish\BlockPC.UI\
echo.
echo  Eseguire install.bat come Amministratore per installare i servizi.
echo ============================================================
echo.
pause
