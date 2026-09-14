using Godot;
namespace Heartbeat;

public partial class DialogueController : Control
{
    public GameSave Game { get; set; }=new(); public NpcActor Actor { get; set; }=null!; public Action? Closed; public Action? Changed; public Action<NpcActor>? DuelRequested;
    const int MaxPlayerTurns = 8;
    Label _line=null!,_status=null!; TextEdit _input=null!;Button? _invite; bool _busy; int _turns; readonly CancellationTokenSource _cancel=new();
    CardShopController? _shop;
    bool _parkAsk;
    bool _exiting;
    
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        var panel = new PanelContainer(); AddChild(panel);
        panel.SetAnchorsPreset(LayoutPreset.BottomWide);
        panel.OffsetTop = -248; panel.OffsetBottom = -20;
        panel.OffsetLeft = 100; panel.OffsetRight = -100;
        
        var style = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.75f), ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
        panel.AddThemeStyleboxOverride("panel", style);
        
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 10); panel.AddChild(content);
        
        var header = new HBoxContainer(); content.AddChild(header);
        var label = Ui.Text(Actor.Data.Name, 26); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; label.AddThemeColorOverride("font_color", new Color("f2d388")); header.AddChild(label);
        
        var closeBtn = Ui.Button("Fechar - Esc", RequestClose);
        var shortcut = new Shortcut(); shortcut.Events.Add(new InputEventKey { Keycode = Key.Escape }); closeBtn.Shortcut = shortcut;
        header.AddChild(closeBtn);
        
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(text);
        
        _line = Ui.Body($"{Actor.Data.Name} olha para você. O que você quer dizer?", 22); _line.SizeFlagsVertical = SizeFlags.ExpandFill; text.AddChild(_line);
        _status = Ui.Body("", 14); _status.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f)); text.AddChild(_status);
        
        var controls = new HBoxContainer(); text.AddChild(controls);
        _input = new TextEdit { PlaceholderText = "Digite aqui... Enter envia", CustomMinimumSize = new Vector2(0, 45), SizeFlagsHorizontal = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; controls.AddChild(_input);
        _input.GuiInput += e => { if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Enter && !k.ShiftPressed) { _input.AcceptEvent(); _=Send(); } };
        controls.AddChild(Ui.Button("Enviar", () => _=Send()));
        
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; text.AddChild(actions);
        if (Actor.Data.Id == "merchant" || Actor.Data.Tags.Contains("merchant"))
        {
            actions.AddChild(Ui.Button("Comprar Cartas", OpenShop));
        }
        else if (Actor.Data.CanBuildRelationship)
        {
            actions.AddChild(Ui.Button("Parque", () => { if(!Alive())return; _parkAsk=true; _input.Text = "Quer passar um tempo comigo no parque?"; _=Send(); }));
            actions.AddChild(Ui.Button("Duelo", () => { DuelRequested?.Invoke(Actor); RequestClose(); }));
            _invite=Ui.Button("Convidar para o acampamento",()=>
            {
                if(!Alive())return;
                if(CampService.Invite(Game,Actor.Data,Actor.State)){_line.Text=$"{Actor.Data.Name} aceita dividir o acampamento com você.";_status.Text="Novo morador · progresso salvo";RefreshInvite();Changed?.Invoke();}
            });RefreshInvite();actions.AddChild(_invite);
        }
        actions.AddChild(Ui.Button("Perfil", ShowProfile));
        if (Actor.Data.CanBuildRelationship)
            actions.AddChild(Ui.Button("Forçar Visita (Debug)", ForceVisit));
        
        panel.Modulate = new Color(1,1,1,0); panel.CreateTween().TweenProperty(panel, "modulate:a", 1f, .2);
        _input.GrabFocus();
    }

    public bool ShopOpen => GodotObject.IsInstanceValid(_shop) && _shop!.IsInsideTree();

    void OpenShop()
    {
        if (!Alive() || ShopOpen) return;
        var parent = GetParent();
        if (parent == null) return;
        Visible = false;
        var shop = new CardShopController { Game = Game, MerchantId = Actor.Data.Id };
        _shop = shop;
        shop.Closed = OnShopClosed;
        parent.AddChild(shop);
    }

    void OnShopClosed()
    {
        _shop = null;
        if (!Alive()) return;
        Visible = true;
        _input.GrabFocus();
    }

    void RequestClose()
    {
        if (ShopOpen)
        {
            _shop?.CloseShop();
            return;
        }
        Closed?.Invoke();
    }

    bool Alive() => !_exiting && GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion();
    
    async Task Send()
    {
        if(_busy || !Alive() || string.IsNullOrWhiteSpace(_input.Text))return;
        if(_turns >= MaxPlayerTurns) { _status.Text = "Esta conversa chegou a uma pausa natural. Feche para voltar ao atlas."; return; }
        var advancesRelationship = Actor.Data.CanBuildRelationship && Game.ActionsLeft > 0;
        var input=_input.Text.Trim(); input=input[..Math.Min(input.Length,1000)]; _input.Text=""; _busy=true; _status.Text=Actor.Data.Name+" está pensando...";
        try
        {
            var s=Actor.State; s.TimeContext=$"Dia {Game.Day}, {Game.Period}";
            var lore=Game.WorldLore??new WorldLore();
            var prompt=string.IsNullOrWhiteSpace(Game.WorldPrompt)?lore.Premise:Game.WorldPrompt;
            if(prompt.Length>420)prompt=prompt[..420].Trim()+"...";
            var worldCtx=$"Mundo {lore.RegionName}. Pedido do jogador: {prompt}. Ameaca: {lore.Threat}. Tom: {lore.Atmosphere}. Local {s.CurrentLocation}; {s.TimeContext}; atividade {s.CurrentActivity}";
            var memory=new SocialMemoryService();
            memory.PrepareTurn(Actor.Data,s,worldCtx,Game.Day,Game.WorldMinutes);
            IDialogueProvider provider = Game.Settings.UseOpenRouter && OpenRouterClient.HasKey(Game.Settings)
                ? new OpenRouterDialogueProvider()
                : (Game.Settings.UseOnlineAi ? new GroqDialogueProvider() : new ProceduralDialogueProvider());
            var r=DialogueValidator.Sanitize(await provider.ReplyAsync(Actor.Data,s,input,Game.Settings,_cancel.Token));
            if(!Alive())return;
            _turns++;
            if (advancesRelationship) Game.ActionsLeft--;
            else { r.AffectionDelta = r.TrustDelta = r.RomanceDelta = r.AttractionDelta = r.EnergyDelta = r.StressDelta = 0; r.ProviderStatus += " · conversa livre"; }
            var normalized=input.ToLowerInvariant(); var repeated=s.RecentInputs.Contains(normalized);
            if(repeated) { r.AffectionDelta=Math.Min(0,r.AffectionDelta); r.TrustDelta=Math.Min(0,r.TrustDelta); }
            s.RecentInputs.Add(normalized); if(s.RecentInputs.Count>12)s.RecentInputs.RemoveAt(0);
            if (Actor.Data.CanBuildRelationship)
            {
                new RelationshipSystem().Apply(s,r.AffectionDelta,r.TrustDelta,r.RomanceDelta,r.AttractionDelta,Game.Day);
                s.Energy+=r.EnergyDelta; s.Stress+=r.StressDelta; s.Mood+=r.AffectionDelta; s.CurrentDesire=r.Desire;
                CampService.MaybeJoinByAffection(Game, Actor.Data, s);
                RefreshInvite();
            }
            s.CurrentEmotion=r.Emotion; s.Clamp();
            memory.RecordConfirmedPlayerAction(Actor.Data,s,input,Game.Day,Game.WorldMinutes);
            s.Conversation.Add("Jogador: "+input); s.Conversation.Add(Actor.Data.Name+": "+r.Dialogue); while(s.Conversation.Count>8)s.Conversation.RemoveAt(0);
            var intent=_parkAsk?"park":""; _parkAsk=false;
            var beats=SocialBeatService.TryUnlock(Game,Actor.Data,s,intent);
            DeckManager.SyncUnlocks(Game, new CardRepository().Catalog());
            _line.Text=r.Dialogue;
            _status.Text=beats.Count>0 ? StatusLine(r.ProviderStatus)+" · Momento: "+beats[0] : StatusLine(r.ProviderStatus);
            Changed?.Invoke();
            _ = NpcVoice.Speak(this, Actor.Data, r.Dialogue, Game.Settings, _cancel.Token);
        }
        catch(OperationCanceledException) { }
        catch(Exception e) { if(Alive()) { var alternative = new ProceduralDialogueProvider().Reply(Actor.Data, Actor.State, input); _line.Text = alternative.Dialogue; Actor.State.CurrentEmotion = alternative.Emotion; _status.Text=$"Falha técnica ({e.GetType().Name})"; _ = NpcVoice.Speak(this, Actor.Data, alternative.Dialogue, Game.Settings, _cancel.Token); } }
        finally { if(Alive()) _busy=false; }
    }
    string StatusLine(string provider)
    {
        var s=Actor.State;
        var role=Actor.Data.CanBuildRelationship ? s.Relationship : (Actor.Data.Profession.Length>0?Actor.Data.Profession:"conhecido");
        return $"{provider}  ·  {Actor.Data.Name} · {role} · humor {s.CurrentMood} · afeto {s.Affection} conf {s.Trust}";
    }

    void ForceVisit()
    {
        if (!Alive() || !Actor.Data.CanBuildRelationship) return;
        if (CampService.Invite(Game, Actor.Data, Actor.State))
        {
            _line.Text = Actor.Data.Name + " foi forçado para o acampamento (debug).";
            _status.Text = "Morador de teste · progresso salvo";
            RefreshInvite();
            Changed?.Invoke();
        }
        else _status.Text = Actor.Data.Name + " já está no acampamento.";
    }

    void ShowProfile()
    {
        if(!Alive())return;
        var d=Actor.Data; var s=Actor.State;
        SocialModelMigrator.Migrate(d); SocialModelMigrator.Migrate(s);
        var mem=s.Memories.Where(m=>m.Confirmed).TakeLast(2).Select(m=>m.Content);
        var summary=string.IsNullOrWhiteSpace(s.MemorySummary)?"":"\nLembra: "+s.MemorySummary[..Math.Min(160,s.MemorySummary.Length)];
        _line.Text=$"{d.Name}, {d.Age} anos · {d.Profession}\n{d.Description}\n{d.Personality}\nAfeto {s.Affection}  Confianca {s.Trust}  Romance {s.Romance}  Atracao {s.Attraction}  Respeito {s.Emotions.Respect}\nRelacao: {s.Relationship}  Humor: {s.CurrentMood}{summary}\n"+(mem.Any()?string.Join("\n",mem):"Ainda sem memorias confirmadas.");
    }

    void RefreshInvite()
    {
        if(_invite==null||!Alive())return;var resident=Game.CampResidents.Contains(Actor.Data.Id);_invite.Text=resident?"Mora no acampamento":"Convidar para o acampamento";_invite.Disabled=resident||!CampService.CanInvite(Game,Actor.Data,Actor.State);_invite.TooltipText=resident?"Morador permanente do acampamento.":"Podes chamar agora. Não precisa de afeto.";
    }
    public override void _ExitTree()
    {
        _exiting = true;
        NpcVoice.Stop(Game.Settings);
        if (GodotObject.IsInstanceValid(_shop) && !_shop!.IsQueuedForDeletion())
            _shop.QueueFree();
        _shop = null;
        try { _cancel.Cancel(); } catch (ObjectDisposedException) { }
    }
}
