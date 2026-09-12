using Godot;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Heartbeat;

public partial class WorldController : Node3D
{
    public GameSave? InitialSave { get; set; }
    readonly SaveManager _saves = new();
    readonly CharacterRepository _characters = new();
    readonly PortraitCache _portraits = new();
    GameSave _game = new();
    PlayerController _player = null!;
    ChunkManager _chunks = null!;
    NpcManager _npcs = null!;
    WorldTimeSystem _time = null!;
    SkyController _sky = null!;
    Control _ui = null!;
    Control? _screen;
    Label _hint = null!, _clock = null!, _healthText = null!, _manaText = null!;
    ProgressBar _healthBar = null!, _manaBar = null!;
    PanelContainer _quickMenu = null!;
    NpcActor? _near;
    Node3D? _nearPoi;
    EncounterManager _encounters=null!;
    bool _combatTransition;
    CombatArenaController? _combat;
    public bool IsInCombat=>_combat!=null||_combatTransition;

    public override void _Ready()
    {
        _game = InitialSave ?? _saves.Load() ?? new();
        _saves.Migrate(_game);
        if (_game.CharacterIds.Count == 0) _game.CharacterIds = _characters.List().Select(c => c.Id).ToList();

        if (_game.WorldSeed == 0) _game.WorldSeed = new Random().NextInt64();

        BuildTime();
        BuildChunkSystem();
        BuildPlayer();
        BuildSky();
        AddChild(new ForestAmbience { Time = _time });
        BuildNpcs();
        _time.PeriodChanged += OnPeriodChanged;
        BuildUi();
        UpdateClock();
        _encounters=new EncounterManager {Game=_game,Chunks=_chunks,Player=_player,CanEngage=()=>_screen==null&&!_quickMenu.Visible&&!IsInCombat};
        _encounters.Engaged=enemy=>_ = EnterCombat(enemy);
        AddChild(_encounters);
        if(_game.ActiveCombat!=null)CallDeferred(nameof(ResumeCombat));
    }

    void BuildTime()
    {
        _time = new WorldTimeSystem { Game = _game };
        _time.ClockChanged += UpdateClock;
        AddChild(_time);
    }

    void BuildChunkSystem()
    {
        var generator = new ChunkGenerator(_game.WorldSeed);
        _chunks = new ChunkManager { Generator = generator };
        AddChild(_chunks);
    }

    void BuildPlayer()
    {
        float ground = TerrainHeight.Sample(_game.PlayerX, _game.PlayerZ, _game.WorldSeed);
        _player = new PlayerController
        {
            Position = new(_game.PlayerX, ground + .15f, _game.PlayerZ)
        };
        AddChild(_player);
        _player.SetInitialView(_game.FirstPerson, _game.PlayerRotationY);

        _chunks.UpdateAroundPlayer(_player.Position);
        for (int i = 0; i < 100; i++) _chunks.ProcessBuildQueue();
    }

    void BuildSky()
    {
        _sky = new SkyController { Time = _time, Follow = _player };
        AddChild(_sky);
    }

    void BuildNpcs()
    {
        _npcs = new NpcManager
        {
            Chunks = _chunks,
            Portraits = _portraits
        };
        AddChild(_npcs);

        int i = 0;
        foreach (var id in _game.CharacterIds)
        {
            var data = _characters.Load(id);
            if (data == null) continue;
            if (!_game.CharacterStates.TryGetValue(id, out var state))
                _game.CharacterStates[id] = state = new CharacterState();

            if (state.WorldX == 0 && state.WorldZ == 0)
            {
                var loc = state.CurrentLocation ?? "Cafe";
                state.WorldX = loc == "Cafe" ? -5 : loc == "Park" ? 1.2f : 0;
                state.WorldZ = loc == "Cafe" ? -3 : 5 + (i % 4) * 1.4f;
            }

            _npcs.Register(data, state);
            i++;
        }
    }

