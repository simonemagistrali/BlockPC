using System.Text;

namespace BlockPC.Shared;

/// <summary>
/// RF2 - Blocco navigazione web tramite DNS Override.
/// Modifica programmaticamente il file hosts di Windows per reindirizzare
/// i domini in blacklist verso l'indirizzo di loopback locale (127.0.0.1).
/// </summary>
public static class HostsFileManager
{
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string BlockMarkerStart = "# >>> BlockPC - INIZIO BLOCCO DNS <<<";
    private const string BlockMarkerEnd = "# >>> BlockPC - FINE BLOCCO DNS <<<";
    private const string LoopbackAddress = "127.0.0.1";

    /// <summary>
    /// Riscrive la sezione BlockPC del file hosts con i domini attualmente in blacklist.
    /// Preserva le righe scritte manualmente dall'utente al di fuori dei marker.
    /// </summary>
    /// <param name="blockedDomains">Lista dei domini da bloccare</param>
    /// <param name="logger">Logger opzionale per tracciamento</param>
    public static void ApplyDnsBlock(
        IReadOnlyList<string> blockedDomains,
        Action<string>? logger = null)
    {
        if (blockedDomains.Count == 0) return;

        try
        {
            // Legge il contenuto attuale del file hosts
            string existingContent = File.Exists(HostsFilePath)
                ? File.ReadAllText(HostsFilePath, Encoding.UTF8)
                : string.Empty;

            // Rimuove la sezione BlockPC precedente (se presente)
            string cleanedContent = RemoveBlockPcSection(existingContent);

            // Costruisce il nuovo blocco DNS
            var sb = new StringBuilder(cleanedContent.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(BlockMarkerStart);

            foreach (var domain in blockedDomains)
            {
                string trimmed = domain.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Aggiunge sia il dominio base che il sottodominio www
                sb.AppendLine($"{LoopbackAddress} {trimmed}");
                if (!trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"{LoopbackAddress} www.{trimmed}");
                }
            }

            sb.AppendLine(BlockMarkerEnd);

            string newContent = sb.ToString();
            if (!string.Equals(existingContent, newContent, StringComparison.Ordinal))
            {
                File.WriteAllText(HostsFilePath, newContent, Encoding.UTF8);
                logger?.Invoke($"[HostsFileManager] File hosts aggiornato con {blockedDomains.Count} domini bloccati.");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.Invoke($"[HostsFileManager] Accesso negato al file hosts: {ex.Message}");
        }
        catch (IOException ex)
        {
            logger?.Invoke($"[HostsFileManager] Errore I/O file hosts: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[HostsFileManager] Errore inatteso: {ex.Message}");
        }
    }

    /// <summary>
    /// Rimuove la sezione BlockPC dal file hosts, ripristinando il contenuto originale.
    /// Chiamata durante lo shutdown pulito del servizio.
    /// </summary>
    public static void RemoveDnsBlock(Action<string>? logger = null)
    {
        try
        {
            if (!File.Exists(HostsFilePath)) return;

            string existingContent = File.ReadAllText(HostsFilePath, Encoding.UTF8);
            string cleanedContent = RemoveBlockPcSection(existingContent);
            File.WriteAllText(HostsFilePath, cleanedContent.TrimEnd() + Environment.NewLine, Encoding.UTF8);

            logger?.Invoke("[HostsFileManager] Blocco DNS rimosso dal file hosts.");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[HostsFileManager] Errore rimozione blocco DNS: {ex.Message}");
        }
    }

    /// <summary>
    /// Rimuove il blocco delimitato dai marker BlockPC dal contenuto del file hosts.
    /// </summary>
    private static string RemoveBlockPcSection(string content)
    {
        int startIndex = content.IndexOf(BlockMarkerStart, StringComparison.OrdinalIgnoreCase);
        int endIndex = content.IndexOf(BlockMarkerEnd, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0 || endIndex < 0) return content;

        int endOfMarker = endIndex + BlockMarkerEnd.Length;

        // Rimuove anche eventuali newline dopo il marker di fine
        while (endOfMarker < content.Length &&
               (content[endOfMarker] == '\r' || content[endOfMarker] == '\n'))
        {
            endOfMarker++;
        }

        return content[..startIndex] + content[endOfMarker..];
    }

    /// <summary>
    /// Svuota la cache DNS del sistema operativo dopo una modifica al file hosts.
    /// Necessario perché Windows mantiene una cache DNS in memoria.
    /// </summary>
    public static void FlushDnsCache(Action<string>? logger = null)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(5000);
            logger?.Invoke("[HostsFileManager] Cache DNS svuotata.");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[HostsFileManager] Errore flush DNS: {ex.Message}");
        }
    }
}
