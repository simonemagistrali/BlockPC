using System.Diagnostics;

namespace BlockPC.Shared;

/// <summary>
/// RF1/RF4/RF5 - Motore di terminazione processi.
/// Interroga il sistema operativo e termina i processi in blacklist,
/// gestendo silenziosamente le eccezioni di accesso negato (RF5).
/// </summary>
public static class ProcessKiller
{
    /// <summary>
    /// Scansiona tutti i processi in esecuzione e termina quelli corrispondenti alla blacklist.
    /// Il confronto è case-insensitive e senza estensione .exe.
    /// </summary>
    /// <param name="blockedProcessNames">Nomi dei processi da terminare (senza .exe)</param>
    /// <param name="logger">Logger opzionale per tracciamento</param>
    public static void KillBlockedProcesses(
        IReadOnlyList<string> blockedProcessNames,
        Action<string>? logger = null)
    {
        if (blockedProcessNames.Count == 0) return;

        foreach (var blocked in blockedProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(blocked);
                foreach (var process in processes)
                {
                    try
                    {
                        logger?.Invoke($"[ProcessKiller] Terminazione processo: {process.ProcessName} (PID: {process.Id})");
                        process.Kill(entireProcessTree: true);
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        // RF5: Accesso negato
                    }
                    catch (InvalidOperationException)
                    {
                        // RF5: Processo già terminato
                    }
                    catch (Exception ex)
                    {
                        logger?.Invoke($"[ProcessKiller] Eccezione gestita su PID {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        try { process.Dispose(); }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"[ProcessKiller] Errore ottenimento processi per {blocked}: {ex.Message}");
            }
        }
    }
}
