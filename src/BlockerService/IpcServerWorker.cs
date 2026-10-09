using System.IO.Pipes;
using System.Text.Json;
using BlockPC.Shared;
using Microsoft.Extensions.Options;

namespace BlockerService;

public class IpcServerWorker : BackgroundService
{
    private readonly ILogger<IpcServerWorker> _logger;
    private readonly BlockingPolicy _policy;
    private readonly string _settingsFilePath;

    public IpcServerWorker(ILogger<IpcServerWorker> logger, IOptions<BlockingPolicy> policy)
    {
        _logger = logger;
        _policy = policy.Value;
        
        // Percorso in cui salvare la policy
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IPC Server in ascolto su BlockPCPipe");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    "BlockPCPipe",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(pipeServer);
                using var writer = new StreamWriter(pipeServer) { AutoFlush = true };

                var message = await reader.ReadLineAsync(stoppingToken);
                if (message != null)
                {
                    var response = HandleMessage(message);
                    await writer.WriteLineAsync(response);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la gestione della IPC pipe");
            }
        }
    }

    private string HandleMessage(string message)
    {
        try
        {
            // Deserialize command
            var command = JsonSerializer.Deserialize<IpcCommand>(message);
            if (command == null) return "ERROR: Invalid command format";

            bool isBlockActive = IsBlockActive();

            switch (command.Action)
            {
                case "GET_STATE":
                    return JsonSerializer.Serialize(new
                    {
                        IsActive = isBlockActive,
                        Policy = _policy
                    });

                case "UPDATE_POLICY":
                    if (command.Payload == null) return "ERROR: Missing payload";
                    var newPolicy = JsonSerializer.Deserialize<BlockingPolicy>(command.Payload.Value.GetRawText());
                    
                    if (newPolicy == null) return "ERROR: Invalid policy data";

                    // Validazione Append-Only (RS2)
                    if (isBlockActive)
                    {
                        // Se blocco attivo, NON si possono rimuovere processi o domini
                        if (newPolicy.BlockedProcesses.Count < _policy.BlockedProcesses.Count ||
                            !_policy.BlockedProcesses.All(p => newPolicy.BlockedProcesses.Contains(p)))
                        {
                            return "ERROR: Non è possibile rimuovere processi mentre il blocco è attivo.";
                        }

                        if (newPolicy.BlockedDomains.Count < _policy.BlockedDomains.Count ||
                            !_policy.BlockedDomains.All(d => newPolicy.BlockedDomains.Contains(d)))
                        {
                            return "ERROR: Non è possibile rimuovere domini mentre il blocco è attivo.";
                        }

                        // NON si possono ridurre le fasce orarie (rimuovere giorni o stringere orari)
                        if (newPolicy.ActiveDays.Count < _policy.ActiveDays.Count ||
                            !_policy.ActiveDays.All(d => newPolicy.ActiveDays.Contains(d)))
                        {
                            return "ERROR: Non è possibile rimuovere giorni mentre il blocco è attivo.";
                        }
                        
                        if (newPolicy.StartTime > _policy.StartTime || newPolicy.EndTime < _policy.EndTime)
                        {
                            return "ERROR: Non è possibile ridurre l'orario di blocco mentre il blocco è attivo.";
                        }

                        // Il Quick Block può solo essere esteso
                        if (_policy.QuickBlockEndTime.HasValue)
                        {
                            if (!newPolicy.QuickBlockEndTime.HasValue || newPolicy.QuickBlockEndTime.Value < _policy.QuickBlockEndTime.Value)
                            {
                                return "ERROR: Non è possibile ridurre o annullare il Blocco Rapido.";
                            }
                        }
                    }

                    // Applica le modifiche in memoria
                    UpdateInMemoryPolicy(newPolicy);

                    // Salva su file (rimuovendo temporaneamente l'ACL se necessario, ma dato che il servizio è LocalSystem, potrebbe scriverlo se è proprietario)
                    // In questo caso gestiamo il salvataggio su appsettings.json.
                    SavePolicyToFile();

                    return "OK";

                default:
                    return "ERROR: Unknown action";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nella gestione del comando IPC");
            return "ERROR: " + ex.Message;
        }
    }

    private void UpdateInMemoryPolicy(BlockingPolicy newPolicy)
    {
        _policy.BlockedProcesses = newPolicy.BlockedProcesses;
        _policy.BlockedDomains = newPolicy.BlockedDomains;
        _policy.ActiveDays = newPolicy.ActiveDays;
        _policy.StartTime = newPolicy.StartTime;
        _policy.EndTime = newPolicy.EndTime;
        _policy.QuickBlockEndTime = newPolicy.QuickBlockEndTime;
        _policy.EnableAclProtection = newPolicy.EnableAclProtection;
    }

    private void SavePolicyToFile()
    {
        // Rimuove temporaneamente la protezione se attiva?
        // Il servizio esegue come LocalSystem, quindi dovrebbe poter scrivere, ma se le ACL lo impediscono...
        // Supponiamo che il servizio possa scrivere perché ha privilegi massimi.
        try
        {
            if (_policy.EnableAclProtection)
            {
                AclProtection.Unlock(AppContext.BaseDirectory, _ => { });
            }

            var configJson = File.ReadAllText(_settingsFilePath);
            var jsonDocument = JsonDocument.Parse(configJson);
            var configObject = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson) ?? new();
            
            // Aggiorna la sezione BlockingPolicy
            configObject["BlockingPolicy"] = _policy;

            var updatedJson = JsonSerializer.Serialize(configObject, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, updatedJson);
        }
        finally
        {
            if (_policy.EnableAclProtection)
            {
                AclProtection.LockDown(AppContext.BaseDirectory, _ => { });
            }
        }
    }

    private bool IsBlockActive()
    {
        var now = DateTime.Now;

        if (_policy.QuickBlockEndTime.HasValue && _policy.QuickBlockEndTime.Value > now)
        {
            return true;
        }

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
}

public class IpcCommand
{
    public string Action { get; set; } = string.Empty;
    public JsonElement? Payload { get; set; }
}
