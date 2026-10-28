using System.IO;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Presentation.Services;

/// <summary>
/// Lets a second WorkBuddy launch talk to the first one over a per-user named pipe, instead of silently exiting.
/// The pipe is restricted to the current user, so other accounts on the machine cannot send commands.
/// </summary>
public sealed class SingleInstanceChannel(ILogger<SingleInstanceChannel> logger) : IDisposable
{
    public const string ActivateCommand = "activate";
    public const string ShowCommand = "show";
    public const string QuitCommand = "quit";

    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _disposed;

    public static string PipeName => $"WorkBuddy.{Environment.UserName}.Commands";

    /// <summary>Starts accepting commands from later launches. <paramref name="onCommand"/> runs on a background thread.</summary>
    public void StartListening(Action<string> onCommand)
    {
        _loop = Task.Run(() => ListenAsync(onCommand, _cts.Token));
    }

    /// <summary>Sends a command to the running instance. Returns false when nobody answered within the timeout.</summary>
    public static bool TrySignalExisting(string command, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect((int)timeout.TotalMilliseconds);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(command);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task ListenAsync(Action<string> onCommand, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    logger.LogInformation("Second launch signalled this instance: {Command}", command);
                    onCommand(command.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Single-instance pipe error; listening again");
            }
        }
    }

    public void Dispose()
    {
        // Called by OnExit and again by the DI container; the second call must be a no-op.
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Expected: the listener was cancelled during shutdown.
        }

        _cts.Dispose();
    }
}
