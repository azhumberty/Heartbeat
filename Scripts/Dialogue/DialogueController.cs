using Godot;
namespace Heartbeat;

public partial class DialogueController : Control
{
    public GameSave Game { get; set; }=new(); public NpcActor Actor { get; set; }=null!; public Action? Closed; public Action? Changed; public Action<NpcActor>? DuelRequested;
    const int MaxPlayerTurns = 8;
    Label _line=null!,_status=null!; TextEdit _input=null!; bool _busy; int _turns; readonly CancellationTokenSource _cancel=new();
    
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        
        // Minimalist Bottom Panel
        var panel = new PanelContainer(); AddChild(panel);
        panel.SetAnchorsPreset(LayoutPreset.BottomWide);
        panel.OffsetTop = -280; panel.OffsetBottom = -20;
        panel.OffsetLeft = 100; panel.OffsetRight = -100;
        
        var style = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.75f), ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 18, ContentMarginBottom = 18, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
        panel.AddThemeStyleboxOverride("panel", style);
        
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 10); panel.AddChild(content);
        
        var header = new HBoxContainer(); content.AddChild(header);
        var label = Ui.Text(Actor.Data.Name, 26); label.AddThemeColorOverride("font_color", new Color("f2d388")); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(label);
        
        var closeBtn = Ui.Button("Fechar · Esc", () => Closed?.Invoke());
        var shortcut = new Shortcut(); shortcut.Events.Add(new InputEventKey { Keycode = Key.Escape }); closeBtn.Shortcut = shortcut;
        header.AddChild(closeBtn);
        
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(text);
        
        _line = Ui.Text($"{Actor.Data.Name} olha para você. O que você quer dizer?", 22); _line.SizeFlagsVertical = SizeFlags.ExpandFill; _line.AutowrapMode = TextServer.AutowrapMode.Word; text.AddChild(_line);
        _status = Ui.Text("", 14); _status.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f)); text.AddChild(_status);
        
        var controls = new HBoxContainer(); text.AddChild(controls);
        _input = new TextEdit { PlaceholderText = "Digite aqui... Enter envia", CustomMinimumSize = new Vector2(0, 45), SizeFlagsHorizontal = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary }; controls.AddChild(_input);
        _input.GuiInput += e => { if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Enter && !k.ShiftPressed) { _input.AcceptEvent(); _=Send(); } };
        controls.AddChild(Ui.Button("Enviar", () => _=Send()));
        
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; text.AddChild(actions);
        if (Actor.Data.Id == "merchant" || Actor.Data.Tags.Contains("merchant"))
        {
            actions.AddChild(Ui.Button("💰 Comprar Cartas", () => {
                var shop = new CardShopController { Game = Game };
                var onClosed = Closed;
                shop.Closed = () => { onClosed?.Invoke(); shop.QueueFree(); };
                GetParent().AddChild(shop); QueueFree();
            }));
        }
        else if (Actor.Data.CanBuildRelationship)
        {
            actions.AddChild(Ui.Button("Parque", () => { _input.Text = "Quer passar um tempo comigo no parque?"; _=Send(); }));
            actions.AddChild(Ui.Button("⚔️ Duelo", () => { DuelRequested?.Invoke(Actor); Closed?.Invoke(); }));
        }
        actions.AddChild(Ui.Button("Perfil", () => _line.Text = Actor.Data.CanBuildRelationship ? $"{Actor.Data.Description}\nAfeto {Actor.State.Affection} · Confiança {Actor.State.Trust}" : Actor.Data.Description));
        
        panel.Modulate = new Color(1,1,1,0); panel.CreateTween().TweenProperty(panel, "modulate:a", 1f, .2);
        _input.GrabFocus();
    }
    
    async Task Send()
    {
        if(_busy || string.IsNullOrWhiteSpace(_input.Text))return;
        if(_turns >= MaxPlayerTurns) { _status.Text = "Esta conversa chegou a uma pausa natural. Feche para voltar ao atlas."; return; }
        var advancesRelationship = Actor.Data.CanBuildRelationship && Game.ActionsLeft > 0;
        var input=_input.Text.Trim(); input=input[..Math.Min(input.Length,1000)]; _input.Text=""; _busy=true; _status.Text=Actor.Data.Name+" está pensando...";
        try
        {
            var s=Actor.State; s.TimeContext=$"Dia {Game.Day}, {Game.Period}";
            var memory=new SocialMemoryService();
            memory.PrepareTurn(Actor.Data,s,$"Floresta medieval; local {s.CurrentLocation}; {s.TimeContext}; atividade {s.CurrentActivity}; desejo {s.CurrentDesire}",Game.Day,Game.WorldMinutes);
            IDialogueProvider provider=Game.Settings.UseOnlineAi?new GroqDialogueProvider():new ProceduralDialogueProvider();
            var r=DialogueValidator.Sanitize(await provider.ReplyAsync(Actor.Data,s,input,Game.Settings,_cancel.Token));
            if(_cancel.IsCancellationRequested || !IsInsideTree())return;
            _turns++;
            if (advancesRelationship) Game.ActionsLeft--;
            else { r.AffectionDelta = r.TrustDelta = r.RomanceDelta = r.AttractionDelta = r.EnergyDelta = r.StressDelta = 0; r.ProviderStatus += " · conversa livre"; }
            var normalized=input.ToLowerInvariant(); var repeated=s.RecentInputs.Contains(normalized);
            if(repeated) { r.AffectionDelta=Math.Min(0,r.AffectionDelta); r.TrustDelta=Math.Min(0,r.TrustDelta); }
            s.RecentInputs.Add(normalized); if(s.RecentInputs.Count>12)s.RecentInputs.RemoveAt(0);
            if (Actor.Data.CanBuildRelationship)
            {
                new RelationshipSystem().Apply(s,r.AffectionDelta,r.TrustDelta,r.RomanceDelta,r.AttractionDelta);
                s.Energy+=r.EnergyDelta; s.Stress+=r.StressDelta; s.Mood+=r.AffectionDelta; s.CurrentDesire=r.Desire;
            }
            s.CurrentEmotion=r.Emotion; s.Clamp();
            memory.RecordConfirmedPlayerAction(Actor.Data,s,input,Game.Day,Game.WorldMinutes);
            s.Conversation.Add("Jogador: "+input); s.Conversation.Add(Actor.Data.Name+": "+r.Dialogue); while(s.Conversation.Count>8)s.Conversation.RemoveAt(0);
            _line.Text=r.Dialogue; _status.Text=Actor.Data.CanBuildRelationship ? r.ProviderStatus+$" · Rel: {s.Relationship} · Humor: {s.CurrentMood}" : r.ProviderStatus+$" · NPC do mundo · Turno {_turns}/{MaxPlayerTurns}"; Changed?.Invoke();
        }
        catch(OperationCanceledException) { }
        catch(Exception e) { if(IsInsideTree()) { var alternative = new ProceduralDialogueProvider().Reply(Actor.Data, Actor.State, input); _line.Text = alternative.Dialogue; Actor.State.CurrentEmotion = alternative.Emotion; _status.Text=$"Falha técnica ({e.GetType().Name})"; } }
        finally { _busy=false; }
    }
    public override void _ExitTree() { _cancel.Cancel(); _cancel.Dispose(); }
}
