@echo off
:: ============================================================
:: BlockPC - Script di Installazione Servizi Windows
:: Eseguire come Amministratore
:: ============================================================

echo.
echo  ____  _            _    ____   ____
echo ^| __ )^| ^| ___   ___^| ^| _^|  _ \ / ___^|
echo ^|  _ \^| ^|/ _ \ / __^| ^|/ ^/ ^|_) ^| ^|
echo ^| ^|_) ^| ^| (_) ^| (__^|   ^<^|  __/^| ^|___
echo ^|____/^|_^|\___/ \___^|_^|\_\_^|    \____^|
echo.
echo  Installazione Servizi Anti-Distrazione
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

set INSTALL_DIR=%~dp0
set BLOCKER_EXE=%INSTALL_DIR%publish\BlockerService\BlockerService.exe
set WATCHDOG_EXE=%INSTALL_DIR%publish\WatchdogService\WatchdogService.exe
set BLOCKER_SVC=BlockPC.BlockerService
set WATCHDOG_SVC=BlockPC.WatchdogService

:: Verifica che i file esistano
if not exist "%BLOCKER_EXE%" (
    echo [ERRORE] File non trovato: %BLOCKER_EXE%
    echo          Eseguire prima: dotnet publish
    pause
    exit /b 1
)

if not exist "%WATCHDOG_EXE%" (
    echo [ERRORE] File non trovato: %WATCHDOG_EXE%
    echo          Eseguire prima: dotnet publish
    pause
    exit /b 1
)

echo [1/4] Registrazione BlockerService...
sc create "%BLOCKER_SVC%" binPath="%BLOCKER_EXE%" start=auto obj="LocalSystem" DisplayName="BlockPC - Blocker Service"
sc description "%BLOCKER_SVC%" "Servizio principale BlockPC: termina processi distraenti e blocca siti web."
sc failure "%BLOCKER_SVC%" reset=60 actions=restart/5000/restart/10000/restart/30000

echo.
echo [2/4] Registrazione WatchdogService...
sc create "%WATCHDOG_SVC%" binPath="%WATCHDOG_EXE%" start=auto obj="LocalSystem" DisplayName="BlockPC - Watchdog Service"
sc description "%WATCHDOG_SVC%" "Servizio di sorveglianza BlockPC: monitora e riavvia il Blocker Service."
sc failure "%WATCHDOG_SVC%" reset=60 actions=restart/5000/restart/10000/restart/30000

echo.
echo [3/4] Avvio WatchdogService...
sc start "%WATCHDOG_SVC%"

echo.
echo [4/4] Avvio BlockerService...
sc start "%BLOCKER_SVC%"

echo.
echo ============================================================
echo  INSTALLAZIONE COMPLETATA
echo.
echo  Servizi registrati e avviati:
echo    - %BLOCKER_SVC% (Principale)
echo    - %WATCHDOG_SVC% (Sorveglianza)
echo.
echo  Entrambi i servizi si riavviano automaticamente al boot.
echo  Il meccanismo watchdog reciproco e' attivo.
echo ============================================================
echo.
pause
