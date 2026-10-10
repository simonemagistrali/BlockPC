# BlockPC - Sistema di Blocco Anti-Distrazione & Focus Enforcement

> Soluzione completa per Windows ad alta resilienza per massimizzare la concentrazione e la produttività personale, basata su un'architettura **Dual-Service Watchdog**, rilevamento eventi di processo ad alte prestazioni e un'interfaccia grafica desktop **WPF**.

---

## 📑 Indice

- [Panoramica](#-panoramica)
- [Architettura del Sistema](#-architettura-del-sistema)
- [Caratteristiche Principali](#-caratteristiche-principali)
- [Requisiti di Sistema](#-requisiti-di-sistema)
- [Guida Rapida: Build e Installazione](#-guida-rapida-build-e-installazione)
- [BlockPC Dashboard (UI)](#-blockpc-dashboard-ui)
- [Configurazione (`appsettings.json`)](#-configurazione-appsettingsjson)
- [Meccanismi di Difesa e Resilienza](#-meccanismi-di-difesa-e-resilienza)
- [Struttura del Progetto](#-struttura-del-progetto)
- [Disinstallazione e Modalità di Emergenza](#-disinstallazione-e-modalit-di-emergenza)
- [Licenza](#-licenza)

---

## 🌟 Panoramica

**BlockPC** è progettato per eliminare le distrazioni al computer imponendo regole rigide e difficilmente aggirabili. A differenza dei comuni software di produttività facilmente chiudibili da Gestione Attività, BlockPC opera a livello di sistema operativo tramite servizi Windows privilegiati (`NT AUTHORITY\SYSTEM`), monitoraggio WMI in tempo reale e blocco DNS a livello di rete locale.

Include inoltre una dashboard grafica desktop per configurare le policy e attivare sessioni di focus rapido, protetta da regole di **append-only** per prevenire disattivazioni impulsive.

---

## 🏗️ Architettura del Sistema

```
┌───────────────────────────────────────────────────────────┐
│                      BlockPC.UI                           │
│                (Dashboard Grafica WPF)                    │
│                                                           │
│  ✦ Stato in tempo reale (Attivo / In Pausa)               │
│  ✦ Gestione liste processi & domini                       │
│  ✦ Schedulazione orari e giorni attivi                    │
│  ✦ Blocco Rapido temporizzato (Quick Block)               │
│  ✦ Enforcement Append-Only a blocco attivo                │
└─────────────────────────────┬─────────────────────────────┘
                              │
                    Named Pipe IPC (JSON)
                    \\.\pipe\BlockPCPipe
                              │
┌─────────────────────────────▼─┐     ┌─────────────────────────────┐
│       BlockerService          │◄───►│      WatchdogService        │
│    (Servizio Principale)      │     │   (Servizio di Supporto)    │
│                               │     │                             │
│  ✦ Event-Driven Killer (WMI)  │     │  ✦ Monitora BlockerService  │
│  ✦ Safety-Net Loop periodico  │     │  ✦ Riavvio automatico       │
│  ✦ DNS Override (File Hosts)  │     │  ✦ Resilienza a terminazione│
│  ✦ Schedulazione & Timer      │     │                             │
│  ✦ Server IPC Named Pipes     │     │                             │
│  ✦ Protezione ACL cartella    │     │                             │
│  ✦ Monitora WatchdogService   │     │                             │
└───────────────────────────────┘     └─────────────────────────────┘
                │                                    │
                └──────── NT AUTHORITY\SYSTEM ───────┘
```

### Componenti:

1. **`BlockerService` (Servizio Principale)**:
   - Intercetta ed elimina istantaneamente i processi non consentiti e gli strumenti di disattivazione (Task Manager, prompt dei comandi, PowerShell, editor di registro).
   - Modifica il file `hosts` per redirigere i domini vietati su `127.0.0.1` e svuota la cache DNS.
   - Espone un server IPC su Named Pipes per consentire alla UI di leggere lo stato e aggiornare le configurazioni.
   - Sorveglia il `WatchdogService` e lo riavvia se viene chiuso.

2. **`WatchdogService` (Servizio di Sorveglianza)**:
   - Esegue un loop leggero e costante sullo stato di `BlockerService`.
   - Se il servizio principale viene terminato o va in crash, lo riavvia istantaneamente.

3. **`BlockPC.UI` (Client Grafico WPF)**:
   - Permette all'utente di interagire comodamente con il servizio in background.
   - Applica regole di autodisciplina (RS2): durante un blocco attivo è impossibile rimuovere processi o accorciare i timer di blocco.

4. **`BlockPC.Shared` (Libreria Condivisa)**:
   - Modelli di dati (`BlockingPolicy`), helper DNS (`HostsFileManager`), terminazione processi (`ProcessKiller`), gestione ACL NTFS (`AclProtection`) e controllo servizi Windows (`ServiceGuard`).

---

## ⚡ Caratteristiche Principali

- **WMI Event-Driven Process Killer**: Invece di interrogare continuamente l'elenco di tutti i processi (polling ad alto uso di CPU), sfrutta gli eventi WMI del kernel Windows (`Win32_ProcessStartTrace`) per intercettare l'avvio delle applicazioni target in pochi millisecondi, mantenendo l'uso di CPU quasi a **0%**.
- **Safety-Net Polling**: Un controllo periodico di sicurezza a bassa frequenza assicura che nessun processo preesistente rimanga aperto.
- **DNS Override Intelligente**: Applica e rimuove le regole dal file `hosts` di sistema solo quando necessario o durante i cambi di stato, minimizzando le scritture su disco (salvaguardia dell'SSD).
- **Programmazione Oraria & Giornaliera**: Configura orari di inizio e fine blocco (es. 09:00 - 18:00) e giorni lavorativi dedicati.
- **Blocco Rapido (Quick Block)**: Attiva una sessione intensiva immediata (es. 1, 2, 4 o 8 ore) che sovrascrive temporaneamente il normale calendario settimanale.
- **Politica Append-Only a Blocco Attivo**: Mentre una sessione di blocco è in corso, non è consentito allentare le regole (non si possono togliere siti o app vietate, né ridurre orari o annullare il Quick Block).
- **Autodifesa Preventiva**: Blocca i principali strumenti amministrativi (Gestione Attività, Regedit, Process Hacker, Procmon, CMD, PowerShell).

---

## 📋 Requisiti di Sistema

- **Sistema Operativo**: Windows 10 / 11 (64-bit)
- **Runtime / SDK**: .NET 9.0 SDK o Runtime (incluso il supporto Desktop per WPF)
- **Privilegi**: Diritti di Amministratore (necessari per registrare servizi Windows, gestire i file protetti in `%SystemRoot%` e impostare permessi ACL)

---

## 🚀 Guida Rapida: Build e Installazione

### 1. Compilazione del Progetto

È sufficiente eseguire lo script di build incluso:
```cmd
build.bat
```
Lo script pulirà le build precedenti e pubblicherà in modalità `Release` i tre eseguibili dentro la cartella `publish/`:
- `publish\BlockerService\`
- `publish\WatchdogService\`
- `publish\BlockPC.UI\`

*In alternativa, compilazione manuale da terminale:*
```bash
dotnet publish src/BlockerService/BlockerService.csproj -c Release -o publish/BlockerService --self-contained false
dotnet publish src/WatchdogService/WatchdogService.csproj -c Release -o publish/WatchdogService --self-contained false
dotnet publish src/BlockPC.UI/BlockPC.UI.csproj -c Release -o publish/BlockPC.UI --self-contained false
```

### 2. Installazione dei Servizi (Come Amministratore)

Fare clic destro su **`install.bat`** e selezionare **"Esegui come amministratore"**.

Lo script:
1. Registra `BlockPC.BlockerService` nel Service Control Manager con account `LocalSystem` e avvio automatico.
2. Registra `BlockPC.WatchdogService` con account `LocalSystem` e avvio automatico.
3. Imposta le regole di riavvio in caso di errore (`sc failure`).
4. Avvia immediatamente entrambi i servizi.

### 3. Avvio della Dashboard Grafica

Dopo l'installazione dei servizi, avviare:
```cmd
publish\BlockPC.UI\BlockPC.UI.exe
```
L'interfaccia si connetterà automaticamente al servizio tramite la Named Pipe locale `BlockPCPipe`.

---

## 🖥️ BlockPC Dashboard (UI)

La finestra di gestione WPF offre una panoramica chiara dello stato:

- **Banner di Stato**: Mostra in tempo reale se il sistema è `ATTIVO` (verde) oppure `IN PAUSA` (arancione), insieme all'eventuale scadenza del Blocco Rapido.
- **Sezione Blocco Rapido**: Permette di selezionare ore aggiuntive e fare clic su *Attiva Ora!*.
- **Gestione Processi Bloccati**: Lista con campo di testo e pulsanti `+` e `-` per aggiungere o rimuovere eseguibili.
- **Gestione Domini Bloccati**: Lista per aggiungere o rimuovere domini web reindirizzati.
- **Pianificazione Settimanale**: Selezione dei giorni attivi (Lun-Dom) e degli orari di inizio e fine blocco.
- **Salva Configurazione**: Invia le nuove policy al servizio via IPC e aggiorna il file di configurazione su disco.

---

## ⚙️ Configurazione (`appsettings.json`)

Le impostazioni predefinite dei servizi si trovano in `src/BlockerService/appsettings.json` (e nella cartella pubblicata):

```json
{
  "BlockingPolicy": {
    "PollingIntervalMs": 500,
    "WatchdogIntervalMs": 1000,
    "HostsFileIntervalMs": 5000,
    "EnableAclProtection": true,

    "BlockedProcesses": [
      "taskmgr", "cmd", "powershell", "pwsh", "mmc",
      "devenv", "regedit", "msconfig", "perfmon", "resmon",
      "procexp", "procmon", "autoruns"
    ],

    "BlockedDomains": [
      "youtube.com", "facebook.com", "instagram.com",
      "twitter.com", "x.com", "tiktok.com", "reddit.com",
      "twitch.tv", "netflix.com", "primevideo.com", "disneyplus.com"
    ]
  }
}
```

### Parametri di Configurazione

| Parametro | Default | Descrizione |
|---|---|---|
| `PollingIntervalMs` | `500` | Frequenza del controllo di sicurezza sui processi (ms). |
| `WatchdogIntervalMs` | `1000` | Frequenza del controllo reciproco tra i servizi (ms). |
| `HostsFileIntervalMs` | `5000` | Intervallo verifica e riscrittura regole hosts (ms). |
| `EnableAclProtection` | `true` | Applica permessi restrittivi ACL NTFS sulla cartella di installazione. |
| `BlockedProcesses` | *[array]* | Nomi dei processi da terminare (senza estensione `.exe`). |
| `BlockedDomains` | *[array]* | Domini web da bloccare tramite DNS locale. |
| `ActiveDays` | *[Lun-Dom]* | Giorni settimanali in cui applicare la fascia oraria di blocco. |
| `StartTime` | `00:00:00` | Orario inizio blocco programmato. |
| `EndTime` | `23:59:59` | Orario fine blocco programmato. |
| `QuickBlockEndTime` | `null` | Data e ora di scadenza dell'eventuale Blocco Rapido attivo. |

---

## 🛡️ Meccanismi di Difesa e Resilienza

| Livello | Meccanismo | Descrizione |
|---|---|---|
| **Processi (Kernel/WMI)** | Event Tracing & Kill Loop | Intercetta all'istante l'avvio dei processi bloccati e li termina. |
| **Rete (DNS)** | Hosts Override + Flush | I domini distraenti puntano a `127.0.0.1` con svuotamento cache DNS. |
| **Servizio** | Dual Watchdog | I due servizi si monitorano a vicenda riavviandosi istantaneamente se uno cade. |
| **Windows SCM** | Service Recovery Policy | Il sistema operativo riavvia automaticamente il servizio a 5s, 10s e 30s. |
| **Filesystem** | ACL Deny | Protegge i file binari da cancellazioni accidentali o intenzionali durante il runtime. |
| **Integrità Policy** | Append-Only Enforcement | Durante una sessione attiva non è consentito allentare le restrizioni o rimuovere liste. |

---

## 📁 Struttura del Progetto

```
BlockPC/
├── BlockPC.sln                  # Soluzione Visual Studio / .NET 9
├── build.bat                    # Script di compilazione e pubblicazione Release
├── install.bat                  # Script di registrazione e avvio servizi SCM
├── uninstall.bat                # Script di arresto e rimozione servizi SCM
├── src/
│   ├── BlockPC.Shared/          # Libreria condivisa comune
│   │   ├── AclProtection.cs     # Gestione permessi di sicurezza NTFS
│   │   ├── BlockingPolicy.cs    # Modello dati policy, orari e blacklist
│   │   ├── HostsFileManager.cs  # Gestione file hosts e flush DNS
│   │   ├── ProcessKiller.cs     # Terminazione processi Windows
│   │   ├── ServiceGuard.cs      # Monitoraggio e avvio servizi SCM
│   │   └── ServiceNames.cs      # Costanti nomi servizi Windows
│   ├── BlockerService/          # Servizio principale Windows
│   │   ├── Program.cs           # Configurazione host ed esecuzione come Windows Service
│   │   ├── Worker.cs            # Motore WMI, loop hosts e mutuo watchdog
│   │   ├── IpcServerWorker.cs   # Server Named Pipes e validazione policy
│   │   └── appsettings.json     # Configurazione iniziale
│   ├── WatchdogService/         # Servizio di sorveglianza Windows
│   │   ├── Program.cs           # Entrypoint Windows Service
│   │   ├── Worker.cs            # Loop di riavvio automatico per BlockerService
│   │   └── appsettings.json     # Configurazione di supporto
│   └── BlockPC.UI/              # Dashboard Desktop (WPF .NET 9)
│       ├── App.xaml / .cs       # Entrypoint applicazione WPF
│       ├── MainWindow.xaml      # Layout interfaccia grafica
│       ├── MainWindow.xaml.cs   # Logica di presentazione, scheduling e comandi
│       └── IpcClient.cs         # Client Named Pipes per comunicazione con BlockerService
└── publish/                     # Cartella degli output compilati (generata da build.bat)
    ├── BlockerService/
    ├── WatchdogService/
    └── BlockPC.UI/
```

---

## ⚠️ Disinstallazione e Modalità di Emergenza

### Disinstallazione Normale
1. Chiudere l'interfaccia grafica `BlockPC.UI`.
2. Fare clic destro su **`uninstall.bat`** e selezionare **"Esegui come amministratore"**.
3. Lo script arresterà entrambi i servizi, rimuoverà le protezioni ACL, ripristinerà il file `hosts` originale ed eliminerà la registrazione dei servizi da Windows.

### Procedura di Emergenza (Sblocco Rapido in 1 Click)
Qualora gli strumenti di sistema siano inaccessibili o il prompt dei comandi non sia avviabile:
1. Fare doppio clic sul file **`sblocca_pc.vbs`** nella cartella principale del progetto e confermare la richiesta UAC di Windows.
2. Lo script VBScript bypassa qualsiasi blocco, arresta e rimuove immediatamente entrambi i servizi, svuota la cache DNS e ripristina il file `hosts`.
3. In alternativa, riavviare il PC in **Modalità Provvisoria (Safe Mode)** di Windows ed eseguire `uninstall.bat` come amministratore.

---

## 📜 Licenza

Questo progetto è rilasciato per **uso personale e studio**. Non utilizzarlo su dispositivi di terzi senza il loro esplicito consenso.
