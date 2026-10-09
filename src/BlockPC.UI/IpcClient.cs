using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using BlockPC.Shared;

namespace BlockPC.UI;

public class IpcClient
{
    private const string PipeName = "BlockPCPipe";

    public async Task<IpcStateResponse?> GetStateAsync()
    {
        var command = new { Action = "GET_STATE" };
        var responseJson = await SendCommandAsync(command);
        if (string.IsNullOrEmpty(responseJson) || responseJson.StartsWith("ERROR"))
            return null;

        return JsonSerializer.Deserialize<IpcStateResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<string> UpdatePolicyAsync(BlockingPolicy policy)
    {
        var command = new
        {
            Action = "UPDATE_POLICY",
            Payload = policy
        };
        return await SendCommandAsync(command);
    }

    private async Task<string> SendCommandAsync(object command)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // Timeout di 2 secondi
            await pipeClient.ConnectAsync(2000);

            using var reader = new StreamReader(pipeClient);
            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

            var message = JsonSerializer.Serialize(command);
            await writer.WriteLineAsync(message);

            return await reader.ReadLineAsync() ?? "ERROR: Empty response";
        }
        catch (TimeoutException)
        {
            return "ERROR: Timeout connessione al servizio. Il servizio è in esecuzione?";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}

public class IpcStateResponse
{
    public bool IsActive { get; set; }
    public BlockingPolicy? Policy { get; set; }
}
