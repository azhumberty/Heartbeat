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
    CancellationTokenSource? _eventCancel;
    string _activeAtlasNodeId = "";
    bool _completeOnClose = true;
    DateTime _sessionStart = DateTime.UtcNow;
    CampController? _camp;

    // Fast Save Access
    public static GameSave? InitialSave;

    public override void _Ready()
    {
        var initial=InitialSave;InitialSave=null;_game = initial ?? _saves.Load() ?? new GameSave();_saves.Migrate(_game);
        var opts=_saves.LoadSettings();
        _game.Settings.OpenRouterApiKey=opts.OpenRouterApiKey;
        _game.Settings.OpenRouterModel=opts.OpenRouterModel;
        _game.Settings.UseOpenRouter=opts.UseOpenRouter;
        _game.Settings.UseOnlineAi=opts.UseOnlineAi;
        
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
        _atlas = new AtlasController { Game = _game, NodeSelected = OnTravel, CampRequested = OpenCamp };
        _uiLayer.AddChild(_atlas);

        // HUD and Global Shortcuts can be rebuilt here if needed

        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Restore combat if game was closed during combat
        if (_game.ActiveCombat != null)
        {
            ResumeCombat();
        }
        else _atlas.Visible = true;
        _ = PrefetchWorldImages();
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
        _vnCharacter.Scale = Vector2.One;
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
        if (node == null || (node.Status == AtlasNodeStatus.Locked && !node.Persistent)) return;
        if(node.Persistent){OpenCamp();return;}
        _activeAtlasNodeId = node.Id;
        _completeOnClose = true;
        _atlas.Visible = false;
        ClearVnMeet();

        _vnBackground.Modulate = new Color(0.85f, 0.85f, 0.88f);
        _vnBackground.Texture = node.BackgroundId.Contains("://") || Path.IsPathRooted(node.BackgroundId) ? ContentLibrary.LoadTexture(node.BackgroundId) : ChromaArt.LoadArt(ChromaArt.BackgroundForDestination(node.BackgroundId));

        if (node.Kind == AtlasNodeKind.Merchant)
        {
            var merchant = _npcs.GetNpc(string.IsNullOrWhiteSpace(node.ContentId)?"merchant":node.ContentId)??_npcs.GetNpc("silas")??_npcs.GetNpc("merchant");
            if (merchant != null) OnNpcInteracted(merchant);
            else ShowVnMeet(ChromaArt.MerchantSprite, "Mercador");
            return;
        }

        if (node.Kind==AtlasNodeKind.Character&&_npcs.GetNpc(node.ContentId) is { } character)
        {
            OnNpcInteracted(character);
            return;
        }

        var template=string.IsNullOrWhiteSpace(node.TemplateId)?AtlasGenerator.TemplateFromId(node.Id):node.TemplateId;
        if (template == "knight")
        {
            ShowVnMeet(ChromaArt.KnightSprite, "Cavaleiro");
            return;
        }

        if (node.Kind is AtlasNodeKind.Combat or AtlasNodeKind.Boss)
        {
            var foeId = string.IsNullOrWhiteSpace(node.ContentId) ? (node.Kind==AtlasNodeKind.Boss?"ruin":"forest") : node.ContentId;
            var arena=node.BackgroundId.Contains("cave",StringComparison.OrdinalIgnoreCase)?"cave_chamber":node.Kind==AtlasNodeKind.Boss?"ruin":"forest";
            var role = node.Kind==AtlasNodeKind.Boss ? EnemyRole.Boss
                : (node.Risk>=4 || node.Title.StartsWith("Elite", StringComparison.OrdinalIgnoreCase)) ? EnemyRole.Elite
                : EnemyRole.Normal;
            _ = EnterCombat(EnemyDefinition.Get(foeId, _game),arena,role);
            return;
        }

        if(template=="tavern")
        {
            _vnCharacter.Texture=ChromaArt.LoadArt(ChromaArt.BarmaidSprite);
            ChromaArt.ApplyChroma(_vnCharacter);
        }
        OpenProceduralEvent(node);
    }

    void OpenCamp()
    {
        _atlas.Visible=false;_activeAtlasNodeId="";_completeOnClose=false;ClearVnMeet();
        _vnBackground.Modulate=new Color(.92f,.9f,.86f);_vnBackground.Texture=ChromaArt.LoadArt("Backgrounds/camp.png");ShowCampPanel();
    }
    void ShowCampPanel()
    {
        if(GodotObject.IsInstanceValid(_camp)){_camp!.Visible=true;_camp.Refresh();_screen=_camp;return;}
        _camp=new CampController {Game=_game,Closed=LeaveCamp,TalkRequested=OpenCampTalk,Rested=Save};_screen=_camp;_uiLayer.AddChild(_camp);
    }
    void LeaveCamp()
    {
        if(GodotObject.IsInstanceValid(_camp))_camp!.QueueFree();_camp=null;_screen=null;_completeOnClose=true;ClearVnMeet();_vnBackground.Texture=null;_atlas.Visible=true;_atlas.RefreshProgress();Save();
    }
    void OpenCampTalk(string id)
    {
        var actor=_npcs.GetNpc(id);if(actor==null)return;if(GodotObject.IsInstanceValid(_camp))_camp!.Visible=false;_screen=null;_completeOnClose=false;OnNpcInteracted(actor);
    }

    async void OpenProceduralEvent(AtlasNodeData node)
    {
        var loading=new Control();loading.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);var label=Ui.Text("O destino está tomando forma…",22);label.Position=new Vector2(70,610);loading.AddChild(label);_screen=loading;_uiLayer.AddChild(loading);
        var context=new EventContext {Game=_game,Node=node,Assets=new ContentLibrary().Load()};
        var cts=new CancellationTokenSource();_eventCancel=cts;
        EventResult story;
        try
        {
            IEventProvider provider = _game.Settings.UseOpenRouter && OpenRouterClient.HasKey(_game.Settings)
                ? new OpenRouterEventProvider()
                : (_game.Settings.UseOnlineAi ? new GroqEventProvider() : new OfflineEventProvider());
            // Run event generation and image generation concurrently
            var eventTask = provider.CreateAsync(context,_game.Settings,cts.Token);
            var fallbackPrompt = BackgroundGenerationService.PromptFor(_game, node);
            story = await eventTask;
            
            if (_game.Settings.UseImageAi)
            {
                var imgPrompt = !string.IsNullOrWhiteSpace(story.ImagePrompt) ? story.ImagePrompt : fallbackPrompt;
                _ = ApplyGeneratedBackground(node, imgPrompt, cts.Token);
            }
        }
        catch(OperationCanceledException){return;}
        finally{if(ReferenceEquals(_eventCancel,cts))_eventCancel=null;cts.Dispose();}
        if(!GodotObject.IsInstanceValid(loading)||!loading.IsInsideTree())return;
        loading.QueueFree();
        EventController? view=null;
        view=new EventController
        {
            Game=_game,Story=story,
            Completed=()=>
            {
                FinishAtlasNode();view?.QueueFree();_screen=null;ClearVnMeet();_vnBackground.Texture=null;_atlas.Visible=true;
            }
        };
        _screen=view;_uiLayer.AddChild(view);
    }

    async Task ApplyGeneratedBackground(AtlasNodeData node, string? eventPrompt, CancellationToken ct)
    {
        try
        {
            var tex = await BackgroundGenerationService.FetchAsync(_game, node, eventPrompt, ct);
            if (tex != null && IsInsideTree() && GodotObject.IsInstanceValid(_vnBackground))
            {
                _vnBackground.Texture = tex;
                _vnBackground.Modulate = new Color(0.92f, 0.92f, 0.94f);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { GD.PushWarning("[ImageAI] fundo: " + ex.Message); }
    }

    async Task PrefetchWorldImages()
    {
        if (!_game.Settings.UseImageAi) return;
        try
        {
            foreach (var person in _game.GeneratedCast.Take(3))
                await PortraitGenerationService.EnsureAsync(person, _game, CancellationToken.None);
        }
        catch (Exception ex) { GD.PushWarning("[ImageAI] prefetch: " + ex.Message); }
    }

    async Task ApplyPortrait(NpcActor actor)
    {
        if (!_game.Settings.UseImageAi || !actor.Data.Tags.Contains("generated")) return;
        try
        {
            var tex = await PortraitGenerationService.EnsureAsync(actor.Data, _game, CancellationToken.None);
            if (tex != null && IsInsideTree() && GodotObject.IsInstanceValid(_vnCharacter))
            {
                _vnCharacter.Material = null;
                _vnCharacter.Texture = tex;
            }
        }
        catch (Exception ex) { GD.PushWarning("[ImageAI] retrato: " + ex.Message); }
    }

    async Task PrefetchSpecials(CharacterData person)
    {
        if (!_game.Settings.UseImageAi) return;
        foreach (var beat in SocialBeatService.BeatsFor(person))
        {
            if (!_game.UnlockedCinematics.Contains(SocialBeatService.Key(person.Id, beat.Id))) continue;
            try { await PortraitGenerationService.SpecialAsync(person, _game, beat.Id, beat.Text, CancellationToken.None); }
            catch { }
        }
    }
    void OnNpcInteracted(NpcActor actor)
    {
        if (actor.Data.Id == "merchant" || actor.Data.Tags.Contains("merchant"))
        {
            _vnCharacter.Texture = ChromaArt.LoadArt(ChromaArt.MerchantSprite);
            ChromaArt.ApplyChroma(_vnCharacter);
        }
        else
        {
            _vnCharacter.Material = null;
            _vnCharacter.Texture = new PortraitCache().Get(actor.Data, actor.State, false);
            if(actor.Data.MainImagePath.Contains("chroma",StringComparison.OrdinalIgnoreCase))ChromaArt.ApplyChroma(_vnCharacter);
        }
        PortraitMotion.Breath(_vnCharacter);
        _ = ApplyPortrait(actor);
        _ = PrefetchSpecials(actor.Data);

        DialogueController? dialog = null;
        dialog = new DialogueController
        {
            Actor = actor,
            Game = _game,
            DuelRequested = StartDuel,
            Changed = Save,
            Closed = () => LeaveDialogue(dialog)
        };
        _screen = dialog;
        _uiLayer.AddChild(dialog);
    }

    void LeaveDialogue(DialogueController? dialog)
    {
        if (dialog != null && GodotObject.IsInstanceValid(dialog) && dialog.ShopOpen) return;
        var complete = _completeOnClose;
        if (dialog != null && GodotObject.IsInstanceValid(dialog) && !dialog.IsQueuedForDeletion())
            dialog.QueueFree();
        if (ReferenceEquals(_screen, dialog)) _screen = null;
        _vnCharacter.Texture = null;
        _vnCharacter.Material = null;
        _vnCharacter.Scale = Vector2.One;
        if (complete)
        {
            FinishAtlasNode();
            _vnBackground.Texture = null;
            _atlas.Visible = true;
        }
        else ShowCampPanel();
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
        if (_screen is CampController) { LeaveCamp(); return; }
        if (_screen is DialogueController dialog && GodotObject.IsInstanceValid(dialog))
        {
            if (dialog.ShopOpen) return;
            LeaveDialogue(dialog);
            return;
        }
        var complete = _completeOnClose && !string.IsNullOrEmpty(_activeAtlasNodeId);
        _eventCancel?.Cancel();
        if (GodotObject.IsInstanceValid(_screen)) _screen!.QueueFree();
        _screen = null;
        ClearVnMeet();
        _vnBackground.Texture = null;
        if (complete) FinishAtlasNode();
        else _activeAtlasNodeId = "";
        _atlas.Visible = true;
        _atlas.RefreshProgress();
    }

    void FinishAtlasNode()
    {
        if (string.IsNullOrEmpty(_activeAtlasNodeId)) return;
        var finished=_game.AtlasNodes.FirstOrDefault(node=>node.Id==_activeAtlasNodeId);
        AtlasGenerator.Complete(_game, _activeAtlasNodeId);
        _game.RecentEvents.Add(_activeAtlasNodeId);
        if (_game.RecentEvents.Count > 16) _game.RecentEvents.RemoveAt(0);
        _activeAtlasNodeId = "";
        if(finished?.Kind==AtlasNodeKind.Boss){AtlasGenerator.BeginNextExpedition(_game);RebuildAtlas();}
        else _atlas.RefreshProgress();
        Save();
    }

    void RebuildAtlas()
    {
        var previous=_atlas;_atlas=new AtlasController{Game=_game,NodeSelected=OnTravel,CampRequested=OpenCamp};_uiLayer.AddChild(_atlas);previous.QueueFree();
    }

    void Save()
    {
        _game.FirstPerson = false;
        var now = DateTime.UtcNow;
        _game.LastPlayedAt = now.ToString("o");
        _game.PlayedSeconds += Math.Max(0, (int)(now - _sessionStart).TotalSeconds);
        _sessionStart = now;
        _saves.Save(_game);
    }

    void ResumeCombat()
    {
        if(_game.ActiveCombat==null)return;
        _ = ShowCombat(new CombatManager(_game,_game.ActiveCombat));
    }
    
    public async Task EnterCombat(EnemyDefinition enemy,string? arenaHint=null, EnemyRole role=EnemyRole.Normal)
    {
        if(_screen!=null)return;
        var arena = arenaHint??(enemy.Id is "forest" or "night" or "minotaur" ? "forest" : "camp");
        var encounterId="atlas_"+(string.IsNullOrWhiteSpace(_activeAtlasNodeId)?enemy.Id:_activeAtlasNodeId);var manager=CombatManager.Start(_game,new CardRepository().Catalog(), encounterId, enemy.Id, arena, _time.Hour, role);
        var scale=Math.Min(25,_game.ExpeditionIndex);manager.State.Enemy.MaxHealth+=scale*10;manager.State.Enemy.Health=manager.State.Enemy.MaxHealth;manager.State.RewardXp=enemy.RewardXp+scale*3;
        manager.State.Cooldown=0;
        manager.State.Repeat=true;
        await ShowCombat(manager);
    }
    void StartDuel(NpcActor actor)
    {
        if (_screen is DialogueController dialog && GodotObject.IsInstanceValid(dialog) && dialog.ShopOpen) return;
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
    void PersistCombat()=>_saves.Save(_game);
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
