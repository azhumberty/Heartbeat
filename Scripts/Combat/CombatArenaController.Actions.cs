using Godot;
namespace Heartbeat;

public partial class CombatArenaController
{
    public async Task PlaySelected()
    {
        if(Busy||Manager.CanPlay(_selected).Length>0)return;
        Busy=true;_play.Disabled=true;_end.Disabled=true;_flee.Disabled=true;
        var card=Manager.State.Cards[Manager.State.Hand[_selected]];
        int before=Manager.State.Enemy.Health;int hp=Manager.State.Player.Health;
        var flying=new CardView {Card=card,Compact=true,Position=new(510,460),MouseFilter=MouseFilterEnum.Ignore};_effects.AddChild(flying);
        var tween=CreateTween();tween.SetParallel();
        tween.TweenProperty(flying,"position",new Vector2(575,170),.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(flying,"scale",new Vector2(.55f,.55f),.32);
        tween.TweenProperty(flying,"modulate:a",0f,.36).SetDelay(.08);
        tween.TweenProperty(flying,"rotation_degrees",12f,.32);
        await Delay(.34);flying.QueueFree();
        Manager.Play(_selected);Changed?.Invoke();
        if(card.CharacterId.Length>0)await Summon(card);
        int amount=before-Manager.State.Enemy.Health;
        FloatText(amount>0?"−"+amount:Manager.State.Player.Health>hp?"+"+(Manager.State.Player.Health-hp):CardRules.EffectName(card.Effects[0].Kind),amount>0?new Color("ffd2a2"):new Color("a7e5bb"));
        if(!ReduceMotion){
            var punch=CreateTween();punch.SetParallel(true);
            punch.TweenProperty(_enemy,"scale",new Vector2(1.1f, 1.1f),.08).SetTrans(Tween.TransitionType.Back);
            punch.TweenProperty(_enemy,"modulate",new Color(1,.35f,.35f),.08);
            punch.Chain().SetParallel(true);
            punch.TweenProperty(_enemy,"scale",Vector2.One,.18).SetTrans(Tween.TransitionType.Elastic);
            punch.TweenProperty(_enemy,"modulate",new Color(1,1,1),.18);
            var flash=new ColorRect {Color=new Color(1,.7f,.5f,.35f),MouseFilter=MouseFilterEnum.Ignore};
            _effects.AddChild(flash); flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var ft=CreateTween(); ft.TweenProperty(flash,"modulate:a",0f,.22); ft.TweenCallback(Callable.From(flash.QueueFree));
        }
        await Delay(.28);
        if(Manager.State.Result=="Victory"){var fade=CreateTween();fade.TweenProperty(_enemy,"scale",Vector2.One*.03f,.4).SetTrans(Tween.TransitionType.Back);await Delay(.4);}
        Busy=false;Refresh();
    }
    async Task Summon(CardDefinition c)
    {
        var tex=CardArt.TextureFor(c,Game);if(tex==null)return;
        var portrait=new TextureRect {Texture=tex,Position=new(760,95),Size=new(270,350),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore,Modulate=new Color(1,1,1,0)};_effects.AddChild(portrait);
        _message.Text=c.Name+": "+c.Phrase;
        var t=CreateTween();t.TweenProperty(portrait,"modulate:a",1f,.15);t.TweenInterval(.3);t.TweenProperty(portrait,"modulate:a",0f,.2);await Delay(.65);portrait.QueueFree();
    }
    void FloatText(string text,Color color)
    {
        var label=Ui.Text(text,36);label.Modulate=color;label.Position=new(565,230);_effects.AddChild(label);
        var t=CreateTween().SetParallel();t.TweenProperty(label,"position:y",150f,.6).SetTrans(Tween.TransitionType.Quad);t.TweenProperty(label,"modulate:a",0f,.6);t.TweenProperty(label,"scale",new Vector2(1.25f,1.25f),.6);t.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
    public async Task EndTurnAnimated()
    {
        if(Busy||Manager.State.Result.Length>0)return;
        Busy=true;_play.Disabled=true;_end.Disabled=true;_flee.Disabled=true;_message.Text="O inimigo prepara sua ação...";
        await Delay(.32);int before=Manager.State.Player.Health;Manager.EndTurn();Changed?.Invoke();
        if(!ReduceMotion){var t=CreateTween();t.TweenProperty(_enemy,"scale",new Vector2(1.2f,1.2f),.12).SetTrans(Tween.TransitionType.Back);t.TweenProperty(_enemy,"scale",Vector2.One,.2).SetTrans(Tween.TransitionType.Elastic);}
        FloatText(before>Manager.State.Player.Health?"-"+(before-Manager.State.Player.Health):"Guarda",new Color("f1a6a0"));await Delay(.32);Busy=false;Refresh();
    }
    async Task Retreat(){if(Busy)return;Busy=true;await Delay(.2);Manager.Flee();Busy=false;Refresh();}
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed){if(k.Keycode==Key.Space&&!k.Echo)_=EndTurnAnimated();GetViewport().SetInputAsHandled();}}
}
