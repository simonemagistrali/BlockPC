using System.Diagnostics;
using System.ServiceProcess;

namespace BlockPC.Shared;

/// <summary>
/// RF3 - Meccanismo di Mutuo Soccorso (Watchdog).
/// Monitora lo stato di un servizio partner e lo riavvia se non è in esecuzione.
/// Utilizza il Service Control Manager (SCM) nativo di Windows.
/// </summary>
public static class ServiceGuard
{
    /// <summary>
    /// Verifica se il servizio partner è in esecuzione.
    /// In caso contrario, tenta di riavviarlo tramite sc.exe.
    /// </summary>
    /// <param name="targetServiceName">Nome del servizio Windows da monitorare</param>
    /// <param name="logger">Logger opzionale per tracciamento</param>
    /// <returns>true se il servizio è in esecuzione o è stato riavviato con successo</returns>
    public static bool EnsureServiceRunning(
        string targetServiceName,
        Action<string>? logger = null)
    {
        try
        {
            using var sc = new ServiceController(targetServiceName);

            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            logger?.Invoke($"[ServiceGuard] Servizio '{targetServiceName}' non in esecuzione (stato: {sc.Status}). Tentativo di riavvio...");

            // Se il servizio è in stato di stop, avvialo direttamente
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                logger?.Invoke($"[ServiceGuard] Servizio '{targetServiceName}' riavviato con successo.");
                return true;
            }

            // Per altri stati (Paused, StopPending, etc.), usa sc.exe come fallback
            return StartViaScExe(targetServiceName, logger);
        }
        catch (InvalidOperationException ex)
        {
            // Il servizio non è installato o non è accessibile
            logger?.Invoke($"[ServiceGuard] Servizio '{targetServiceName}' non trovato: {ex.Message}");

            // Tentativo via sc.exe come ultima risorsa
            return StartViaScExe(targetServiceName, logger);
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            logger?.Invoke($"[ServiceGuard] Timeout avvio servizio '{targetServiceName}'.");
            return false;
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[ServiceGuard] Errore monitoraggio '{targetServiceName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Avvia il servizio tramite il comando sc.exe (fallback).
    /// Utile quando il ServiceController non riesce a gestire lo stato corrente.
    /// </summary>
    private static bool StartViaScExe(string serviceName, Action<string>? logger)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"start \"{serviceName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(10_000);

            if (process.ExitCode == 0)
            {
                logger?.Invoke($"[ServiceGuard] Servizio '{serviceName}' avviato via sc.exe.");
                return true;
            }
            else
            {
                string error = process.StandardError.ReadToEnd();
                logger?.Invoke($"[ServiceGuard] sc.exe fallito per '{serviceName}': {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[ServiceGuard] Errore esecuzione sc.exe: {ex.Message}");
            return false;
        }
    }
}
