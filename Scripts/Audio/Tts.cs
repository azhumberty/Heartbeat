using Godot;

namespace Heartbeat;

public sealed class NpcVoiceProfile
{
    public string VoiceId { get; set; } = "";
    public float Pitch { get; set; } = 1f;
    public float Rate { get; set; } = 1f;
    public string Language { get; set; } = "pt";
}

public interface ITtsProvider
{
    Task SpeakAsync(string text, NpcVoiceProfile voice, float volume, Node? host, CancellationToken ct);
    void Stop();
}

public sealed class OfflineTtsProvider : ITtsProvider
{
    public Task SpeakAsync(string text, NpcVoiceProfile voice, float volume, Node? host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
    public void Stop() { }
}

/// <summary>OS voices (Windows SAPI / Godot DisplayServer). No key. Offline.</summary>
public sealed class SystemTtsProvider : ITtsProvider
{
    public static bool Available
    {
        get
        {
            try { return DisplayServer.GetName() != "headless" && DisplayServer.TtsGetVoices().Count > 0; }
            catch { return false; }
        }
    }

    public Task SpeakAsync(string text, NpcVoiceProfile voice, float volume, Node? host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || !Available) return Task.CompletedTask;
        var id = Pick(voice);
        if (id.Length == 0) return Task.CompletedTask;
        try
        {
            DisplayServer.TtsStop();
            DisplayServer.TtsSpeak(
                Clip(text, 420),
                id,
                Mathf.Clamp((int)(volume * 100), 10, 100),
                Mathf.Clamp(voice.Pitch, 0.5f, 2f),
                Mathf.Clamp(voice.Rate, 0.4f, 2.5f),
                0,
                true);
        }
        catch (Exception e) { GD.PushWarning("[TTS] sistema: " + e.GetType().Name); }
        return Task.CompletedTask;
    }

    public void Stop()
    {
        try { if (DisplayServer.GetName() != "headless") DisplayServer.TtsStop(); } catch { }
    }

    public static string Pick(NpcVoiceProfile voice)
    {
        try
        {
            var all = DisplayServer.TtsGetVoices();
            if (all.Count == 0) return "";
            var lang = string.IsNullOrWhiteSpace(voice.Language) ? "pt" : voice.Language;
            var match = new List<string>();
            var any = new List<string>();
            foreach (Godot.Collections.Dictionary d in all)
            {
                var id = Read(d, "id");
                if (id.Length == 0) continue;
                any.Add(id);
                var language = Read(d, "language");
                if (language.StartsWith(lang, StringComparison.OrdinalIgnoreCase) || language.Contains("pt", StringComparison.OrdinalIgnoreCase))
                    match.Add(id);
            }
            var pool = match.Count > 0 ? match : any;
            if (pool.Count == 0) return "";
            int idx = Math.Abs(Stable(voice.VoiceId)) % pool.Count;
            return pool[idx];
        }
        catch { return ""; }
    }

    static string Read(Godot.Collections.Dictionary d, string key) => d.ContainsKey(key) ? d[key].AsString() : "";
    static int Stable(string s) { unchecked { int h = 23; foreach (var c in s ?? "") h = h * 31 + c; return h & 0x7fffffff; } }
    static string Clip(string text, int n) { var t = text.Replace('\n', ' ').Trim(); return t.Length <= n ? t : t[..n].Trim() + "."; }
}

/// <summary>Portuguese MP3 clips (Google translate TTS). Optional. Cached in user://TtsCache.</summary>
public sealed class HttpTtsProvider : ITtsProvider
{
    static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    AudioStreamPlayer? _player;

    public async Task SpeakAsync(string text, NpcVoiceProfile voice, float volume, Node? host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var clip = text.Replace('\n', ' ').Trim();
        if (clip.Length > 180) clip = clip[..180].Trim();
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clip)))[..16].ToLowerInvariant();
        var dir = ProjectSettings.GlobalizePath("user://TtsCache");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, key + ".mp3");
        byte[] bytes;
        if (File.Exists(path)) bytes = await File.ReadAllBytesAsync(path, ct);
        else
        {
            var url = "https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=pt-BR&q=" + Uri.EscapeDataString(clip);
            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Heartbeat/1.0");
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return;
            bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length < 64) return;
            await File.WriteAllBytesAsync(path, bytes, ct);
        }
        if (ct.IsCancellationRequested || host == null || !GodotObject.IsInstanceValid(host)) return;
        _player = host.GetNodeOrNull<AudioStreamPlayer>("TtsPlayer");
        if (_player == null)
        {
            _player = new AudioStreamPlayer { Name = "TtsPlayer" };
            host.AddChild(_player);
        }
        _player.Stop();
        _player.Stream = new AudioStreamMP3 { Data = bytes };
        _player.VolumeDb = Mathf.LinearToDb(Math.Max(.05f, volume));
        _player.Play();
    }

    public void Stop() => _player?.Stop();
}

public static class NpcVoice
{
    public static NpcVoiceProfile Ensure(CharacterData c)
    {
        c.Voice ??= new NpcVoiceProfile();
        if (!string.IsNullOrWhiteSpace(c.Voice.VoiceId) && c.Voice.Pitch > 0.2f) return c.Voice;
        int h = Stable(c.Id + "|" + c.Name);
        bool grave = c.PersonalityProfile.Pride > 60 || c.Id.Contains("kael", StringComparison.OrdinalIgnoreCase);
        bool light = c.Tags.Contains("merchant") || c.PersonalityProfile.Extraversion > 70;
        c.Voice.Pitch = grave ? 0.82f : light ? 1.14f : 0.90f + (h % 17) / 80f;
        c.Voice.Rate = 0.92f + (h % 11) / 90f;
        c.Voice.VoiceId = c.Id;
        c.Voice.Language = "pt";
        return c.Voice;
    }

    public static ITtsProvider From(GameSettings settings)
    {
        if (!settings.UseTts) return new OfflineTtsProvider();
        if (settings.UseOnlineTts) return new HttpTtsProvider();
        return SystemTtsProvider.Available ? new SystemTtsProvider() : new HttpTtsProvider();
    }

    public static async Task Speak(Node host, CharacterData person, string text, GameSettings settings, CancellationToken ct)
    {
        if (!settings.UseTts || string.IsNullOrWhiteSpace(text)) return;
        try { await From(settings).SpeakAsync(text, Ensure(person), settings.Volume, host, ct); }
        catch (OperationCanceledException) { }
        catch (Exception e) { GD.PushWarning("[TTS] " + e.GetType().Name); }
    }

    public static void Stop(GameSettings settings)
    {
        try { From(settings).Stop(); } catch { }
        try { if (DisplayServer.GetName() != "headless") DisplayServer.TtsStop(); } catch { }
    }

    static int Stable(string s) { unchecked { int h = 23; foreach (var ch in s ?? "") h = h * 31 + ch; return h & 0x7fffffff; } }
}
