using Godot;
using System;
using System.Threading.Tasks;

namespace Heartbeat;

public partial class WorldController : Node
{
    GameSave _game = null!;
    SaveManager _saves = new();
    
    // Core Managers
    NpcManager _npcs = null!;
    WorldTimeSystem _time = null!;

    // UI Roots
    CanvasLayer _uiLayer = null!;
    
    // 2D Systems
    AtlasController _atlas = null!;
    TextureRect _vnBackground = null!;
    TextureRect _vnCharacter = null!;
    
    // Top-Level Screens
    Control? _screen;
    CombatArenaController? _combat;
    string _activeAtlasNodeId = "";

    // Fast Save Access
    public static GameSave? InitialSave;

    public override void _Ready()
    {
        _game = InitialSave ?? _saves.Load() ?? new GameSave();
        
        // Ensure UI layer is top-level
        _uiLayer = new CanvasLayer { Layer = 1 };
        AddChild(_uiLayer);

        // Visual Novel Background Layer
        _vnBackground = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.2f, 0.2f, 0.2f) // Darken background slightly for contrast
        };
        _vnBackground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_vnBackground);

        // Visual Novel Character Sprite
        _vnCharacter = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        _vnCharacter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_vnCharacter);

        // System Initialization
        _time = new WorldTimeSystem { Game = _game };
        _time.PeriodChanged += OnPeriodChanged;
        _npcs = new NpcManager(_game, _time, OnNpcInteracted);
        AddChild(_time);
        AddChild(_npcs);

        // Atlas Map Initialization
        _atlas = new AtlasController { Game = _game, NodeSelected = OnTravel };
        _uiLayer.AddChild(_atlas);

        // HUD and Global Shortcuts can be rebuilt here if needed

        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Restore combat if game was closed during combat
        if (_game.ActiveCombat != null)
        {
            ResumeCombat();
        }
        else _atlas.Visible = true;
    }
    
    void OnPeriodChanged(string period)
    {
        _game.ActionsLeft = 6;
        _npcs.AdvancePeriod(period);
        Save();
    }
    
    Button? _vnBack;

    void ClearVnMeet()
    {
        _vnCharacter.Texture = null;
        _vnCharacter.Material = null;
        if (_vnBack != null)
        {
            _vnBack.QueueFree();
            _vnBack = null;
        }
    }

    void ShowVnMeet(string spritePath, string caption)
    {
        ClearVnMeet();
        _vnCharacter.Texture = ChromaArt.LoadArt(spritePath);
        ChromaArt.ApplyChroma(_vnCharacter);

        _vnBack = Ui.Button(caption + " — Voltar ao atlas", () =>
        {
            FinishAtlasNode();
            ClearVnMeet();
            _vnBackground.Texture = null;
            _atlas.Visible = true;
        });
        _vnBack.Position = new Vector2(40, 620);
        _vnBack.CustomMinimumSize = new Vector2(420, 44);
        _uiLayer.AddChild(_vnBack);
    }

    void ShowScenicStop()
    {
        ClearVnMeet();
        _vnBack = Ui.Button("Voltar ao atlas", () =>
        {
            FinishAtlasNode();
            ClearVnMeet();
            _vnBackground.Texture = null;
            _atlas.Visible = true;
        });
        _vnBack.Position = new Vector2(40, 620);
        _vnBack.CustomMinimumSize = new Vector2(280, 44);
        _uiLayer.AddChild(_vnBack);
    }

    void OnTravel(string destinationId)
    {
        var node = _game.AtlasNodes.Find(n => n.Id == destinationId);
        if (node == null || node.Status == AtlasNodeStatus.Locked) return;
        _activeAtlasNodeId = node.Id;
        _atlas.Visible = false;
        ClearVnMeet();

        _vnBackground.Modulate = new Color(0.85f, 0.85f, 0.88f);
        _vnBackground.Texture = ChromaArt.LoadArt(ChromaArt.BackgroundForDestination(node.BackgroundId));

        if (node.Kind == AtlasNodeKind.Merchant)
        {
            var merchant = _npcs.GetNpc("merchant");
            if (merchant != null) OnNpcInteracted(merchant);
            else ShowVnMeet(ChromaArt.MerchantSprite, "Mercador");
            return;
        }

        if (destinationId == "tavern")
        {
            ShowVnMeet(ChromaArt.BarmaidSprite, "Donzela da taverna");
            return;
        }

        if (destinationId == "knight")
        {
            ShowVnMeet(ChromaArt.KnightSprite, "Cavaleiro");
            return;
        }

        if (destinationId is "forest_dark" or "forest" or "camp")
        {
            if (new Random().NextDouble() < 0.5)
            {
                var rng = new Random();
                var npcList = new System.Collections.Generic.List<string>(_game.CharacterStates.Keys);
                if (npcList.Count > 0)
                {
                    var npc = _npcs.GetNpc(npcList[rng.Next(npcList.Count)]);
                    if (npc != null)
                    {
                        OnNpcInteracted(npc);
                        return;
                    }
                }
            }

            var foeId = destinationId == "camp" ? "camp" : "forest";
            _ = EnterCombat(EnemyDefinition.Get(foeId));
            return;
        }

        ShowScenicStop();
    }
    void OnNpcInteracted(NpcActor actor)
    {
        if (actor.Data.Id == "merchant")
        {
            _vnCharacter.Texture = ChromaArt.LoadArt(ChromaArt.MerchantSprite);
            ChromaArt.ApplyChroma(_vnCharacter);
        }
        else
        {
            _vnCharacter.Material = null;
            _vnCharacter.Texture = new PortraitCache().Get(actor.Data, actor.State, false);
        }

        DialogueController? dialog = null;
        dialog = new DialogueController
        {
            Actor = actor,
            Game = _game,
            DuelRequested = StartDuel,
            Closed = () =>
            {
                FinishAtlasNode();
                dialog?.QueueFree();
                _screen = null;
                _atlas.Visible = true;
                _vnCharacter.Texture = null;
                _vnCharacter.Material = null;
            }
        };
        _screen = dialog;
        _uiLayer.AddChild(dialog);
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (k.Keycode == Key.Escape && _screen != null)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (k.Keycode == Key.M && _screen == null) // Toggle Map
        {
            _atlas.Visible = !_atlas.Visible;
        }
    }

    void Close()
    {
        _screen?.QueueFree();
        _screen = null;
    }

    void FinishAtlasNode()
    {
        if (string.IsNullOrEmpty(_activeAtlasNodeId)) return;
        AtlasGenerator.Complete(_game, _activeAtlasNodeId);
        _game.RecentEvents.Add(_activeAtlasNodeId);
        if (_game.RecentEvents.Count > 16) _game.RecentEvents.RemoveAt(0);
        _activeAtlasNodeId = "";
        Save();
    }

    void Save()
    {
        _game.FirstPerson = false;
        if (InitialSave == null) _saves.Save(_game);
    }

    void ResumeCombat()
    {
        if(_game.ActiveCombat==null)return;
        _ = ShowCombat(new CombatManager(_game,_game.ActiveCombat));
    }
    
    public async Task EnterCombat(EnemyDefinition enemy)
    {
        if(_screen!=null)return;
        var arena = enemy.Id is "forest" or "night" ? "forest" : "camp";
        var manager=CombatManager.Start(_game,new CardRepository().Catalog(), "wild_encounter", enemy.Id, arena, _time.Hour);
        manager.State.RewardXp=enemy.RewardXp;
        manager.State.Cooldown=0;
        manager.State.Repeat=true;
        await ShowCombat(manager);
    }
    void StartDuel(NpcActor actor)
    {
        if (_screen != null) _screen.QueueFree();
        _screen = null;
        var manager = CombatManager.Start(_game, new CardRepository().Catalog(), "duel_" + actor.Data.Id, "camp", "tavern", _time.Hour);
        manager.State.IsDuel = true;
        manager.State.OpponentCharacterId = actor.Data.Id;
        _ = ShowCombat(manager);
    }
    
    async Task Fade(ColorRect fade,float alpha,double duration)
    {
        var tween=CreateTween();tween.TweenProperty(fade,"color:a",alpha,duration);await ToSignal(tween,Tween.SignalName.Finished);
    }
    
    async Task ShowCombat(CombatManager manager)
    {
        var fade=new ColorRect {Color=new Color(0,0,0,0)};_uiLayer.AddChild(fade);fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Fade(fade,1,.4);
        _combat=new CombatArenaController {Manager=manager,Game=_game,Changed=PersistCombat};
        _uiLayer.AddChild(_combat);_uiLayer.MoveChild(fade,_uiLayer.GetChildCount()-1);
        _combat.Finished=()=>_ = LeaveCombat(manager,fade);
        PersistCombat();await Fade(fade,0,.4);fade.MouseFilter=Control.MouseFilterEnum.Ignore;
    }
    void PersistCombat(){if(InitialSave==null)_saves.Save(_game);}
    async Task LeaveCombat(CombatManager manager,ColorRect fade)
    {
        if(manager.State.Result.Length==0)return;
        fade.MouseFilter=Control.MouseFilterEnum.Stop;await Fade(fade,1,.35);
        manager.Settle();
        if (manager.State.Result == "Victory") FinishAtlasNode();
        _game.ActiveCombat=null;_combat?.QueueFree();_combat=null;Save();
        _atlas.Visible = true; // Return to map
        await Fade(fade,0,.35);fade.QueueFree();
    }
}
