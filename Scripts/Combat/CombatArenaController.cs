using Godot;
namespace Heartbeat;

public partial class CombatArenaController : Control
{
    public CombatManager Manager {get;set;}=null!;
    public GameSave Game {get;set;}=null!;
    public Action? Changed;
    public Action? Finished;
    public bool Busy {get;private set;}
    public bool ReduceMotion {get;set;}
    HBoxContainer _hand=null!;
    Label _playerText=null!,_enemyText=null!,_intent=null!,_message=null!,_turn=null!;
    ProgressBar _playerHealth=null!,_enemyHealth=null!;
    VBoxContainer _detail=null!;
    Button _play=null!,_end=null!,_return=null!,_flee=null!;
    Node3D _enemy=null!;
    Camera3D _camera=null!;
    SubViewport _viewport=null!;
    int _selected=-1;
    Control _effects=null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildArena();BuildInterface();Refresh();
    }
    void BuildArena()
    {
        var container=new SubViewportContainer {Stretch=true,MouseFilter=MouseFilterEnum.Ignore};AddChild(container);container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _viewport=new SubViewport {OwnWorld3D=true,Size=new(1280,720),RenderTargetUpdateMode=SubViewport.UpdateMode.Always};container.AddChild(_viewport);
        var stage=new Node3D();_viewport.AddChild(stage);
        bool night=Manager.State.Hour<6||Manager.State.Hour>=18;string arena=Manager.State.Arena;
        var env=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color(night?"0c1820":"455954"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("9eb1ae"),AmbientLightEnergy=night?.65f:.85f,FogEnabled=true,FogDensity=.022f,FogLightColor=new Color("384a42"),TonemapMode=Godot.Environment.ToneMapper.Filmic};
        stage.AddChild(new WorldEnvironment {Environment=env});
        stage.AddChild(new DirectionalLight3D {RotationDegrees=new(-38,-25,0),LightColor=new Color("f9dbad"),LightEnergy=night?.6f:1.2f,ShadowEnabled=true,DirectionalShadowMaxDistance=24});
        void Mesh(Mesh mesh,Vector3 pos,Material mat){stage.AddChild(new MeshInstance3D {Mesh=mesh,Position=pos,MaterialOverride=mat});}
        Mesh(new PlaneMesh {Size=new(45,45)},Vector3.Zero,SurfaceMaterials.ForestGround());
        var rng=new Random(42);
        for(int i=0;i<18;i++)
        {
            float x=(i%2==0?-1:1)*(5+rng.Next(8));float z=-9+rng.Next(13);
            Mesh(new CylinderMesh {TopRadius=.18f,BottomRadius=.4f,Height=7,RadialSegments=7},new(x,3.5f,z),SurfaceMaterials.Bark());
            Mesh(new CylinderMesh {TopRadius=0,BottomRadius=2,Height=6,RadialSegments=8},new(x,7,z),SurfaceMaterials.Forest("284637",ForestKind.Conifer));
        }
        for(int i=0;i<9;i++)Mesh(new SphereMesh {Radius=.45f,Height=.5f,RadialSegments=8,Rings=4},new(-5+i*1.3f,.15f,-4),SurfaceMaterials.MossyRock());
        if(arena=="Ruin")for(int i=0;i<5;i++){Mesh(new BoxMesh {Size=new(1.5f,1.7f+i%2*1.2f,1)},new(-4+i*2,1,-5),SurfaceMaterials.MossyRock());}
        if(arena=="Camp")
        {
            Mesh(new CylinderMesh{TopRadius=.6f,BottomRadius=1.1f,Height=.3f,RadialSegments=8},new(-3,.15f,-1),SurfaceMaterials.MossyRock());
            stage.AddChild(new OmniLight3D {Position=new(-3,1,-1),LightColor=new Color("ffae5a"),LightEnergy=2.5f,OmniRange=9});
            Mesh(new PrismMesh {Size=new(3,2,3)},new(4,1,-5),SurfaceMaterials.For("624b40"));
        }
        _enemy=EnemyVisual.Create(Manager.State.EnemyId);_enemy.Position=new(0,0,-1);stage.AddChild(_enemy);
        stage.AddChild(new OmniLight3D {Position=new(0,3,2),LightColor=new Color("a7d3db"),LightEnergy=2,OmniRange=8});
        _camera=new Camera3D {Position=new(0,3.5f,9),Fov=54,Current=true};stage.AddChild(_camera);_camera.LookAt(new Vector3(0,1.15f,-1));
    }
    void BuildInterface()
    {
        var shade=new ColorRect {Color=new Color("0a0f12c8"),MouseFilter=MouseFilterEnum.Ignore};AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);shade.OffsetBottom=112;
        var header=new HBoxContainer {Position=new(28,18)};AddChild(header);
        var p=new VBoxContainer {CustomMinimumSize=new(280,0)};header.AddChild(p);p.AddChild(Ui.Text("VIAJANTE",16));_playerText=Ui.Text("",15);p.AddChild(_playerText);_playerHealth=Bar(p,new Color("b14743"),new Color("e67373"));
        var spacer=new Control {CustomMinimumSize=new(55,0)};header.AddChild(spacer);
        var enemy=new VBoxContainer {CustomMinimumSize=new(330,0)};header.AddChild(enemy);enemy.AddChild(Ui.Text(EnemyDefinition.Get(Manager.State.EnemyId).Name.ToUpperInvariant(),19));_enemyText=Ui.Text("",14);enemy.AddChild(_enemyText);_enemyHealth=Bar(enemy,new Color("be8744"),new Color("e3a456"));
        _intent=Ui.Text("",17);enemy.AddChild(_intent);
        var actions=new VBoxContainer {Position=new(1010,18),CustomMinimumSize=new(240,0)};AddChild(actions);
        _turn=Ui.Text("",16);actions.AddChild(_turn);
        _end=Ui.Button("Encerrar turno",()=>_ = EndTurnAnimated());actions.AddChild(_end);
        _flee=Ui.Button("Recuar",()=>_ = Retreat());actions.AddChild(_flee);
        var reduced=new CheckButton {Text="Reduzir movimento",ButtonPressed=ReduceMotion};actions.AddChild(reduced);reduced.Toggled+=v=>ReduceMotion=v;
        var detailPanel=new PanelContainer {Position=new(28,145),CustomMinimumSize=new(270,0)};AddChild(detailPanel);
        detailPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("0a0e12f0"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,BorderColor=new Color("b39a64"),ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=12,ContentMarginBottom=12,CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,ShadowColor=new Color(0,0,0,.5f),ShadowSize=6});
        var details=new VBoxContainer();detailPanel.AddChild(details);_detail=new VBoxContainer();details.AddChild(_detail);
        _play=Ui.Button("Jogar carta",()=>_ = PlaySelected());details.AddChild(_play);
        _message=Ui.Text("Escolha uma carta. Revele fraquezas, defenda e ataque.",17);_message.Position=new(345,330);_message.Size=new(550,75);_message.HorizontalAlignment=HorizontalAlignment.Center;AddChild(_message);
        var bottom=new PanelContainer();AddChild(bottom);bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);bottom.OffsetTop=-280; // INCREASED HEIGHT TO PREVENT CLIPPING
        bottom.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("05080ae8"),ContentMarginLeft=30,ContentMarginRight=30,ContentMarginTop=20,ContentMarginBottom=10});
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled};bottom.AddChild(scroll);
        _hand=new HBoxContainer();_hand.AddThemeConstantOverride("separation", -8);scroll.AddChild(_hand); // NEGATIVE SEPARATION FOR OVERLAP
        _effects=new Control {MouseFilter=MouseFilterEnum.Ignore};AddChild(_effects);_effects.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _return=Ui.Button("Retornar à floresta",()=>Finished?.Invoke());_return.Position=new(470,407);_return.CustomMinimumSize=new(340,46);_return.Visible=false;AddChild(_return);
    }
    static ProgressBar Bar(Control parent,Color fill,Color light)
    {
        var bar=new ProgressBar {ShowPercentage=false,CustomMinimumSize=new(0,12)};
        bar.AddThemeStyleboxOverride("background",new StyleBoxFlat{BgColor=new Color("0a0e12"),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,BorderWidthTop=1,BorderWidthBottom=1,BorderWidthLeft=1,BorderWidthRight=1,BorderColor=new Color("1f262a")});
        bar.AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=fill,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ShadowColor=light,ShadowSize=2});parent.AddChild(bar);return bar;
    }
    static string Status(CombatantState s)=>string.Join(" · ",s.Status.Where(x=>x.Value>0).Select(x=>CardRules.EffectName(x.Key)+" "+x.Value+"t"));
    void Refresh()
    {
        var s=Manager.State;
        _playerText.Text=$"Vida {s.Player.Health}/{s.Player.MaxHealth} · Escudo {s.Player.Shield} · Mana {s.Mana}/{s.MaxMana}";
        _enemyText.Text=$"Vida {s.Enemy.Health}/{s.Enemy.MaxHealth} · Escudo {s.Enemy.Shield} · "+Status(s.Enemy);
        _playerHealth.MaxValue=s.Player.MaxHealth;_enemyHealth.MaxValue=s.Enemy.MaxHealth;
        CreateTween().TweenProperty(_playerHealth,"value",(double)s.Player.Health,.22);
        CreateTween().TweenProperty(_enemyHealth,"value",(double)s.Enemy.Health,.22);
        string intent = Manager.Intent;
        _intent.Text = (intent.Contains("Atacar") ? "⚔️ " : intent.Contains("Atordoado") ? "💤 " : "🛡️ ") + intent;
        _turn.Text=$"TURNO {s.Turn} · Monte {s.DrawPile.Count} / Descarte {s.Discard.Count}";
        CardUi.Clear(_hand);
        for(int i=0;i<s.Hand.Count;i++)
        {
            int index=i;var data=CardRules.Copy(s.Cards[s.Hand[i]]);
            if(data.CharacterId.Length>0&&data.ImagePath==""){var person=new CharacterRepository().Load(data.CharacterId);if(person!=null)data.ImagePath=CardArt.PathFor(person,Game.CharacterStates.GetValueOrDefault(person.Id));}
            var card=new CardView {Card=data,Compact=true,Clicked=()=>Select(index)};_hand.AddChild(card);
            card.Modulate=new Color(1,1,1,0);card.CreateTween().TweenProperty(card,"modulate:a",1f,.15+i*.025);
            if(s.Mana<data.Cost)card.SelfModulate=new Color(.6f,.6f,.6f);
        }
        _selected=-1;CardUi.Clear(_detail);_detail.AddChild(Ui.Text("SELECIONE UMA CARTA",16));_detail.AddChild(Ui.Text(Status(s.Player),13));_play.Disabled=true;
        _end.Disabled=Busy||s.Result.Length>0;_flee.Disabled=Busy||s.Result.Length>0;
        if(s.Result.Length>0)
        {
            Manager.Settle();Changed?.Invoke();_message.Text=(s.Result=="Victory"?"VITÓRIA":s.Result=="Defeat"?"DERROTA":"RETIRADA")+"\n"+s.RewardText;_return.Visible=true;_end.Visible=false;_flee.Visible=false;
        }
    }
    public void Select(int index)
    {
        if(Busy||Manager.State.Result.Length>0||index<0||index>=Manager.State.Hand.Count)return;
        _selected=index;var c=Manager.State.Cards[Manager.State.Hand[index]];
        CardUi.Clear(_detail);_detail.AddChild(Ui.Text(c.Name,22));_detail.AddChild(Ui.Text(c.Description,15));_detail.AddChild(Ui.Text(CardRules.Describe(c),16));
        if(c.Passive!=null)_detail.AddChild(Ui.Text("Passiva por turno: "+CardRules.EffectName(c.Passive.Kind)+" "+c.Passive.Value,14));
        string reason=Manager.CanPlay(index);_play.Disabled=reason.Length>0;_message.Text=reason.Length>0?reason:c.Phrase;
    }
    async Task Delay(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    public async Task PlaySelected()
    {
        if(Busy||Manager.CanPlay(_selected).Length>0)return;
        Busy=true;_play.Disabled=true;_end.Disabled=true;_flee.Disabled=true;
        var card=Manager.State.Cards[Manager.State.Hand[_selected]];
        int before=Manager.State.Enemy.Health;int hp=Manager.State.Player.Health;
        var flying=new CardView {Card=card,Compact=true,Position=new(510,460),MouseFilter=MouseFilterEnum.Ignore};_effects.AddChild(flying);
        var tween=CreateTween();tween.SetParallel();tween.TweenProperty(flying,"position",new Vector2(575,190),.28);tween.TweenProperty(flying,"scale",new Vector2(.5f,.5f),.28);tween.TweenProperty(flying,"modulate:a",0f,.3);
        await Delay(.3);flying.QueueFree();
        Manager.Play(_selected);Changed?.Invoke();
        if(card.CharacterId.Length>0)await Summon(card);
        int amount=before-Manager.State.Enemy.Health;
        FloatText(amount>0?"−"+amount:Manager.State.Player.Health>hp?"+"+(Manager.State.Player.Health-hp):CardRules.EffectName(card.Effects[0].Kind),amount>0?new Color("ffd2a2"):new Color("a7e5bb"));
        if(!ReduceMotion){
            var punch=CreateTween();punch.SetParallel(true);
            punch.TweenProperty(_enemy,"position:z",-.65f,.07);
            if (_enemy.HasNode("CustomSprite")) punch.TweenProperty(_enemy.GetNode("CustomSprite"),"modulate",new Color(1,0,0),.07);
            punch.Chain().TweenProperty(_enemy,"position:z",-1f,.15);
            if (_enemy.HasNode("CustomSprite")) punch.TweenProperty(_enemy.GetNode("CustomSprite"),"modulate",new Color(1,1,1),.15);
        }
        await Delay(.25);
        if(Manager.State.Result=="Victory"){var fade=CreateTween();fade.TweenProperty(_enemy,"scale",Vector3.One*.03f,.35);await Delay(.35);}
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
        var label=Ui.Text(text,34);label.Modulate=color;label.Position=new(565,230);_effects.AddChild(label);
        var t=CreateTween().SetParallel();t.TweenProperty(label,"position:y",165f,.55);t.TweenProperty(label,"modulate:a",0f,.55);t.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
    public async Task EndTurnAnimated()
    {
        if(Busy||Manager.State.Result.Length>0)return;
        Busy=true;_play.Disabled=true;_end.Disabled=true;_flee.Disabled=true;_message.Text="O inimigo prepara sua ação...";
        await Delay(.3);int before=Manager.State.Player.Health;Manager.EndTurn();Changed?.Invoke();
        if(!ReduceMotion){var t=CreateTween();t.TweenProperty(_enemy,"position:z",.3f,.12);t.TweenProperty(_enemy,"position:z",-1f,.18);}
        FloatText(before>Manager.State.Player.Health?"−"+(before-Manager.State.Player.Health):"Guarda",new Color("f1a6a0"));await Delay(.3);Busy=false;Refresh();
    }
    async Task Retreat(){if(Busy)return;Busy=true;await Delay(.2);Manager.Flee();Busy=false;Refresh();}
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed){if(k.Keycode==Key.Space&&!k.Echo)_=EndTurnAnimated();GetViewport().SetInputAsHandled();}}
}
