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
    /// RF1/RF4: Loop di polling processi.
    /// Interroga il sistema ogni PollingIntervalMs e termina i processi in blacklist.
    /// </summary>
    private async Task ProcessKillerLoopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ProcessKiller] Loop avviato - intervallo: {Interval}ms", _policy.PollingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ProcessKiller.KillBlockedProcesses(
                    _policy.BlockedProcesses,
                    msg => _logger.LogDebug("{Message}", msg));
            }
            catch (Exception ex)
            {
                // RF5: Cattura qualsiasi eccezione per proteggere il loop
                _logger.LogError(ex, "[ProcessKiller] Eccezione nel loop principale");
            }

            // RNF2: Rilascio temporaneo del thread
            await Task.Delay(_policy.PollingIntervalMs, stoppingToken);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                HostsFileManager.ApplyDnsBlock(
                    _policy.BlockedDomains,
                    msg => _logger.LogDebug("{Message}", msg));
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