    void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_ui);
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var clockPanel = DarkPanel(new Color("10130fd9"), new Color("71624c"));
        clockPanel.Position = new(22, 18);
        clockPanel.CustomMinimumSize = new(178, 44);
        _ui.AddChild(clockPanel);
        _clock = Ui.Text("", 18);
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        clockPanel.AddChild(_clock);

        var stats = DarkPanel(new Color("0d1110e6"), new Color("8a7150"));
        stats.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        stats.Position = new(22, -116);
        stats.CustomMinimumSize = new(286, 92);
        _ui.AddChild(stats);
        var statRows = new VBoxContainer(); statRows.AddThemeConstantOverride("separation", 4); stats.AddChild(statRows);
        (_healthText, _healthBar) = AddStatRow(statRows, "VIDA", new Color("a93636"));
        (_manaText, _manaBar) = AddStatRow(statRows, "MANA", new Color("315e9b"));

        _quickMenu = DarkPanel(new Color("111511f2"), new Color("8a7150"));
        _quickMenu.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _quickMenu.Position = new(-236, 18);
        _quickMenu.CustomMinimumSize = new(214, 0);
        _quickMenu.Visible = false;
        _ui.AddChild(_quickMenu);
        var actions = new VBoxContainer(); actions.AddThemeConstantOverride("separation", 6); _quickMenu.AddChild(actions);
        actions.AddChild(Ui.Text("AÇÕES · TAB", 16));
        actions.AddChild(Ui.Button("Passar tempo", Advance));
        actions.AddChild(Ui.Button("Pausar relógio", () => { _time.Paused = !_time.Paused; UpdateClock(); }));
        actions.AddChild(Ui.Button("Velocidade", () => { _time.CycleSpeed(); UpdateClock(); }));
        actions.AddChild(Ui.Button("Salvar", Save));
        actions.AddChild(Ui.Button("Carregar", () => GetTree().ReloadCurrentScene()));
        actions.AddChild(Ui.Button("Personagens", OpenCreator));
        actions.AddChild(Ui.Button("Galeria", OpenGallery));
        actions.AddChild(Ui.Button("Baralho", OpenDeck));
        actions.AddChild(Ui.Button("Criar cartas", OpenCards));
        actions.AddChild(Ui.Button("Configurações", OpenSettings));
        actions.AddChild(Ui.Button("Menu principal", () => { Save(); GetTree().ChangeSceneToFile("res://Scenes/MainMenu/MainMenu.tscn"); }));

        _hint = Ui.Text("", 20);
        _ui.AddChild(_hint);
        _hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _hint.OffsetTop = -90;
        _hint.OffsetBottom = -20;
        _hint.OffsetLeft = 330;
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        var reticle = Ui.Text("＋", 20);
        reticle.MouseFilter = Control.MouseFilterEnum.Ignore;
        reticle.SetAnchorsPreset(Control.LayoutPreset.Center);
        reticle.Position = new(-10, -14);
        _ui.AddChild(reticle);
        reticle.Visible = true;
        UpdateHud();
    }

    static PanelContainer DarkPanel(Color background, Color border)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 9, ContentMarginBottom = 9
        });
        return panel;
    }

    static (Label label, ProgressBar bar) AddStatRow(VBoxContainer parent, string name, Color color)
    {
        var label = Ui.Text(name, 14); parent.AddChild(label);
        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 100, ShowPercentage = false, CustomMinimumSize = new(252, 12) };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("050807"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        parent.AddChild(bar);
        return (label, bar);
    }

    public override void _Process(double delta)
    {
        if(IsInCombat)return;
        _player.Locked = _screen != null || _quickMenu.Visible;

        _chunks.UpdateAroundPlayer(_player.Position);
        _chunks.ProcessBuildQueue();

        _npcs.UpdateVisibility(_player.Position);
        _npcs.UpdateBehavior(_game.Period, (float)delta);

        if (_screen != null || _quickMenu.Visible) return;

        _near = _player.AimedNpc();
        _nearPoi = FindNearestPoi();

        var enemy=_encounters?.Nearest();
        _hint.Text = _near != null ? $"[E] Conversar com {_near.Data.Name}" : 
                     enemy != null ? "[E] Enfrentar "+EnemyDefinition.Get(enemy.Definition.EnemyId).Name : 
                     _nearPoi != null ? $"[E] Interagir com {_nearPoi.GetMeta("poi_id").AsString()}" : "";
    }
    
    Node3D? FindNearestPoi()
    {
        Node3D? best = null;
        float bestDist = 4.0f;
        
        foreach (var chunk in _chunks.GetChildren().OfType<ChunkNode>())
        {
            foreach (var child in chunk.GetChildren().OfType<Node3D>())
            {
                if (child.HasMeta("poi_id"))
                {
                    var dist = child.GlobalPosition.DistanceTo(_player.GlobalPosition);
                    if (dist < bestDist) { bestDist = dist; best = child; }
                }
            }
        }
        return best;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if(IsInCombat)return;
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (k.Keycode == Key.Tab && _screen == null)
        {
            _quickMenu.Visible = !_quickMenu.Visible;
            Input.MouseMode = _quickMenu.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled(); return;
        }
        if (k.Keycode == Key.Escape)
        {
            if (_quickMenu.Visible) { _quickMenu.Visible = false; Input.MouseMode = Input.MouseModeEnum.Captured; }
            else Close();
            GetViewport().SetInputAsHandled();
        }
        if(k.Keycode==Key.E&&_screen==null&&!_quickMenu.Visible&&_near==null&&_nearPoi==null&&_encounters.Nearest() is { } enemy){_ = EnterCombat(enemy);GetViewport().SetInputAsHandled();return;}
        if (k.Keycode == Key.E && _screen == null && !_quickMenu.Visible && _near != null)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            var actor = _near;
            actor.PauseForDialogue();
            var dialog = new DialogueController
            {
                Actor = actor,
                Game = _game,
                Closed = () => { actor.ResumeAfterDialogue(); Close(); },
                Changed = () => { UpdateClock(); TryMoment(actor); },
                DuelRequested = (npc) => { _ = StartDuel(npc); }
            };
            _screen = dialog;
            _ui.AddChild(dialog);
            GetViewport().SetInputAsHandled(); return;
        }
        if (k.Keycode == Key.E && _screen == null && !_quickMenu.Visible && _nearPoi != null)
        {
            InteractWithPoi(_nearPoi);
            GetViewport().SetInputAsHandled(); return;
        }
    }
    
    void InteractWithPoi(Node3D poi)
    {
        string poiId = poi.GetMeta("poi_id").AsString();
        string actionName = poiId switch { "camp" => "Descansar na Fogueira", "ruin" => "Coletar Cristal", "cabin" => "Dormir", _ => "Sentar" };
        string description = poiId switch { "camp" => "Recupere um pouco de Vida e Energia aquecendo-se ao fogo.", "ruin" => "Obtenha Mana tocando as ruínas cristalizadas.", "cabin" => "Descanse para restaurar toda sua Vida, Mana e Energia.", _ => "Passe um tempo relaxando." };
        
        var screen = new Control();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _screen = screen;
        _ui.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Ui.Panel(screen, "INTERAÇÃO", out var body, Close);
        
        body.AddChild(Ui.Text(description, 16));
        body.AddChild(Ui.Button(actionName, () => {
            if (poiId == "cabin") {
                _game.Player.Health = _game.Player.MaxHealth;
                _game.Player.Mana = _game.Player.MaxMana;
                _game.Player.Energy = 100;
            } else if (poiId == "camp") {
                _game.Player.Health = Math.Min(_game.Player.MaxHealth, _game.Player.Health + 25);
                _game.Player.Energy = Math.Min(100, _game.Player.Energy + 20);
            } else if (poiId == "ruin") {
                _game.Player.Mana = Math.Min(_game.Player.MaxMana, _game.Player.Mana + 40);
            }
            UpdateClock();
            Close();
            _hint.Text = "Você se sente restaurado.";
        }));
    }

    void Close()
    {
        if (_screen is DialogueController dialogue && dialogue.Actor.IsTalking) dialogue.Actor.ResumeAfterDialogue();
        _screen?.QueueFree(); _screen = null; _player.Locked = false;
    }

    void OpenCreator()
    {
        if (_screen != null) return;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var creator = new CharacterCreatorController { Game = _game };
        creator.Closed = () => { Close(); Save(); GetTree().ReloadCurrentScene(); };
        _screen = creator;
        _ui.AddChild(creator);
    }

    void OpenGallery() { if (_screen == null) { Input.MouseMode = Input.MouseModeEnum.Visible; _screen = AuxiliaryScreens.Gallery(_ui, _game, Close); } }
    void OpenDeck(){if(_screen!=null)return;var editor=new DeckEditor {Game=_game,Closed=Close,Saved=Save};_screen=editor;_ui.AddChild(editor);}
    void OpenCards(){if(_screen!=null)return;var editor=new CardEditor {Closed=Close};_screen=editor;_ui.AddChild(editor);}
    void OpenSettings() { if (_screen == null) { Input.MouseMode = Input.MouseModeEnum.Visible; _screen = AuxiliaryScreens.Settings(_ui, _game.Settings, () => { Save(); Close(); }); } }

    void Save()
    {
        _game.Character = null;
        _game.State = null;
        _game.PlayerX = _player.Position.X;
        _game.PlayerZ = _player.Position.Z;
        _game.PlayerRotationY = _player.CameraYRotation;
        _game.FirstPerson = true;

        _npcs.SyncPositions();

        if (InitialSave == null) _saves.Save(_game);
        _hint.Text = "Progresso salvo · slot 1";
    }

    void Advance()
    {
        if (_screen != null) return;
        _time.SkipHours(6);
        _game.ActionsLeft = 6;
        UpdateClock();
    }

    void OnPeriodChanged(string period)
    {
        _game.ActionsLeft = 6;
        _npcs.AdvancePeriod(period);
        UpdateClock();
    }

    void UpdateClock()
    {
        _clock.Text = $"DIA {_game.Day}  ·  {_time.ClockText}";
        UpdateHud();
    }

    void UpdateHud()
    {
        if (_healthBar == null || _manaBar == null) return;
        var stats = _game.Player;
        stats.Clamp();
        _healthBar.MaxValue = stats.MaxHealth; _healthBar.Value = stats.Health;
        _manaBar.MaxValue = Math.Max(1, stats.MaxMana); _manaBar.Value = stats.Mana;
        _healthText.Text = $"VIDA   {stats.Health}/{stats.MaxHealth}";
        _manaText.Text = $"MANA   {stats.Mana}/{stats.MaxMana}";
    }

    void ResumeCombat()
    {
        if(_game.ActiveCombat==null)return;
        _ = ShowCombat(new CombatManager(_game,_game.ActiveCombat));
    }
    
    public async Task EnterCombat(EnemyActor enemy)
    {
        if(IsInCombat||_screen!=null||_quickMenu.Visible)return;
        try
        {
            _game.PlayerX=_player.Position.X;_game.PlayerZ=_player.Position.Z;_game.PlayerRotationY=_player.CameraYRotation;
            var manager=CombatManager.Start(_game,new CardRepository().Catalog(),enemy.Definition.Id,enemy.Definition.EnemyId,enemy.Definition.Arena,_time.Hour);
            manager.State.ReturnY=_player.Position.Y;
            manager.State.RewardXp=enemy.Definition.Reward;manager.State.Cooldown=enemy.Definition.Cooldown;manager.State.Repeat=enemy.Definition.Repeat;
            await ShowCombat(manager);
        }
        catch(ArgumentException e){_hint.Text=e.Message+" · Tab → Baralho";_game.Encounters[enemy.Definition.Id]=new(){AvailableAt=_encounters.Now+10};}
    }
    
    public async Task StartDuel(NpcActor npc)
    {
        if(IsInCombat||_quickMenu.Visible)return;
        try
        {
            _game.PlayerX=_player.Position.X;_game.PlayerZ=_player.Position.Z;_game.PlayerRotationY=_player.CameraYRotation;
            
            var manager=CombatManager.Start(_game,new CardRepository().Catalog(),"duel_"+npc.Data.Id,"camp","Camp",_time.Hour);
            manager.State.ReturnY=_player.Position.Y;
            manager.State.RewardXp = 15;
            manager.State.Cooldown = 120;
            manager.State.Repeat = true;
            manager.State.IsDuel = true;
            
            await ShowCombat(manager);
        }
        catch(ArgumentException e){_hint.Text=e.Message+" · Tab → Baralho";}
    }
    
    async Task Fade(ColorRect fade,float alpha,double duration)
    {
        var tween=CreateTween();tween.TweenProperty(fade,"color:a",alpha,duration);await ToSignal(tween,Tween.SignalName.Finished);
    }
    void SuspendWorld(bool suspended)
    {
        _encounters.Suspended=suspended;_encounters.ProcessMode=suspended?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        _npcs.ProcessMode=suspended?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        _time.ProcessMode=suspended?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        _player.Locked=suspended;_player.Velocity=Vector3.Zero;_player.ProcessMode=suspended?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        _chunks.Visible=!suspended;_encounters.Visible=!suspended;_ui.Visible=!suspended;
    }
    async Task ShowCombat(CombatManager manager)
    {
        _combatTransition=true;SuspendWorld(true);Input.MouseMode=Input.MouseModeEnum.Visible;
        var layer=new CanvasLayer {Layer=20};AddChild(layer);
        var fade=new ColorRect {Color=new Color(0,0,0,0)};layer.AddChild(fade);fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Fade(fade,1,.4);
        _combat=new CombatArenaController {Manager=manager,Game=_game,Changed=PersistCombat};
        layer.AddChild(_combat);layer.MoveChild(fade,layer.GetChildCount()-1);
        _combat.Finished=()=>_ = LeaveCombat(manager,layer,fade);
        PersistCombat();await Fade(fade,0,.4);fade.MouseFilter=Control.MouseFilterEnum.Ignore;_combatTransition=false;
    }
    void PersistCombat(){if(InitialSave==null)_saves.Save(_game);}
    async Task LeaveCombat(CombatManager manager,CanvasLayer layer,ColorRect fade)
    {
        if(_combatTransition||manager.State.Result.Length==0)return;
        _combatTransition=true;fade.MouseFilter=Control.MouseFilterEnum.Stop;await Fade(fade,1,.35);
        manager.Settle();
        _encounters.RemoveEncounter(manager.State.EncounterId);
        _player.Position=_chunks.ResolveValidPosition(new Vector3(manager.State.ReturnX,0,manager.State.ReturnZ));
        _player.SetInitialView(true,manager.State.ReturnYaw);
        _game.ActiveCombat=null;_combat?.QueueFree();_combat=null;SuspendWorld(false);_player.ActiveCamera.MakeCurrent();UpdateClock();Save();
        await Fade(fade,0,.35);layer.QueueFree();_combatTransition=false;Input.MouseMode=Input.MouseModeEnum.Captured;
    }

    void TryMoment(NpcActor actor)
    {
        var key = actor.Data.Id + ":park_first_date";
        if (!new EventManager().CanUnlockParkDate(actor.State, _game.Period) || _game.UnlockedCinematics.Contains(key)) return;
        _game.UnlockedCinematics.Add(key);
        _game.MomentDetails[key] = $"Dia {_game.Day} · Parque · Uma conversa que vocês vão lembrar.";
        actor.State.Flags.Add("park_first_date");
        actor.State.RelationshipMemories.Add("Primeiro momento juntos no parque.");
        new MemoryManager().Remember(actor.State, "Vocês dividiram uma noite especial no parque.", true);
        new RelationshipSystem().Apply(actor.State, 3, 3, 3, 1);
        Close();

        var screen = new Control();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _screen = screen;
        _ui.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Ui.Panel(screen, "UM INSTANTE NO PARQUE", out var body, Close);
        body.AddChild(new TextureRect
        {
            Texture = _portraits.Get(actor.Data, actor.State, true),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        });
        body.AddChild(Ui.Text(actor.Data.Name + ": \u201cAinda bem que você ficou. Esta noite já valeu a pena.\u201d", 24));
        Save();
    }
}
