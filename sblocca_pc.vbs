' SbloccaPC - Script VBS per arrestare e rimuovere i servizi BlockPC
' Non usa cmd.exe ne powershell.exe, quindi non viene terminato dal BlockerService.

Set objShell = CreateObject("Shell.Application")
Set wshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

' Auto-elevazione con privilegi di Amministratore (UAC)
If WScript.Arguments.Named.Exists("elevated") = False Then
    objShell.ShellExecute "wscript.exe", """" & WScript.ScriptFullName & """ /elevated", "", "runas", 1
    WScript.Quit
End If

' 1. Disabilita i servizi per evitare riavvii automatici dal service control manager
wshShell.Run "sc config BlockPC.WatchdogService start=disabled", 0, True
wshShell.Run "sc config BlockPC.BlockerService start=disabled", 0, True

' 2. Arresta i servizi Windows
wshShell.Run "sc stop BlockPC.WatchdogService", 0, True
wshShell.Run "sc stop BlockPC.BlockerService", 0, True

' 3. Forza la chiusura immediata dei processi in esecuzione
wshShell.Run "taskkill /F /IM WatchdogService.exe", 0, True
wshShell.Run "taskkill /F /IM BlockerService.exe", 0, True

' 4. Rimuove i servizi registrati in Windows
wshShell.Run "sc delete BlockPC.WatchdogService", 0, True
wshShell.Run "sc delete BlockPC.BlockerService", 0, True

' 5. Ripristina il file hosts rimuovendo i blocchi DNS di BlockPC
hostsPath = "C:\Windows\System32\drivers\etc\hosts"
If fso.FileExists(hostsPath) Then
    On Error Resume Next
    Set ts = fso.OpenTextFile(hostsPath, 1)
    content = ts.ReadAll
    ts.Close

    startMarker = "# >>> BlockPC - INIZIO BLOCCO DNS <<<"
    endMarker = "# >>> BlockPC - FINE BLOCCO DNS <<<"

    sPos = InStr(1, content, startMarker, vbTextCompare)
    ePos = InStr(1, content, endMarker, vbTextCompare)

    If sPos > 0 And ePos > 0 Then
        beforeText = Left(content, sPos - 1)
        afterText = Mid(content, ePos + Len(endMarker))
        cleaned = Trim(beforeText & afterText) & vbCrLf

        Set tsOut = fso.OpenTextFile(hostsPath, 2)
        tsOut.Write cleaned
        tsOut.Close
    End If
    On Error GoTo 0
End If

' 6. Svuota la cache DNS di Windows
wshShell.Run "ipconfig /flushdns", 0, True

MsgBox "OPERAZIONE COMPLETATA!" & vbCrLf & vbCrLf & _
       "- BlockerService e WatchdogService sono stati arrestati e rimossi." & vbCrLf & _
       "- I terminali e le app di sistema (PowerShell, CMD, Task Manager, ecc.) sono ora sbloccati." & vbCrLf & _
       "- Il file hosts e la navigazione web sono stati ripristinati.", _
       vbInformation + vbOKOnly, "BlockPC - PC Sbloccato con Successo"
