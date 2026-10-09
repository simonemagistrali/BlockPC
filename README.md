# BlockPC - Sistema di Blocco Anti-Distrazione

> Architettura a doppio servizio Windows (Dual-Service Watchdog) per massimizzare la produttività bloccando applicazioni e siti web distraenti.

## 🏗️ Architettura

```
┌─────────────────────────────┐     ┌─────────────────────────────┐
│      BlockerService         │◄───►│      WatchdogService        │
│   (Servizio Principale)     │     │   (Servizio di Supporto)    │
│                             │     │                             │
│  ✦ Termina processi         │     │  ✦ Monitora BlockerService  │
│  ✦ Blocca siti web (DNS)    │     │  ✦ Riavvio automatico       │
│  ✦ Monitora WatchdogService │     │                             │
│  ✦ Protezione ACL           │     │                             │
└─────────────────────────────┘     └─────────────────────────────┘
         │                                    │
         └──── NT AUTHORITY\SYSTEM ───────────┘
```

Entrambi i servizi si monitorano reciprocamente: se uno viene terminato, l'altro lo riavvia istantaneamente.

## 📋 Requisiti

- Windows 10/11 (64-bit)
- .NET 9.0 Runtime
- Privilegi di Amministratore (per installazione)

## 🚀 Build e Installazione

### 1. Build
```batch
build.bat
```
Oppure manualmente:
```bash
dotnet publish src/BlockerService/BlockerService.csproj -c Release -o publish/BlockerService
dotnet publish src/WatchdogService/WatchdogService.csproj -c Release -o publish/WatchdogService
```

### 2. Installazione (come Amministratore)
```batch
install.bat
```

### 3. Disinstallazione (come Amministratore)
```batch
uninstall.bat
```

## ⚙️ Configurazione

Modificare `appsettings.json` nella cartella del BlockerService **prima dell'installazione**:

```json
{
  "BlockingPolicy": {
    "PollingIntervalMs": 500,
    "WatchdogIntervalMs": 1000,
    "HostsFileIntervalMs": 5000,
    "EnableAclProtection": true,

    "BlockedProcesses": [
      "taskmgr", "cmd", "powershell", "pwsh", "mmc",
      "devenv", "regedit", "msconfig"
    ],

    "BlockedDomains": [
      "youtube.com", "facebook.com", "instagram.com",
      "twitter.com", "reddit.com", "tiktok.com"
    ]
  }
}
```

### Parametri

| Parametro | Default | Descrizione |
|---|---|---|
| `PollingIntervalMs` | 500 | Intervallo scansione processi (ms) |
| `WatchdogIntervalMs` | 1000 | Intervallo monitoraggio servizio partner (ms) |
| `HostsFileIntervalMs` | 5000 | Intervallo riscrittura file hosts (ms) |
| `EnableAclProtection` | true | Protezione ACL cartella di installazione |
| `BlockedProcesses` | [...] | Lista processi da terminare (senza .exe) |
| `BlockedDomains` | [...] | Lista domini da bloccare via DNS |

## 🛡️ Meccanismi di Difesa

| Livello | Meccanismo | Descrizione |
|---|---|---|
| **Processo** | Kill Loop | Termina Task Manager, CMD, PowerShell, MMC |
| **Rete** | DNS Override | Modifica file hosts → 127.0.0.1 |
| **Servizio** | Dual Watchdog | Mutuo soccorso tra i due servizi |
| **SCM** | Recovery Policy | Windows riavvia i servizi automaticamente |
| **Filesystem** | ACL Deny | Impedisce eliminazione file del servizio |

## 📁 Struttura Progetto

```
BlockPC/
├── BlockPC.sln
├── build.bat
├── install.bat
├── uninstall.bat
├── src/
│   ├── BlockPC.Shared/          # Libreria condivisa
│   │   ├── AclProtection.cs     # RNF3 - Protezione NTFS
│   │   ├── BlockingPolicy.cs    # Modello configurazione
│   │   ├── HostsFileManager.cs  # RF2 - DNS Override
│   │   ├── ProcessKiller.cs     # RF1/RF4/RF5 - Kill processi
│   │   ├── ServiceGuard.cs      # RF3 - Mutuo soccorso
│   │   └── ServiceNames.cs      # Costanti nomi servizi
│   ├── BlockerService/          # Servizio principale
│   │   ├── Program.cs
│   │   ├── Worker.cs
│   │   └── appsettings.json
│   └── WatchdogService/         # Servizio di sorveglianza
│       ├── Program.cs
│       ├── Worker.cs
│       └── appsettings.json
└── publish/                     # Output build (generato)
```

## ⚠️ Avvertenze

- **Questo software è progettato per l'auto-disciplina personale.** Non utilizzarlo su computer di terzi senza consenso.
- La protezione ACL e il blocco degli strumenti di sistema rendono difficile la disinstallazione a runtime. Utilizzare `uninstall.bat` da un prompt Amministratore per la rimozione pulita.
- In caso di emergenza, avviare Windows in **Modalità Provvisoria** per bypassare i servizi.

## 📜 Licenza

Questo progetto è ad uso personale.
