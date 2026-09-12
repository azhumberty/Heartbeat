using Godot;
namespace Heartbeat;

/// <summary>Very quiet synthesized wind bed; no external audio file or license is required.</summary>
public partial class ForestAmbience : Node
{
    public WorldTimeSystem Time { get; set; } = null!;
    AudioStreamPlayer _player = null!;
    AudioStreamGeneratorPlayback? _playback;
    readonly Random _random = new(7139);
    float _filtered;

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless") { SetProcess(false); return; }
        var stream = new AudioStreamGenerator { MixRate = 22050, BufferLength = .25f };
        _player = new AudioStreamPlayer { Stream = stream, VolumeDb = -29 };
        AddChild(_player); _player.Play();
        _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _Process(double delta)
    {
        if (_playback == null) return;
        int frames = _playback.GetFramesAvailable();
        float night = Time != null && (Time.Hour >= 20 || Time.Hour < 6) ? .7f : .35f;
        for (int i = 0; i < frames; i++)
        {
            float noise = (float)(_random.NextDouble() * 2 - 1);
            _filtered = Mathf.Lerp(_filtered, noise, .012f);
            float gust = .6f + .4f * Mathf.Sin((float)Godot.Time.GetTicksMsec() * .00018f);
            float sample = _filtered * .12f * gust * (1 + night * .25f);
            _playback.PushFrame(new Vector2(sample, sample * .96f));
        }
    }
}
