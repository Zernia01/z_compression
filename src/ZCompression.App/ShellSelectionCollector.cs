using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace ZCompression.App;

internal static class ShellSelectionCollector
{
    // Legacy Explorer verbs start one process per selected item. Gather them before
    // showing a single operation window; compression receives the complete group.
    internal static async Task<string[]?> CollectAsync(string command, string[] sources, string? testPipe = null)
    {
        var name = testPipe ?? "z-compression-shell-" + Environment.UserName + "-" + command.TrimStart('-');
        NamedPipeServerStream server;
        try
        {
            server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance);
        }
        catch (IOException)
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(10000);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(sources));
            if (await reader.ReadLineAsync() != "OK") throw new IOException("The shell selection was not accepted.");
            return null;
        }
        using (server)
        {
            var paths = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                using var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(1200));
                try { await server.WaitForConnectionAsync(quiet.Token); }
                catch (OperationCanceledException) { break; }
                using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using (var reader = new StreamReader(server, leaveOpen: true))
                using (var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true })
                {
                    var line = await reader.ReadLineAsync(readTimeout.Token);
                    var incoming = JsonSerializer.Deserialize<string[]>(line ?? "[]") ?? [];
                    foreach (var path in incoming) paths.Add(path);
                    await writer.WriteLineAsync("OK");
                }
                server.Disconnect();
            }
            return paths.ToArray();
        }
    }
}
