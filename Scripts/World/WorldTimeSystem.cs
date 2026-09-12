using Godot;
namespace Heartbeat;

/// <summary>One authority for clock, day rollover and period transitions.</summary>
public partial class WorldTimeSystem : Node
{
    public GameSave Game { get; set; } = new();
    /// <summary>Game minutes advanced per real second at 1×.</summary>
    public float MinutesPerSecond { get; set; } = 1f;
    float _timeScale = 1f;
    bool _paused;
    public float TimeScale { get => _timeScale; set { _timeScale = Math.Clamp(value, .5f, 4f); Game.Settings.WorldTimeScale = _timeScale; } }
    public bool Paused { get => _paused; set { _paused = value; Game.Settings.WorldTimePaused = value; } }
    public event Action<string>? PeriodChanged;
    public event Action? ClockChanged;
    float _lastDisplayedMinute = -1;

    public float Hour => Game.WorldMinutes / 60f;
    public string ClockText => $"{Mathf.FloorToInt(Hour):00}:{Mathf.FloorToInt(Game.WorldMinutes % 60):00}";
    public float DayFraction => Game.WorldMinutes / 1440f;

    public override void _Ready()
    {
        if (Game.WorldMinutes < 0) Game.WorldMinutes = 8 * 60;
        _timeScale = Math.Clamp(Game.Settings.WorldTimeScale, .5f, 4f);
        _paused = Game.Settings.WorldTimePaused;
        SyncPeriod(false);
    }

    public override void _Process(double delta)
    {
        if (!Paused) AdvanceMinutes((float)delta * MinutesPerSecond * TimeScale);
    }

    public void AdvanceMinutes(float minutes)
    {
        if (minutes <= 0) return;
        Game.WorldMinutes += minutes;
        while (Game.WorldMinutes >= 1440) { Game.WorldMinutes -= 1440; Game.Day++; }
        SyncPeriod(true);
        var displayed = Mathf.Floor(Game.WorldMinutes);
        if (displayed != _lastDisplayedMinute) { _lastDisplayedMinute = displayed; ClockChanged?.Invoke(); }
    }

    public void SkipHours(float hours) => AdvanceMinutes(hours * 60);
    public void CycleSpeed() => TimeScale = TimeScale < 1 ? 1 : TimeScale < 4 ? 4 : .5f;

    void SyncPeriod(bool notify)
    {
        var hour = Hour;
        var period = hour >= 6 && hour < 12 ? "Morning" : hour < 17 ? "Afternoon" : hour < 21 ? "Evening" : "Night";
        if (period == Game.Period) return;
        Game.Period = period;
        if (notify) PeriodChanged?.Invoke(period);
    }
}
