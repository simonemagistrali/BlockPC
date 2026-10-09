namespace BlockPC.Shared;

/// <summary>
/// Configurazione centralizzata delle policy di blocco.
/// Contiene le blacklist di processi e domini e i parametri di timing del sistema.
/// </summary>
public class BlockingPolicy
{
    /// <summary>
    /// Lista dei processi da terminare (senza estensione .exe).
    /// RF1: Processi distraenti + RF4: Strumenti di autodifesa.
    /// </summary>
    public List<string> BlockedProcesses { get; set; } = new();

    /// <summary>
    /// Lista dei domini web da bloccare tramite modifica del file hosts.
    /// RF2: DNS Override verso 127.0.0.1
    /// </summary>
    public List<string> BlockedDomains { get; set; } = new();

    /// <summary>
    /// Intervallo di polling in millisecondi per il ciclo di controllo processi.
    /// RNF2: Bilanciamento reattività/prestazioni. Default: 500ms.
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;

    /// <summary>
    /// Intervallo di polling in millisecondi per il meccanismo watchdog.
    /// Default: 1000ms (1 secondo).
    /// </summary>
    public int WatchdogIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Intervallo di riscrittura del file hosts in millisecondi.
    /// Default: 5000ms (5 secondi).
    /// </summary>
    public int HostsFileIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Abilita la protezione ACL sulla cartella di installazione.
    /// RNF3: Impedisce la disinstallazione forzata a runtime.
    /// </summary>
    public bool EnableAclProtection { get; set; } = true;
}
