using BlockPC.Shared;
using Microsoft.Extensions.Options;

namespace BlockerService;

/// <summary>
/// Servizio Principale (BlockerService) - Motore di enforcement delle policy.
/// 
/// Responsabilità:
/// - RF1: Polling e terminazione processi in blacklist ogni 500ms
/// - RF2: Blocco DNS tramite modifica file hosts
/// - RF3: Monitoraggio e riavvio del WatchdogService
/// - RF4: Autodifesa preventiva (blocco strumenti di sistema)
/// - RF5: Gestione silenziosa eccezioni
/// - RNF3: Protezione ACL cartella di installazione
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly BlockingPolicy _policy;
    private readonly string _installDirectory;

    public Worker(ILogger<Worker> logger, IOptions<BlockingPolicy> policy)
    {
        _logger = logger;
        _policy = policy.Value;

        // Determina la cartella di installazione (dove risiede l'eseguibile)
        _installDirectory = AppContext.BaseDirectory;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== BlockerService avviato ===");
        _logger.LogInformation("Processi bloccati: {Count}", _policy.BlockedProcesses.Count);
        _logger.LogInformation("Domini bloccati: {Count}", _policy.BlockedDomains.Count);
        _logger.LogInformation("Polling intervallo: {Interval}ms", _policy.PollingIntervalMs);

        // RNF3: Applica protezione ACL all'avvio
        if (_policy.EnableAclProtection)
        {
            AclProtection.LockDown(_installDirectory, msg => _logger.LogWarning("{Message}", msg));
        }

        // RF2: Applica blocco DNS immediatamente all'avvio
        HostsFileManager.ApplyDnsBlock(
            _policy.BlockedDomains,
            msg => _logger.LogInformation("{Message}", msg));
        HostsFileManager.FlushDnsCache(msg => _logger.LogInformation("{Message}", msg));

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Avvia i tre loop concorrenti
        var processTask = ProcessKillerLoopAsync(stoppingToken);
        var watchdogTask = WatchdogLoopAsync(stoppingToken);
        var hostsTask = HostsFileLoopAsync(stoppingToken);

        // Attende che tutti completino (o che il token venga cancellato)
        await Task.WhenAll(processTask, watchdogTask, hostsTask);
    }

    /// <summary>
    /// RF4: Determina se il blocco è attualmente "Attivo" in base alle policy di scheduling.
    /// </summary>
    private bool IsBlockActive()
    {
        var now = DateTime.Now;

        // 1. Controllo Quick Block
        if (_policy.QuickBlockEndTime.HasValue && _policy.QuickBlockEndTime.Value > now)
        {
            return true;
        }

        // 2. Controllo Scheduling
        if (!_policy.ActiveDays.Contains(now.DayOfWeek))
        {
            return false;
        }

        var timeOfDay = now.TimeOfDay;
        if (timeOfDay >= _policy.StartTime && timeOfDay <= _policy.EndTime)
        {
            return true;
        }

        return false;
    }

    private async Task ProcessKillerLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ProcessKiller] Loop WMI avviato - safety net ogni {Interval}ms", _policy.PollingIntervalMs);

        System.Management.ManagementEventWatcher? watcher = null;
        try
        {
            watcher = new System.Management.ManagementEventWatcher(
                new System.Management.WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            
            watcher.EventArrived += (sender, e) =>
            {
                try
                {
                    if (!IsBlockActive()) return;

                    var processName = e.NewEvent["ProcessName"]?.ToString();
                    if (processName != null)
                    {
                        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            processName = processName.Substring(0, processName.Length - 4);

                        foreach (var blocked in _policy.BlockedProcesses)
                        {
                            if (string.Equals(processName, blocked, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogDebug("[ProcessKiller WMI] Rilevato processo in blacklist: {ProcessName}", processName);
                                ProcessKiller.KillBlockedProcesses(new[] { blocked }, msg => _logger.LogDebug("{Message}", msg));
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ProcessKiller WMI] Errore nell'handler dell'evento");
                }
            };
            watcher.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ProcessKiller] Impossibile avviare WMI. Verrà utilizzato solo il polling completo.");
        }

        bool wasActive = false;
        int safetyNetCounter = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool isActive = IsBlockActive();

                if (isActive)
                {
                    // Se siamo appena diventati attivi, o ogni 10 cicli (circa 5 secondi), facciamo un controllo di sicurezza
                    if (!wasActive || safetyNetCounter >= 10)
                    {
                        ProcessKiller.KillBlockedProcesses(
                            _policy.BlockedProcesses,
                            msg => _logger.LogDebug("{Message}", msg));
                        
                        safetyNetCounter = 0;
                    }
                    else
                    {
                        safetyNetCounter++;
                    }
                }
                else
                {
                    safetyNetCounter = 0;
                }
                
                wasActive = isActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessKiller] Eccezione nel loop principale");
            }

            await Task.Delay(_policy.PollingIntervalMs, stoppingToken);
        }

        if (watcher != null)
        {
            watcher.Stop();
            watcher.Dispose();
        }
    }

    /// <summary>
    /// RF3: Loop di monitoraggio del WatchdogService.
    /// Verifica che il servizio partner sia in esecuzione e lo riavvia se necessario.
    /// </summary>
    private async Task WatchdogLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Watchdog] Monitoraggio di '{ServiceName}' avviato", ServiceNames.Watchdog);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ServiceGuard.EnsureServiceRunning(
                    ServiceNames.Watchdog,
                    msg => _logger.LogWarning("{Message}", msg));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Watchdog] Errore nel monitoraggio");
            }

            await Task.Delay(_policy.WatchdogIntervalMs, stoppingToken);
        }
    }

    /// <summary>
    /// RF2: Loop di riscrittura periodica del file hosts.
    /// Garantisce che le regole DNS non vengano rimosse manualmente dall'utente.
    /// </summary>
    private async Task HostsFileLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[HostsFile] Loop riscrittura avviato - intervallo: {Interval}ms", _policy.HostsFileIntervalMs);

        bool wasActive = true; // Assume inizialmente attivo o comunque valuta al primo giro

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool isActive = IsBlockActive();

                if (isActive)
                {
                    HostsFileManager.ApplyDnsBlock(
                        _policy.BlockedDomains,
                        msg => _logger.LogDebug("{Message}", msg));
                    wasActive = true;
                }
                else if (wasActive)
                {
                    // Se siamo in pausa ma eravamo attivi, rimuoviamo i blocchi
                    HostsFileManager.RemoveDnsBlock(msg => _logger.LogInformation("[HostsFile] Rimosso blocco DNS (In Pausa): {Message}", msg));
                    HostsFileManager.FlushDnsCache(msg => _logger.LogDebug("{Message}", msg));
                    wasActive = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HostsFile] Errore riscrittura hosts");
            }

            await Task.Delay(_policy.HostsFileIntervalMs, stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== BlockerService in arresto ===");

        // Rimuove le modifiche al file hosts durante lo shutdown pulito
        // (NON durante un kill forzato, in quel caso il Watchdog riavvierà il servizio)
        HostsFileManager.RemoveDnsBlock(msg => _logger.LogInformation("{Message}", msg));
        HostsFileManager.FlushDnsCache(msg => _logger.LogInformation("{Message}", msg));

        // Rimuove la protezione ACL per consentire aggiornamenti
        if (_policy.EnableAclProtection)
        {
            AclProtection.Unlock(_installDirectory, msg => _logger.LogInformation("{Message}", msg));
        }

        return base.StopAsync(cancellationToken);
    }
}
