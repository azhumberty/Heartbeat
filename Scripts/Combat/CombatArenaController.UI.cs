using Godot;
namespace Heartbeat;

public partial class CombatArenaController
{
    void BuildInterface()
    {
        var shade=new ColorRect {Color=new Color("0a0f1288"),MouseFilter=MouseFilterEnum.Ignore};AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);shade.OffsetBottom=150;
        var header=new HBoxContainer();header.SetAnchorsPreset(LayoutPreset.TopWide);header.OffsetLeft=24;header.OffsetRight=-282;header.OffsetTop=16;header.OffsetBottom=145;header.AddThemeConstantOverride("separation",18); AddChild(header);

        var playerPanel=StatPanel(Game.PlayerName.ToUpperInvariant(), out _playerName, out _playerHpText, out _playerHealth, out _playerManaText, out _playerMana, true);
        header.AddChild(playerPanel);

        var spacer=new Control {SizeFlagsHorizontal=SizeFlags.ExpandFill, CustomMinimumSize=new(40,0)}; header.AddChild(spacer);

        var enemyPanel=StatPanel(EnemyDefinition.Get(Manager.State.EnemyId).Name.ToUpperInvariant(), out _enemyName, out _enemyHpText, out _enemyHealth, out _, out _, false);
        header.AddChild(enemyPanel);
        _intentRow=new HBoxContainer(); _intentRow.AddThemeConstantOverride("separation", 8);
        ((VBoxContainer)enemyPanel.GetChild(0)).AddChild(Ui.Text("INTENÇÃO",12));
        ((VBoxContainer)enemyPanel.GetChild(0)).AddChild(_intentRow);

        var actions=new VBoxContainer {CustomMinimumSize=new(240,0)};actions.SetAnchorsPreset(LayoutPreset.TopRight);actions.OffsetLeft=-264;actions.OffsetRight=-24;actions.OffsetTop=18;actions.OffsetBottom=155;AddChild(actions);
        _turn=Ui.Text("",16); _turn.AutowrapMode=TextServer.AutowrapMode.Off; actions.AddChild(_turn);
        _end=Ui.Button("Encerrar turno",()=>_ = EndTurnAnimated());actions.AddChild(_end);
        _flee=Ui.Button("Recuar",()=>_ = Retreat());actions.AddChild(_flee);
        var reduced=new CheckButton {Text="Reduzir movimento",ButtonPressed=ReduceMotion};actions.AddChild(reduced);reduced.Toggled+=v=>ReduceMotion=v;

        _detailPanel=new PanelContainer {Position=new(28,175),CustomMinimumSize=new(300,0),Visible=false};AddChild(_detailPanel);
        _detailPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("0a0e12e0"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,BorderColor=new Color("b39a64"),ContentMarginLeft=14,ContentMarginRight=14,ContentMarginTop=12,ContentMarginBottom=12,CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,ShadowColor=new Color(0,0,0,.5f),ShadowSize=6});
        var details=new VBoxContainer();_detailPanel.AddChild(details);var detailHeader=new HBoxContainer();details.AddChild(detailHeader);var detailTitle=Ui.Text("DETALHES DA CARTA",13);detailTitle.SizeFlagsHorizontal=SizeFlags.ExpandFill;detailHeader.AddChild(detailTitle);detailHeader.AddChild(Ui.Button("×",()=>_detailPanel.Visible=false));_detail=new VBoxContainer();details.AddChild(_detail);
        _play=Ui.Button("Jogar carta",()=>_ = PlaySelected());details.AddChild(_play);
        _message=Ui.Text("Escolha uma carta. Revele fraquezas, defenda e ataque.",17);_message.SetAnchorsPreset(LayoutPreset.TopRight);_message.OffsetLeft=-410;_message.OffsetRight=-30;_message.OffsetTop=190;_message.OffsetBottom=330;_message.HorizontalAlignment=HorizontalAlignment.Right;_message.AutowrapMode=TextServer.AutowrapMode.Word;_message.AddThemeColorOverride("font_color", new Color("f2d388"));_message.AddThemeColorOverride("font_shadow_color", new Color(0,0,0));AddChild(_message);
        var bottom=new PanelContainer();AddChild(bottom);bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);bottom.OffsetTop=-196;
        bottom.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color(0f, 0f, 0f, 0.65f),ContentMarginLeft=30,ContentMarginRight=30,ContentMarginTop=20,ContentMarginBottom=10});
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled};bottom.AddChild(scroll);
        _hand=new HBoxContainer();_hand.AddThemeConstantOverride("separation", -8);scroll.AddChild(_hand);
        _effects=new Control {MouseFilter=MouseFilterEnum.Ignore};AddChild(_effects);_effects.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _return=Ui.Button("Retornar ao atlas",()=>Finished?.Invoke());_return.SetAnchorsPreset(LayoutPreset.Center);_return.OffsetLeft=-170;_return.OffsetRight=170;_return.OffsetTop=45;_return.OffsetBottom=91;_return.Visible=false;AddChild(_return);
    }
    static ProgressBar Bar(Control parent,Color fill,Color light)
    {
        var bar=new ProgressBar {ShowPercentage=false,CustomMinimumSize=new(0,14)};
        bar.AddThemeStyleboxOverride("background",new StyleBoxFlat{BgColor=new Color("0a0e12"),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,BorderWidthTop=1,BorderWidthBottom=1,BorderWidthLeft=1,BorderWidthRight=1,BorderColor=new Color("1f262a")});
        bar.AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=fill,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,ShadowColor=light,ShadowSize=3});parent.AddChild(bar);return bar;
    }
    static string Status(CombatantState s)=>string.Join(" · ",s.Status.Where(x=>x.Value>0).Select(x=>CardRules.EffectName(x.Key)+" "+x.Value+"t"));
    void RefreshIntents()
    {
        CardUi.Clear(_intentRow);
        foreach (var intent in Manager.Intents)
        {
            var chip=new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                BgColor=new Color("1a2230ee"), ContentMarginLeft=8, ContentMarginRight=8, ContentMarginTop=4, ContentMarginBottom=4,
                CornerRadiusTopLeft=8, CornerRadiusTopRight=8, CornerRadiusBottomLeft=8, CornerRadiusBottomRight=8,
                BorderWidthLeft=1, BorderWidthRight=1, BorderWidthTop=1, BorderWidthBottom=1, BorderColor=new Color("b39a6455")
            });
            var label=Ui.Text($"{intent.Icon} {intent.Label}", 14);
            label.AutowrapMode=TextServer.AutowrapMode.Off;
            chip.AddChild(label);
            _intentRow.AddChild(chip);
        }
    }
}
