using Godot;
namespace Heartbeat;

public partial class DialogueController : Control
{
    public GameSave Game { get; set; }=new(); public NpcActor Actor { get; set; }=null!; public Action? Closed; public Action? Changed; public Action<NpcActor>? DuelRequested;
    Label _line=null!,_status=null!; TextEdit _input=null!; TextureRect _portrait=null!; bool _busy; readonly CancellationTokenSource _cancel=new();
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Ui.Panel(this,Actor.Data.Name,out var body,()=>Closed?.Invoke());
        var row=new HBoxContainer { SizeFlagsVertical=SizeFlags.ExpandFill }; body.AddChild(row);
        _portrait=new TextureRect { Texture=Actor.Portraits.Get(Actor.Data,Actor.State),CustomMinimumSize=new(360,0),SizeFlagsHorizontal=SizeFlags.ExpandFill,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered }; row.AddChild(_portrait);
        var text=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new(420,0) }; row.AddChild(text);
        _line=Ui.Text($"{Actor.Data.Name} olha para você. O que você quer dizer?",23); _line.SizeFlagsVertical=SizeFlags.ExpandFill; text.AddChild(_line);
        _status=Ui.Text("",14); text.AddChild(_status);
        _input=new TextEdit { PlaceholderText="Digite aqui… Enter envia · Shift+Enter quebra linha",CustomMinimumSize=new(0,100),WrapMode=TextEdit.LineWrappingMode.Boundary }; text.AddChild(_input);
        _input.GuiInput+=e=> { if(e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode==Key.Enter && !k.ShiftPressed) { _input.AcceptEvent(); _=Send(); } };
        text.AddChild(Ui.Button("Enviar",()=>_=Send())); 
        
        if (Actor.Data.Id == "merchant")
        {
            text.AddChild(Ui.Button("💰 Comprar Cartas", () => {
                var shop = new CardShopController { Game = Game, Closed = Closed };
                GetParent().AddChild(shop);
                QueueFree();
            }));
        }
        else
        {
            text.AddChild(Ui.Button("Convidar para o parque",()=>{ _input.Text="Quer passar um tempo comigo no parque?"; _=Send(); }));
            text.AddChild(Ui.Button("⚔️ Duelo Amistoso", () => {
                DuelRequested?.Invoke(Actor);
                Closed?.Invoke();
            }));
        }
        
        text.AddChild(Ui.Button("Perfil",()=>_line.Text=$"{Actor.Data.Description}\n{Actor.Data.Personality}\n\nAfeto {Actor.State.Affection} · Confiança {Actor.State.Trust}\nRomance {Actor.State.Romance} · Respeito {Actor.State.Emotions.Respect}\nHumor: {Actor.State.CurrentMood} · Energia {Actor.State.Energy}\n{string.Join("\n",new SocialMemoryService().Retrieve(Actor.State,"relação",3).Select(m=>m.Content))}")); _input.GrabFocus();
    }
    async Task Send()
    {
        if(_busy || string.IsNullOrWhiteSpace(_input.Text))return;
        var advancesRelationship = Game.ActionsLeft > 0;
        var input=_input.Text.Trim(); input=input[..Math.Min(input.Length,1000)]; _input.Text=""; _busy=true; _status.Text=Actor.Data.Name+" está pensando…";
        try
        {
            var s=Actor.State; s.TimeContext=$"Dia {Game.Day}, {Game.Period}";
            var memory=new SocialMemoryService();
            memory.PrepareTurn(Actor.Data,s,$"Floresta medieval; local {s.CurrentLocation}; {s.TimeContext}; atividade {s.CurrentActivity}; desejo {s.CurrentDesire}",Game.Day,Game.WorldMinutes);
            IDialogueProvider provider=Game.Settings.UseOnlineAi?new GroqDialogueProvider():new ProceduralDialogueProvider();
            var r=DialogueValidator.Sanitize(await provider.ReplyAsync(Actor.Data,s,input,Game.Settings,_cancel.Token));
            if(_cancel.IsCancellationRequested || !IsInsideTree())return;
            if (advancesRelationship) Game.ActionsLeft--;
            else { r.AffectionDelta = r.TrustDelta = r.RomanceDelta = r.AttractionDelta = r.EnergyDelta = r.StressDelta = 0; r.ProviderStatus += " · conversa livre, sem progresso extra"; }
            var normalized=input.ToLowerInvariant(); var repeated=s.RecentInputs.Contains(normalized);
            if(repeated) { r.AffectionDelta=Math.Min(0,r.AffectionDelta); r.TrustDelta=Math.Min(0,r.TrustDelta); r.RomanceDelta=Math.Min(0,r.RomanceDelta); r.AttractionDelta=Math.Min(0,r.AttractionDelta); }
            s.RecentInputs.Add(normalized); if(s.RecentInputs.Count>12)s.RecentInputs.RemoveAt(0);
            new RelationshipSystem().Apply(s,r.AffectionDelta,r.TrustDelta,r.RomanceDelta,r.AttractionDelta);
            s.Energy+=r.EnergyDelta; s.Stress+=r.StressDelta; s.Mood+=r.AffectionDelta; s.CurrentEmotion=r.Emotion; s.CurrentDesire=r.Desire; s.Clamp();
            memory.RecordConfirmedPlayerAction(Actor.Data,s,input,Game.Day,Game.WorldMinutes);
            s.Conversation.Add("Jogador: "+input); s.Conversation.Add(Actor.Data.Name+": "+r.Dialogue); while(s.Conversation.Count>8)s.Conversation.RemoveAt(0);
            _line.Text=r.Dialogue; _status.Text=r.ProviderStatus+" · "+s.Relationship+" · "+s.CurrentMood; Actor.RefreshPortrait(); _portrait.Texture=Actor.Portraits.Get(Actor.Data,s); Changed?.Invoke();
        }
        catch(OperationCanceledException) { }
        catch(Exception e) { if(IsInsideTree()) { var alternative = new ProceduralDialogueProvider().Reply(Actor.Data, Actor.State, input); _line.Text = alternative.Dialogue; Actor.State.CurrentEmotion = alternative.Emotion; Actor.RefreshPortrait(); _status.Text="Falha técnica · personalidade e memória locais ("+e.GetType().Name+")"; } }
        finally { _busy=false; }
    }
    public override void _ExitTree() { _cancel.Cancel(); _cancel.Dispose(); }
}
