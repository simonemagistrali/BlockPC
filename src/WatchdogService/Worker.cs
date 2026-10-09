using BlockPC.Shared;

namespace WatchdogService;

/// <summary>
/// Servizio di Supporto (WatchdogService).
/// 
/// Componente secondario il cui unico scopo è monitorare lo stato di salute
/// del BlockerService e riavviarlo istantaneamente in caso di terminazione imprevista.
/// 
/// RF3: Meccanismo di Mutuo Soccorso - lato Watchdog.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly int _watchdogIntervalMs;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Legge l'intervallo di polling dalla configurazione (default: 1000ms)
        _watchdogIntervalMs = configuration.GetValue("WatchdogIntervalMs", 1000);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== WatchdogService avviato ===");
        _logger.LogInformation("Monitoraggio di '{ServiceName}' ogni {Interval}ms",
            ServiceNames.Blocker, _watchdogIntervalMs);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Attende 2 secondi prima di iniziare il monitoraggio
        // per dare tempo al BlockerService di avviarsi
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // RF3: Verifica che il BlockerService sia in esecuzione
                bool isRunning = ServiceGuard.EnsureServiceRunning(
                    ServiceNames.Blocker,
                    msg => _logger.LogWarning("{Message}", msg));

                if (!isRunning)
                {
                    _logger.LogCritical(
                        "[Watchdog] ALLARME: BlockerService non in esecuzione! Tentativo di riavvio effettuato.");
                }
            }
            catch (Exception ex)
            {
                // RF5: Gestione silenziosa - il watchdog non deve mai crashare
                _logger.LogError(ex, "[Watchdog] Errore nel ciclo di monitoraggio");
            }

            // RNF2: Rilascio temporaneo del thread
            await Task.Delay(_watchdogIntervalMs, stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== WatchdogService in arresto ===");
        return base.StopAsync(cancellationToken);
    }
}
