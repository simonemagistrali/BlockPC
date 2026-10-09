namespace BlockPC.Shared;

/// <summary>
/// Nomi dei servizi Windows registrati nel sistema.
/// Utilizzati dal meccanismo di mutuo soccorso (watchdog) per avvio/monitoraggio reciproco.
/// </summary>
public static class ServiceNames
{
    public const string Blocker = "BlockPC.BlockerService";
    public const string Watchdog = "BlockPC.WatchdogService";
}
