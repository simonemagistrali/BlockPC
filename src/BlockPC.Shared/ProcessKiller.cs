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

        Process[] allProcesses;
        try
        {
            allProcesses = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            // RF5: Chiusura silenziosa - non possiamo ottenere la lista processi
            logger?.Invoke($"[ProcessKiller] Errore ottenimento processi: {ex.Message}");
            return;
        }

        foreach (var process in allProcesses)
        {
            try
            {
                string processName = process.ProcessName;

                foreach (var blocked in blockedProcessNames)
                {
                    if (string.Equals(processName, blocked, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.Invoke($"[ProcessKiller] Terminazione processo: {processName} (PID: {process.Id})");

                        process.Kill(entireProcessTree: true);
                        break;
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // RF5: Accesso negato - processo protetto dal kernel, ignorare silenziosamente
            }
            catch (InvalidOperationException)
            {
                // RF5: Processo già terminato tra la lettura e il kill, ignorare
            }
            catch (Exception ex)
            {
                // RF5: Qualsiasi altra eccezione viene gestita silenziosamente
                logger?.Invoke($"[ProcessKiller] Eccezione gestita su PID {process.Id}: {ex.Message}");
            }
            finally
            {
                try { process.Dispose(); }
                catch { /* Dispose sicuro */ }
            }
        }
    }
}
