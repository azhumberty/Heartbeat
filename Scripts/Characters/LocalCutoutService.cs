using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace Heartbeat;

public sealed class LocalCutoutService
{
    public const string Model = "u2netp";
    public string PythonPath { get; set; } = System.Environment.GetEnvironmentVariable("HEARTBEAT_PYTHON")
        ?? ProjectSettings.GlobalizePath("res://.tools/rembg/Scripts/python.exe");
    public bool Available => File.Exists(PythonPath);

    public async Task<string> RemoveAsync(string source, string cacheFolder, CancellationToken token)
    {
        if (!Available) throw new InvalidOperationException("Ative o recorte local com Tools/Install-Cutout.ps1. Você pode usar PNG transparente agora.");
        var bytes = await File.ReadAllBytesAsync(source, token);
        var key = Convert.ToHexString(SHA256.HashData(bytes.Concat(Encoding.UTF8.GetBytes("rembg-2.0.67:u2netp:mask-v1")).ToArray()));
        Directory.CreateDirectory(cacheFolder);
        var target = Path.Combine(cacheFolder, key + ".png");
        if (File.Exists(target)) return target;
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp.png";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var start = new ProcessStartInfo(PythonPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add(ProjectSettings.GlobalizePath("res://Tools/remove_background.py"));
            start.ArgumentList.Add(source); start.ArgumentList.Add(temporary);
            start.Environment["U2NET_HOME"] = ProjectSettings.GlobalizePath("res://.tools/models");
            using var process = Process.Start(start) ?? throw new IOException("Não foi possível iniciar o recorte.");
            // Drain both streams without logging file paths or source content.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            await process.WaitForExitAsync(timeout.Token); await stdout; await stderr;
            if (process.ExitCode != 0 || !File.Exists(temporary)) throw new IOException("Recorte falhou. Confira a instalação e a conexão para o primeiro download do modelo. Original preservado.");
            File.Move(temporary, target, true);
            return target;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Recorte excedeu 5 minutos. Tente novamente; o original foi preservado."); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
