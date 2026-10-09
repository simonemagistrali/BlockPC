using System.Security.AccessControl;
using System.Security.Principal;

namespace BlockPC.Shared;

/// <summary>
/// RNF3 - Protezione Access Control List (ACL).
/// Modifica i permessi NTFS della cartella di installazione per impedire
/// la disinstallazione forzata a runtime, aggiungendo una regola di Deny
/// per Scrittura ed Eliminazione al gruppo Administrators.
/// </summary>
public static class AclProtection
{
    /// <summary>
    /// Applica la protezione ACL alla cartella specificata.
    /// Aggiunge una regola Deny per Write e Delete al gruppo Administrators.
    /// </summary>
    /// <param name="directoryPath">Percorso della cartella da proteggere</param>
    /// <param name="logger">Logger opzionale per tracciamento</param>
    public static void LockDown(string directoryPath, Action<string>? logger = null)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                logger?.Invoke($"[AclProtection] Directory non trovata: {directoryPath}");
                return;
            }

            var dirInfo = new DirectoryInfo(directoryPath);
            var security = dirInfo.GetAccessControl();

            // Identità del gruppo Administrators (invariante alla lingua del sistema)
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            // Regola di negazione: impedisce Scrittura, Eliminazione e Modifica permessi
            var denyRule = new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.Write
                | FileSystemRights.Delete
                | FileSystemRights.DeleteSubdirectoriesAndFiles
                | FileSystemRights.ChangePermissions,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny);

            security.AddAccessRule(denyRule);
            dirInfo.SetAccessControl(security);

            logger?.Invoke($"[AclProtection] Protezione ACL applicata a: {directoryPath}");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.Invoke($"[AclProtection] Permessi insufficienti: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[AclProtection] Errore applicazione ACL: {ex.Message}");
        }
    }

    /// <summary>
    /// Rimuove la protezione ACL dalla cartella specificata.
    /// Rimuove la regola Deny per il gruppo Administrators.
    /// Utile per la disinstallazione controllata.
    /// </summary>
    /// <param name="directoryPath">Percorso della cartella da sbloccare</param>
    /// <param name="logger">Logger opzionale per tracciamento</param>
    public static void Unlock(string directoryPath, Action<string>? logger = null)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                logger?.Invoke($"[AclProtection] Directory non trovata: {directoryPath}");
                return;
            }

            var dirInfo = new DirectoryInfo(directoryPath);
            var security = dirInfo.GetAccessControl();

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            // Rimuove TUTTE le regole Deny per gli Administrators
            security.PurgeAccessRules(adminsSid);

            // Ri-aggiunge i permessi standard di lettura/esecuzione
            var allowRule = new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            security.AddAccessRule(allowRule);
            dirInfo.SetAccessControl(security);

            logger?.Invoke($"[AclProtection] Protezione ACL rimossa da: {directoryPath}");
        }
        catch (Exception ex)
        {
            logger?.Invoke($"[AclProtection] Errore rimozione ACL: {ex.Message}");
        }
    }
}
