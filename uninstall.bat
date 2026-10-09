@echo off
:: ============================================================
:: BlockPC - Script di Disinstallazione Servizi Windows
:: Eseguire come Amministratore
:: ============================================================

echo.
echo  BlockPC - Disinstallazione Servizi
echo ============================================================
echo.

:: Verifica privilegi amministratore
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERRORE] Questo script richiede privilegi di Amministratore.
    echo          Fare clic destro e selezionare "Esegui come amministratore".
    pause
    exit /b 1
)

set BLOCKER_SVC=BlockPC.BlockerService
set WATCHDOG_SVC=BlockPC.WatchdogService

echo [1/4] Arresto BlockerService...
sc stop "%BLOCKER_SVC%" 2>nul

echo.
echo [2/4] Arresto WatchdogService...
sc stop "%WATCHDOG_SVC%" 2>nul

:: Attendi che i servizi si fermino
echo.
echo Attesa arresto servizi...
timeout /t 5 /nobreak >nul

echo.
echo [3/4] Rimozione BlockerService...
sc delete "%BLOCKER_SVC%"

echo.
echo [4/4] Rimozione WatchdogService...
sc delete "%WATCHDOG_SVC%"

echo.
echo ============================================================
echo  DISINSTALLAZIONE COMPLETATA
echo.
echo  Entrambi i servizi sono stati rimossi.
echo  Il file hosts e' stato ripristinato automaticamente
echo  durante l'arresto del BlockerService.
echo ============================================================
echo.
pause
